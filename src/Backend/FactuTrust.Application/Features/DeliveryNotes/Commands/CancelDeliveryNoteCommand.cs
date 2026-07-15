using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to cancel a delivery note.
/// </summary>
public sealed record CancelDeliveryNoteCommand(Guid DeliveryNoteId, string Reason) : IRequest<Result>;

/// <summary>
/// Validator for CancelDeliveryNoteCommand.
/// </summary>
public sealed class CancelDeliveryNoteCommandValidator : AbstractValidator<CancelDeliveryNoteCommand>
{
    public CancelDeliveryNoteCommandValidator()
    {
        RuleFor(x => x.DeliveryNoteId)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de livraison est obligatoire");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Le motif d'annulation est obligatoire")
            .MaximumLength(500)
            .WithMessage("Le motif d'annulation ne peut pas dépasser 500 caractères");
    }
}

/// <summary>
/// Handler for CancelDeliveryNoteCommand.
/// </summary>
public sealed class CancelDeliveryNoteCommandHandler : IRequestHandler<CancelDeliveryNoteCommand, Result>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public CancelDeliveryNoteCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IUnitOfWork unitOfWork,
        IAuditService auditService)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelDeliveryNoteCommand request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        var result = deliveryNote.Cancel(request.Reason);
        if (result.IsFailure)
            return result;

        await _deliveryNoteRepository.UpdateAsync(deliveryNote, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.Cancelled,
                "DeliveryNote",
                deliveryNote.Id,
                newValues: new { deliveryNote.Number.Value, request.Reason },
                cancellationToken: cancellationToken);
        }
        catch { }

        return Result.Success();
    }
}
