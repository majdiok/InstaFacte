using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to record delivery completion.
/// </summary>
public sealed record RecordDeliveryCommand(Guid DeliveryNoteId, RecordDeliveryDto Delivery) : IRequest<Result>;

/// <summary>
/// Validator for RecordDeliveryCommand.
/// </summary>
public sealed class RecordDeliveryCommandValidator : AbstractValidator<RecordDeliveryCommand>
{
    public RecordDeliveryCommandValidator()
    {
        RuleFor(x => x.DeliveryNoteId)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de livraison est obligatoire");

        RuleFor(x => x.Delivery.DeliveryDate)
            .NotEmpty()
            .WithMessage("La date de livraison est obligatoire");

        RuleFor(x => x.Delivery.RecipientName)
            .NotEmpty()
            .WithMessage("Le nom du réceptionnaire est obligatoire")
            .MaximumLength(200)
            .WithMessage("Le nom du réceptionnaire ne peut pas dépasser 200 caractères");
    }
}

/// <summary>
/// Handler for RecordDeliveryCommand.
/// </summary>
public sealed class RecordDeliveryCommandHandler : IRequestHandler<RecordDeliveryCommand, Result>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public RecordDeliveryCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IUnitOfWork unitOfWork,
        IAuditService auditService)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Result> Handle(RecordDeliveryCommand request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithLinesAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        var dto = request.Delivery;

        // Record line quantities if provided
        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            foreach (var lineDto in dto.Lines)
            {
                var line = deliveryNote.Lines.FirstOrDefault(l => l.Id == lineDto.LineId);
                if (line is null)
                    return Result.Failure(Error.NotFound("DeliveryNoteLine", lineDto.LineId));

                var recordResult = line.RecordDelivery(
                    lineDto.DeliveredQuantity,
                    lineDto.RejectedQuantity,
                    lineDto.RejectionReason);

                if (recordResult.IsFailure)
                    return recordResult;
            }
        }
        else
        {
            // If no line details provided, assume all items delivered
            foreach (var line in deliveryNote.Lines)
            {
                var recordResult = line.RecordDelivery(line.OrderedQuantity, 0, null);
                if (recordResult.IsFailure)
                    return recordResult;
            }
        }

        // Record overall delivery
        var result = deliveryNote.RecordDelivery(
            dto.DeliveryDate,
            dto.RecipientName,
            dto.RecipientSignature);

        if (result.IsFailure)
            return result;

        await _deliveryNoteRepository.UpdateAsync(deliveryNote, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.Delivered,
                "DeliveryNote",
                deliveryNote.Id,
                newValues: new
                {
                    deliveryNote.Number.Value,
                    deliveryNote.Status,
                    dto.RecipientName,
                    dto.DeliveryDate,
                    IsPartial = !deliveryNote.IsFullyDelivered
                },
                cancellationToken: cancellationToken);
        }
        catch { }

        return Result.Success();
    }
}
