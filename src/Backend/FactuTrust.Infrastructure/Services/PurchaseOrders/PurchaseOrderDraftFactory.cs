using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.PurchaseOrders;

/// <summary>
/// Default implementation of <see cref="IPurchaseOrderDraftFactory"/>. Pure **builder**:
/// constructs <see cref="PurchaseOrder"/> aggregates (Draft status) ready to be attached
/// to the caller's <c>DbContext</c>. It does NOT persist anything itself — that is the
/// caller's responsibility, so the whole batch (POs + recommendation status transitions
/// + audit rows) can be saved atomically with one <c>SaveChangesAsync</c>.
/// </summary>
public sealed class PurchaseOrderDraftFactory : IPurchaseOrderDraftFactory
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ILogger<PurchaseOrderDraftFactory> _logger;

    public PurchaseOrderDraftFactory(
        IPurchaseOrderRepository purchaseOrderRepository,
        ILogger<PurchaseOrderDraftFactory> logger)
    {
        // Repository is used only to seed the next PO number — a read-only lookup whose own
        // DbContext lifetime is fine to share with a side connection.
        _purchaseOrderRepository = purchaseOrderRepository;
        _logger = logger;
    }

    public async Task<PurchaseOrderDraftBatchResult> BuildDraftPurchaseOrdersAsync(
        IReadOnlyList<ReplenishmentRecommendation> recommendations,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, Supplier> suppliers,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (recommendations is null || recommendations.Count == 0)
            return new PurchaseOrderDraftBatchResult(
                Array.Empty<BuiltDraftPurchaseOrder>(), Array.Empty<string>(), Array.Empty<Guid>());

        var warnings = new List<string>();
        var drafts = new List<BuiltDraftPurchaseOrder>();
        // Recommendations that end up attached to no draft PO (skip reasons below).
        var unlinked = new List<Guid>();

        // Partition recommendations by effective supplier (manual override > preferred).
        var bySupplier = recommendations
            .GroupBy(r => r.GetEffectiveSupplierId())
            .ToList();

        var year = DateTime.UtcNow.Year;
        var latestNumberStr = await _purchaseOrderRepository.GetLatestNumberAsync(year, ct);
        var nextNumber = ParseOrInit(latestNumberStr, year);

        foreach (var group in bySupplier)
        {
            var supplierId = group.Key;
            if (supplierId is null)
            {
                foreach (var rec in group)
                    warnings.Add($"Recommandation {rec.Id} non liée : aucun fournisseur (préféré ou manuel) résolu.");
                unlinked.AddRange(group.Select(r => r.Id));
                continue;
            }

            if (!suppliers.TryGetValue(supplierId.Value, out var supplier))
            {
                foreach (var rec in group)
                    warnings.Add($"Recommandation {rec.Id} non liée : fournisseur {supplierId} introuvable.");
                unlinked.AddRange(group.Select(r => r.Id));
                continue;
            }

            if (!supplier.IsActive)
            {
                foreach (var rec in group)
                    warnings.Add($"Recommandation {rec.Id} non liée : fournisseur {supplier.Name} désactivé.");
                unlinked.AddRange(group.Select(r => r.Id));
                continue;
            }

            var poResult = PurchaseOrder.Create(
                nextNumber,
                supplier,
                orderDate: DateTime.UtcNow.Date,
                expectedDeliveryDate: null,
                reference: null,
                notes: "Brouillon créé automatiquement depuis le module Prévisions IA — Réapprovisionnement V2.",
                warehouseId: null);

            if (poResult.IsFailure)
            {
                foreach (var rec in group)
                    warnings.Add($"Recommandation {rec.Id} non liée : échec création BC ({poResult.Error.Description}).");
                unlinked.AddRange(group.Select(r => r.Id));
                continue;
            }

            var po = poResult.Value;
            po.SetAuditInfo(actorUserId);

            var linkedIds = new List<Guid>();
            foreach (var rec in group)
            {
                if (!products.TryGetValue(rec.ProductId, out var product))
                {
                    warnings.Add($"Recommandation {rec.Id} non liée : produit {rec.ProductId} introuvable.");
                    unlinked.Add(rec.Id);
                    continue;
                }

                var qty = rec.GetEffectiveOrderQty();
                if (qty <= 0)
                {
                    warnings.Add($"Recommandation {rec.Id} non liée : quantité effective <= 0.");
                    unlinked.Add(rec.Id);
                    continue;
                }

                var addLine = po.AddLine(product, qty);
                if (addLine.IsFailure)
                {
                    warnings.Add($"Recommandation {rec.Id} non liée : {addLine.Error.Description}.");
                    unlinked.Add(rec.Id);
                    continue;
                }

                linkedIds.Add(rec.Id);
            }

            if (linkedIds.Count == 0)
            {
                _logger.LogWarning("PO draft for supplier {Supplier} skipped: no usable line.", supplier.Name);
                continue;
            }

            drafts.Add(new BuiltDraftPurchaseOrder(PurchaseOrder: po, RecommendationIds: linkedIds));
            nextNumber = nextNumber.Next();
        }

        _logger.LogInformation(
            "PO drafts built (not yet persisted): {Count} draft(s), {Linked} recommendation(s) targeted, {Unlinked} unlinked, {WarningCount} warning(s).",
            drafts.Count,
            drafts.Sum(d => d.RecommendationIds.Count),
            unlinked.Count,
            warnings.Count);

        return new PurchaseOrderDraftBatchResult(drafts, warnings, unlinked);
    }

    private static PurchaseOrderNumber ParseOrInit(string? latest, int year)
    {
        if (string.IsNullOrWhiteSpace(latest))
            return PurchaseOrderNumber.Create("BC", year, 1);

        var parsed = PurchaseOrderNumber.Parse(latest);
        return parsed.IsSuccess ? parsed.Value.Next() : PurchaseOrderNumber.Create("BC", year, 1);
    }
}
