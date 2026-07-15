using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to confirm a delivery note, making it ready for delivery.
/// </summary>
public sealed record ConfirmDeliveryNoteCommand(Guid DeliveryNoteId) : IRequest<Result>;

/// <summary>
/// Handler for ConfirmDeliveryNoteCommand.
/// </summary>
public sealed class ConfirmDeliveryNoteCommandHandler : IRequestHandler<ConfirmDeliveryNoteCommand, Result>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ConfirmDeliveryNoteCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ConfirmDeliveryNoteCommand request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithLinesAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        var result = deliveryNote.Confirm();
        if (result.IsFailure)
            return result;

        deliveryNote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _deliveryNoteRepository.UpdateAsync(deliveryNote, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.Confirmed,
                "DeliveryNote",
                deliveryNote.Id,
                newValues: new { deliveryNote.Number.Value, deliveryNote.Status },
                cancellationToken: cancellationToken);
        }
        catch { }

        return Result.Success();
    }
}
