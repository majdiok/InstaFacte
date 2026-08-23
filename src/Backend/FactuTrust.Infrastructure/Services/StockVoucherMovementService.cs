using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Validates or cancels a stock voucher and applies stock movements in a single transaction.
/// </summary>
public sealed class StockVoucherMovementService : IStockVoucherMovementService
{
    private const int MaxConcurrencyAttempts = 3;

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IStockMutationService _mutation;
    private readonly ILogger<StockVoucherMovementService> _logger;

    public StockVoucherMovementService(
        ITenantDbContextFactory contextFactory,
        IStockMutationService mutation,
        ILogger<StockVoucherMovementService> logger)
    {
        _contextFactory = contextFactory;
        _mutation = mutation;
        _logger = logger;
    }

    public Task<Result> ValidateAsync(Guid stockVoucherId, CancellationToken cancellationToken = default) =>
        ValidateAsync(stockVoucherId, null, cancellationToken);

    public Task<Result> ValidateAsync(
        Guid stockVoucherId,
        IReadOnlyList<StockVoucherLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            stockVoucherId,
            (ctx, id, ct) => ExecuteValidateAsync(ctx, id, lineAllocations, ct),
            cancellationToken);

    public Task<Result> CancelAsync(Guid stockVoucherId, string reason, CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(stockVoucherId, (ctx, id, ct) => ExecuteCancelAsync(ctx, id, reason, ct), cancellationToken);

    private async Task<Result> ExecuteWithRetryAsync(
        Guid stockVoucherId,
        Func<TenantDbContext, Guid, CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            try
            {
                await using var context = _contextFactory.CreateIsolatedContext();
                var strategy = context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => work(context, stockVoucherId, cancellationToken));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict on stock voucher {StockVoucherId}, attempt {Attempt}/{MaxAttempts}",
                    stockVoucherId, attempt, MaxConcurrencyAttempts);

                if (attempt == MaxConcurrencyAttempts)
                {
                    return Result.Failure(Error.Conflict(
                        "Les données de stock ont été modifiées entre-temps. Actualisez la page et réessayez."));
                }
            }
        }

        return Result.Failure(Error.Conflict(
            "Les données de stock ont été modifiées entre-temps. Actualisez la page et réessayez."));
    }

    private async Task<Result> ExecuteValidateAsync(
        TenantDbContext context,
        Guid stockVoucherId,
        IReadOnlyList<StockVoucherLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var voucher = await LoadVoucherAsync(context, stockVoucherId, cancellationToken);
            if (voucher is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.NotFound("StockVoucher", stockVoucherId));
            }

            if (voucher.Status == StockVoucherStatus.Validated)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }

            var warehouse = await context.Set<Warehouse>()
                .FirstOrDefaultAsync(w => w.Id == voucher.WarehouseId, cancellationToken);
            if (warehouse is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.NotFound("Warehouse", voucher.WarehouseId));
            }

            if (!warehouse.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.Validation("Warehouse", "L'entrepôt sélectionné n'est pas actif"));
            }

            var applyResult = await ApplyMovementsAsync(
                context, voucher, warehouse, isReversal: false, lineAllocations, cancellationToken);
            if (applyResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return applyResult;
            }

            var validateResult = voucher.MarkValidated();
            if (validateResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return validateResult;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Result> ExecuteCancelAsync(
        TenantDbContext context,
        Guid stockVoucherId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var voucher = await LoadVoucherAsync(context, stockVoucherId, cancellationToken);
            if (voucher is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.NotFound("StockVoucher", stockVoucherId));
            }

            if (voucher.Status == StockVoucherStatus.Cancelled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }

            var wasValidated = voucher.Status == StockVoucherStatus.Validated;
            if (wasValidated)
            {
                var warehouse = await context.Set<Warehouse>()
                    .FirstOrDefaultAsync(w => w.Id == voucher.WarehouseId, cancellationToken);
                if (warehouse is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("Warehouse", voucher.WarehouseId));
                }

                var reverseResult = await ApplyMovementsAsync(
                    context, voucher, warehouse, isReversal: true, lineAllocations: null, cancellationToken);
                if (reverseResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return reverseResult;
                }
            }

            var cancelResult = voucher.Cancel(reason);
            if (cancelResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return cancelResult;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<StockVoucher?> LoadVoucherAsync(
        TenantDbContext context,
        Guid id,
        CancellationToken cancellationToken) =>
        await context.StockVouchers
            .Include(v => v.Lines)
            .Include(v => v.Warehouse)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    private async Task<Result> ApplyMovementsAsync(
        TenantDbContext context,
        StockVoucher voucher,
        Warehouse warehouse,
        bool isReversal,
        IReadOnlyList<StockVoucherLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken)
    {
        var reference = isReversal ? voucher.StockReversalReference : voucher.StockMovementReference;
        var expectedType = isReversal
            ? (voucher.Kind == StockVoucherKind.Entry ? MovementType.Exit : MovementType.Entry)
            : voucher.Kind.ToMovementType();

        var alreadyApplied = await context.StockMovements
            .AsNoTracking()
            .AnyAsync(m => m.Reference == reference && m.Type == expectedType, cancellationToken);
        if (alreadyApplied)
            return Result.Success();

        var notes = isReversal
            ? $"Annulation {voucher.Kind.ToDisplayString()} {voucher.Number.Value}"
            : $"{voucher.Kind.ToDisplayString()} — {voucher.Reason.ToDisplayString()}";

        if (isReversal)
            return await ReverseFromOriginalMovementsAsync(context, voucher, warehouse, reference, notes, cancellationToken);

        var allocationsByLine = (lineAllocations ?? Array.Empty<StockVoucherLineAllocationsDto>())
            .ToDictionary(a => a.LineId, a => a.Allocations);

        foreach (var line in voucher.Lines.OrderBy(l => l.LineNumber))
        {
            if (line.Quantity <= 0)
                continue;

            var stockItem = await context.StockItems
                .Include(s => s.Movements)
                .FirstOrDefaultAsync(
                    s => s.ProductId == line.ProductId && s.WarehouseId == warehouse.Id,
                    cancellationToken);

            var applyEntry = voucher.Kind == StockVoucherKind.Entry;
            var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, cancellationToken);
            var store = new EfStockTraceabilityStore(context);
            var documentKind = voucher.Kind == StockVoucherKind.Entry
                ? StockDocumentKind.StockVoucherEntry
                : StockDocumentKind.StockVoucherIssue;
            var allocations = allocationsByLine.GetValueOrDefault(line.Id);

            if (applyEntry)
            {
                if (stockItem is null)
                {
                    var createResult = StockItem.Create(line.ProductId, warehouse.Id);
                    if (createResult.IsFailure)
                        return Result.Failure(createResult.Error);

                    stockItem = createResult.Value;
                    context.StockItems.Add(stockItem);
                }

                var movementIdsBefore = stockItem.Movements.Select(m => m.Id).ToHashSet();
                var entryResult = _mutation.Apply(stockItem, new StockMutationRequest
                {
                    ProductId = line.ProductId,
                    WarehouseId = warehouse.Id,
                    Kind = StockMutationKind.Entry,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    Reason = voucher.Reason,
                    Reference = reference,
                    Notes = notes,
                    DocumentLineId = line.Id,
                    DocumentKind = documentKind,
                    Allocations = allocations
                }, product, store);
                if (entryResult.IsFailure)
                    return entryResult;

                EnsureNewStockMovementsAreAdded(context, stockItem, movementIdsBefore);
            }
            else
            {
                if (stockItem is null)
                    return Result.Failure(Error.Validation("StockItem",
                        $"Aucun stock trouvé pour '{line.ProductName}' dans cet entrepôt"));

                var movementIdsBefore = stockItem.Movements.Select(m => m.Id).ToHashSet();
                var exitResult = _mutation.Apply(stockItem, new StockMutationRequest
                {
                    ProductId = line.ProductId,
                    WarehouseId = warehouse.Id,
                    Kind = StockMutationKind.Exit,
                    Quantity = line.Quantity,
                    Reason = voucher.Reason,
                    Reference = reference,
                    Notes = notes,
                    DocumentLineId = line.Id,
                    DocumentKind = documentKind,
                    Allocations = allocations
                }, product, store);
                if (exitResult.IsFailure)
                    return exitResult;

                EnsureNewStockMovementsAreAdded(context, stockItem, movementIdsBefore);
            }
        }

        return Result.Success();
    }

    private async Task<Result> ReverseFromOriginalMovementsAsync(
        TenantDbContext context,
        StockVoucher voucher,
        Warehouse warehouse,
        string reverseReference,
        string notes,
        CancellationToken cancellationToken)
    {
        var original = await context.StockMovements
            .Where(m => m.Reference == voucher.StockMovementReference)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

        if (original.Count == 0)
            return Result.Success();

        var store = new EfStockTraceabilityStore(context);

        foreach (var movement in original)
        {
            var stockItem = await context.StockItems
                .Include(s => s.Movements)
                .FirstOrDefaultAsync(s => s.Id == movement.StockItemId, cancellationToken);
            if (stockItem is null)
                return Result.Failure(Error.NotFound("StockItem", movement.StockItemId));

            var product = await context.Products.FirstOrDefaultAsync(p => p.Id == stockItem.ProductId, cancellationToken);
            var qty = Math.Abs(movement.Quantity);
            var allocations = movement.ProductLotId.HasValue || movement.SerialId.HasValue
                ? new[]
                {
                    new StockAllocationInput(
                        qty,
                        movement.ProductLotId,
                        SerialId: movement.SerialId,
                        UnitCost: movement.UnitCost)
                }
                : null;

            var kind = movement.Type == MovementType.Entry
                ? StockMutationKind.Exit
                : StockMutationKind.Entry;

            if (kind == StockMutationKind.Entry && stockItem.WarehouseId != warehouse.Id)
            {
                // keep warehouse of the voucher
            }

            var movementIdsBefore = stockItem.Movements.Select(m => m.Id).ToHashSet();
            var result = _mutation.Apply(stockItem, new StockMutationRequest
            {
                ProductId = stockItem.ProductId,
                WarehouseId = warehouse.Id,
                Kind = kind,
                Quantity = qty,
                UnitCost = movement.UnitCost,
                Reason = voucher.Reason,
                Reference = reverseReference,
                Notes = notes,
                Allocations = allocations
            }, product, store);
            if (result.IsFailure)
                return result;

            EnsureNewStockMovementsAreAdded(context, stockItem, movementIdsBefore);
        }

        return Result.Success();
    }

    private static void EnsureNewStockMovementsAreAdded(
        TenantDbContext context,
        StockItem item,
        IReadOnlySet<Guid> movementIdsBefore)
    {
        foreach (var movement in item.Movements)
        {
            if (movementIdsBefore.Contains(movement.Id))
                continue;

            var entry = context.Entry(movement);
            if (entry.State != EntityState.Added)
                entry.State = EntityState.Added;
        }
    }
}
