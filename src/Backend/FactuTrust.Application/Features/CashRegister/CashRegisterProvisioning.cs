using CashRegisterEntity = FactuTrust.Domain.Entities.CashRegister;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.CashRegister;

internal static class CashRegisterProvisioning
{
    public static async Task<Result<CashRegisterEntity>> EnsureForWarehouseAsync(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        Guid warehouseId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var warehouse = await warehouses.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Failure<CashRegisterEntity>(Error.NotFound("Warehouse", warehouseId));
        if (!warehouse.IsActive)
            return Result.Failure<CashRegisterEntity>(
                Error.Validation("Warehouse", "Cet entrepôt est désactivé"));

        var existing = await registers.GetByWarehouseIdAsync(warehouseId, cancellationToken);
        if (existing is not null)
            return Result.Success(existing);

        var code = CashRegisterEntity.DefaultCodeForWarehouse(warehouse.Code);
        if (await registers.CodeExistsAsync(code, null, cancellationToken))
        {
            var suffix = warehouse.Id.ToString("N")[..4].ToUpperInvariant();
            code = CashRegisterEntity.DefaultCodeForWarehouse($"{warehouse.Code}{suffix}");
        }

        var created = CashRegisterEntity.Create(code, CashRegisterEntity.DefaultNameForWarehouse(warehouse.Name), warehouseId);
        if (created.IsFailure)
            return created;

        created.Value.SetAuditInfo(userId ?? "system");
        var saved = await registers.AddAsync(created.Value, cancellationToken);
        return Result.Success(saved);
    }
}
