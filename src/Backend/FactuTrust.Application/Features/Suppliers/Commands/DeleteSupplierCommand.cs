using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Suppliers.Commands;

/// <summary>
/// Command to delete a supplier.
/// A supplier can only be deleted if they have no associated purchase orders.
/// </summary>
public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Result>;

/// <summary>
/// Handler for DeleteSupplierCommand.
/// </summary>
public sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Result>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public DeleteSupplierCommandHandler(
        ISupplierRepository supplierRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _supplierRepository = supplierRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier is null)
            return Result.Failure(Error.NotFound("Supplier", request.Id));

        // Check for existing purchase orders
        var hasOrders = await _supplierRepository.HasPurchaseOrdersAsync(request.Id, cancellationToken);
        if (hasOrders)
            return Result.Failure(Error.Validation("Supplier",
                "Impossible de supprimer un fournisseur ayant des bons de commande associés. Désactivez-le à la place."));

        var supplierName = supplier.Name;

        await _supplierRepository.DeleteAsync(supplier, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Supplier.Deleted,
            "Supplier",
            request.Id,
            newValues: new { Name = supplierName },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
