using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SalesOrders.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.SalesOrders.Commands;

// ─────────────────────────────── Confirmation ───────────────────────────────

/// <summary>
/// Confirme une commande : elle devient un engagement ferme et entre au carnet de commandes.
/// La réservation de stock est branchée au lot 2, derrière un drapeau : ce handler ne fait
/// aujourd'hui que la transition d'état.
/// </summary>
public sealed record ConfirmSalesOrderCommand(Guid SalesOrderId) : IRequest<Result>;

public sealed class ConfirmSalesOrderCommandHandler : IRequestHandler<ConfirmSalesOrderCommand, Result>
{
    private readonly ISalesOrderRepository _repository;
    private readonly ISalesOrderStockReservationService _reservationService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ConfirmSalesOrderCommandHandler(
        ISalesOrderRepository repository,
        ISalesOrderStockReservationService reservationService,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _reservationService = reservationService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ConfirmSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("Commande", request.SalesOrderId));

        var result = order.Confirm();
        if (result.IsFailure)
            return result;

        // Réservation du stock — sans effet tant que Features:SalesOrders:StockReservationEnabled
        // est à false, qui est le défaut.
        var reservation = await _reservationService.ReserveAsync(order, cancellationToken);
        if (reservation.IsFailure)
            return reservation;

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(order, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesOrder.Confirmed,
            "SalesOrder",
            order.Id,
            newValues: new { order.Number.Value, order.TotalAmount.Amount },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ─────────────────────────────── Annulation ───────────────────────────────

/// <summary>
/// Annule une commande n'ayant donné lieu à AUCUN mouvement. Dès qu'une livraison ou une
/// facturation existe, c'est la clôture qu'il faut employer — voir
/// <see cref="CloseSalesOrderCommand"/>.
/// </summary>
public sealed record CancelSalesOrderCommand(Guid SalesOrderId, CancelSalesOrderDto Dto) : IRequest<Result>;

public sealed class CancelSalesOrderCommandValidator : AbstractValidator<CancelSalesOrderCommand>
{
    public CancelSalesOrderCommandValidator()
    {
        RuleFor(x => x.Dto.Reason)
            .NotEmpty().WithMessage("Le motif d'annulation est obligatoire")
            .MaximumLength(500);
    }
}

public sealed class CancelSalesOrderCommandHandler : IRequestHandler<CancelSalesOrderCommand, Result>
{
    private readonly ISalesOrderRepository _repository;
    private readonly ISalesOrderStockReservationService _reservationService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CancelSalesOrderCommandHandler(
        ISalesOrderRepository repository,
        ISalesOrderStockReservationService reservationService,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _reservationService = reservationService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("Commande", request.SalesOrderId));

        var result = order.Cancel(request.Dto.Reason);
        if (result.IsFailure)
            return result;

        // Le stock immobilisé doit redevenir disponible immédiatement.
        var release = await _reservationService.ReleaseAsync(order, cancellationToken);
        if (release.IsFailure)
            return release;

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(order, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesOrder.Cancelled,
            "SalesOrder",
            order.Id,
            newValues: new { order.Number.Value, request.Dto.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ─────────────────────────────── Clôture ───────────────────────────────

/// <summary>
/// Solde une commande entamée en abandonnant le reste à livrer : le client renonce au
/// reliquat. La commande sort du carnet sans que les livraisons déjà faites soient effacées.
/// </summary>
public sealed record CloseSalesOrderCommand(Guid SalesOrderId, CloseSalesOrderDto Dto) : IRequest<Result>;

public sealed class CloseSalesOrderCommandValidator : AbstractValidator<CloseSalesOrderCommand>
{
    public CloseSalesOrderCommandValidator()
    {
        RuleFor(x => x.Dto.Reason)
            .NotEmpty().WithMessage("Le motif de clôture est obligatoire")
            .MaximumLength(500);
    }
}

public sealed class CloseSalesOrderCommandHandler : IRequestHandler<CloseSalesOrderCommand, Result>
{
    private readonly ISalesOrderRepository _repository;
    private readonly ISalesOrderStockReservationService _reservationService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CloseSalesOrderCommandHandler(
        ISalesOrderRepository repository,
        ISalesOrderStockReservationService reservationService,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _reservationService = reservationService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CloseSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("Commande", request.SalesOrderId));

        var abandonedQuantity = order.TotalPendingDeliveryQuantity;
        var abandonedAmountHt = order.BacklogAmountHt;

        var result = order.Close(request.Dto.Reason);
        if (result.IsFailure)
            return result;

        // Le reliquat est abandonné : sa réservation n'a plus lieu d'être.
        var release = await _reservationService.ReleaseAsync(order, cancellationToken);
        if (release.IsFailure)
            return release;

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(order, cancellationToken);

        // Le reliquat abandonné est tracé : c'est une perte de chiffre d'affaires potentiel,
        // pas un simple changement d'état.
        await _auditService.LogAsync(
            AuditActions.SalesOrder.Closed,
            "SalesOrder",
            order.Id,
            newValues: new { order.Number.Value, request.Dto.Reason, abandonedQuantity, abandonedAmountHt },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ─────────────────────────────── En-tête ───────────────────────────────

/// <summary>Met à jour l'en-tête d'un brouillon (dates, référence, notes, conditions).</summary>
public sealed record UpdateSalesOrderCommand(Guid SalesOrderId, UpdateSalesOrderDto Dto) : IRequest<Result>;

public sealed class UpdateSalesOrderCommandHandler : IRequestHandler<UpdateSalesOrderCommand, Result>
{
    private readonly ISalesOrderRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdateSalesOrderCommandHandler(
        ISalesOrderRepository repository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("Commande", request.SalesOrderId));

        if (!order.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        order.UpdateHeader(
            request.Dto.ExpectedDeliveryDate,
            request.Dto.Reference,
            request.Dto.Notes,
            request.Dto.PaymentTerms);

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(order, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesOrder.Updated,
            "SalesOrder",
            order.Id,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
