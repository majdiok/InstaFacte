using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to start delivery (transition from Confirmed → InTransit).
/// </summary>
public sealed record StartDeliveryCommand(Guid DeliveryNoteId) : IRequest<Result>;

/// <summary>
/// Handler for StartDeliveryCommand.
/// </summary>
public sealed class StartDeliveryCommandHandler : IRequestHandler<StartDeliveryCommand, Result>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IAuditService _auditService;

    public StartDeliveryCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IAuditService auditService)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(StartDeliveryCommand request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        var result = deliveryNote.StartDelivery();
        if (result.IsFailure)
            return result;

        await _deliveryNoteRepository.UpdateAsync(deliveryNote, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.InTransit,
                "DeliveryNote",
                deliveryNote.Id,
                newValues: new { deliveryNote.Number.Value, deliveryNote.Status },
                cancellationToken: cancellationToken);
        }
        catch { }

        return Result.Success();
    }
}
