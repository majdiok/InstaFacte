using CashRegisterEntity = FactuTrust.Domain.Entities.CashRegister;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.CashRegister;

internal static class CashRegisterResolver
{
    public static async Task<Result<CashRegisterEntity>> ResolveAsync(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        Guid warehouseId,
        Guid? cashRegisterId,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (cashRegisterId is { } id && id != Guid.Empty)
        {
            var register = await registers.GetByIdAsync(id, cancellationToken);
            if (register is null || register.WarehouseId != warehouseId)
                return Result.Failure<CashRegisterEntity>(Error.NotFound("CashRegister", id));
            if (!register.IsActive)
                return Result.Failure<CashRegisterEntity>(
                    Error.Validation("CashRegister", "Cette caisse est désactivée"));
            return Result.Success(register);
        }

        return await CashRegisterProvisioning.EnsureForWarehouseAsync(
            warehouses, registers, warehouseId, userId, cancellationToken);
    }
}
