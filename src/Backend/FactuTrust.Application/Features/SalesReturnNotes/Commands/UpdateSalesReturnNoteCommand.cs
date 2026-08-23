using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Commands;

public sealed record UpdateSalesReturnNoteCommand(Guid Id, UpdateSalesReturnNoteDto Dto) : IRequest<Result>;

public sealed class UpdateSalesReturnNoteCommandHandler : IRequestHandler<UpdateSalesReturnNoteCommand, Result>
{
    private readonly ISalesReturnNoteRepository _returnNoteRepository;
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdateSalesReturnNoteCommandHandler(
        ISalesReturnNoteRepository returnNoteRepository,
        IDeliveryNoteRepository deliveryNoteRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _returnNoteRepository = returnNoteRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdateSalesReturnNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _returnNoteRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (note is null)
            return Result.Failure(Error.NotFound("SalesReturnNote", request.Id));

        if (!note.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être modifiés."));

        var dto = request.Dto;
        if (dto.Lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Au moins une ligne est requise."));

        var deliveryNote = await _deliveryNoteRepository.GetByIdWithDetailsAsync(note.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure(Error.NotFound("DeliveryNote", note.DeliveryNoteId));

        var headerResult = note.UpdateHeader(dto.ReturnDate, dto.Reason, dto.Notes);
        if (headerResult.IsFailure)
            return headerResult;

        var clearResult = note.ClearLines();
        if (clearResult.IsFailure)
            return clearResult;

        foreach (var lineDto in dto.Lines)
        {
            var source = deliveryNote.Lines.FirstOrDefault(l => l.Id == lineDto.DeliveryNoteLineId);
            if (source is null)
                return Result.Failure(Error.NotFound("DeliveryNoteLine", lineDto.DeliveryNoteLineId));

            var addResult = note.AddLine(source, lineDto.ReturnedQuantity, lineDto.Notes);
            if (addResult.IsFailure)
                return addResult;
        }

        note.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _returnNoteRepository.UpdateAsync(note, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesReturnNote.Updated,
            "SalesReturnNote",
            note.Id,
            newValues: new { note.Number.Value },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
