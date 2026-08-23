using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Commands;

public sealed record CreateSalesReturnNoteCommand(CreateSalesReturnNoteDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateSalesReturnNoteCommandHandler : IRequestHandler<CreateSalesReturnNoteCommand, Result<Guid>>
{
    private readonly ISalesReturnNoteRepository _returnNoteRepository;
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;

    public CreateSalesReturnNoteCommandHandler(
        ISalesReturnNoteRepository returnNoteRepository,
        IDeliveryNoteRepository deliveryNoteRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService)
    {
        _returnNoteRepository = returnNoteRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
    }

    public async Task<Result<Guid>> Handle(CreateSalesReturnNoteCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        if (dto.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Au moins une ligne est requise."));

        var deliveryNote = await _deliveryNoteRepository.GetByIdWithDetailsAsync(dto.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure<Guid>(Error.NotFound("DeliveryNote", dto.DeliveryNoteId));

        var year = dto.ReturnDate.Year;
        var numberResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId.Value,
            NumberingDocumentType.SalesReturnNote,
            year,
            dto.ReturnDate,
            cancellationToken);

        var number = SalesReturnNoteNumber.FromRendered(
            numberResult.Value, numberResult.Year, numberResult.Sequence);

        var createResult = Domain.Entities.SalesReturnNote.Create(
            number,
            deliveryNote,
            dto.ReturnDate,
            dto.Reason,
            dto.Notes);

        if (createResult.IsFailure)
            return Result.Failure<Guid>(createResult.Error);

        var note = createResult.Value;

        foreach (var lineDto in dto.Lines)
        {
            var source = deliveryNote.Lines.FirstOrDefault(l => l.Id == lineDto.DeliveryNoteLineId);
            if (source is null)
                return Result.Failure<Guid>(Error.NotFound("DeliveryNoteLine", lineDto.DeliveryNoteLineId));

            var addResult = note.AddLine(source, lineDto.ReturnedQuantity, lineDto.Notes);
            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        note.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _returnNoteRepository.AddAsync(note, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesReturnNote.Created,
            "SalesReturnNote",
            note.Id,
            newValues: new { note.Number.Value, DeliveryNote = deliveryNote.Number.Value, LineCount = note.Lines.Count },
            cancellationToken: cancellationToken);

        return Result.Success(note.Id);
    }
}
