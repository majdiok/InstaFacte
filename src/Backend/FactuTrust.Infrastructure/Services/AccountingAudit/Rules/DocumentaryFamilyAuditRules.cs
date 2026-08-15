using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

/// <summary>
/// Normalisation des numéros de facture fournisseur pour la détection de doublons.
///
/// <para>Un même document arrive sous des graphies différentes selon qu'il est saisi à la main,
/// importé ou re-saisi : « FA-00123 », « fa 123 », « FA000123 ». Comparer les chaînes brutes ne
/// détecterait aucun de ces doublons. On réduit donc à un canon : majuscules, alphanumériques
/// seuls, zéros de tête du bloc numérique final retirés.</para>
/// </summary>
internal static class SupplierInvoiceNumberNormalizer
{
    public static string Normalize(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return string.Empty;

        var kept = new StringBuilder(invoiceNumber.Length);
        foreach (var ch in invoiceNumber)
        {
            if (char.IsLetterOrDigit(ch))
                kept.Append(char.ToUpperInvariant(ch));
        }

        var canonical = kept.ToString();
        if (canonical.Length == 0) return string.Empty;

        // Retirer les zéros de tête du dernier bloc de chiffres : « FA000123 » ≡ « FA123 ».
        var digitsStart = canonical.Length;
        while (digitsStart > 0 && char.IsDigit(canonical[digitsStart - 1]))
            digitsStart--;

        if (digitsStart == canonical.Length) return canonical;

        var prefix = canonical[..digitsStart];
        var digits = canonical[digitsStart..].TrimStart('0');
        return prefix + (digits.Length == 0 ? "0" : digits);
    }
}

/// <summary>
/// Facture fournisseur sans aucune pièce justificative.
///
/// <para>La pièce peut être attachée à deux endroits selon le chemin de saisie : sur le reçu
/// d'achat d'origine, ou sur l'écriture comptable générée. La règle considère la facture justifiée
/// dès que l'un des deux porte un fichier — signaler une facture dont la pièce existe ailleurs
/// ferait perdre confiance dans tout le contrôle.</para>
///
/// <para>Sévérité bloquante : sans pièce, la TVA n'est pas déductible et la charge est rejetable
/// en contrôle fiscal.</para>
/// </summary>
public sealed class SupplierInvoiceWithoutProofAuditRule : AccountingAuditRuleBase
{
    private const int MaxDetailLines = 200;

    public override string Code => "supplier-invoice-no-proof";
    public override string ModuleCode => "documents";
    public override int Category => (int)AnomalyCategory.Documents;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var invoices = await c.Db.SupplierInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == ctx.FiscalYear
                        && i.Status != SupplierInvoiceStatus.Cancelled)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.SupplierId,
                i.SourcePurchaseReceiptId,
                Total = i.TotalAmount.Amount
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0) return Array.Empty<AnomalyCandidate>();

        // Pièces portées par le reçu d'achat.
        var receiptIds = invoices.Where(i => i.SourcePurchaseReceiptId != null)
            .Select(i => i.SourcePurchaseReceiptId!.Value).Distinct().ToList();
        var receiptsWithProof = receiptIds.Count == 0
            ? new HashSet<Guid>()
            : (await c.Db.PurchaseReceiptAttachments.AsNoTracking()
                .Where(a => receiptIds.Contains(a.PurchaseReceiptId))
                .Select(a => a.PurchaseReceiptId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        // Pièces portées par l'écriture comptable de la facture.
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var entryByInvoice = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceEntityType == AccountingService.SourceSupplierInvoice
                        && e.SourceEntityId != null
                        && invoiceIds.Contains(e.SourceEntityId.Value))
            .Select(e => new { e.Id, InvoiceId = e.SourceEntityId!.Value })
            .ToListAsync(cancellationToken);

        var entryIds = entryByInvoice.Select(e => e.Id).ToList();
        var entriesWithProof = entryIds.Count == 0
            ? new HashSet<Guid>()
            : (await c.Db.JournalEntryAttachments.AsNoTracking()
                .Where(a => entryIds.Contains(a.JournalEntryId))
                .Select(a => a.JournalEntryId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var provenInvoiceIds = entryByInvoice
            .Where(e => entriesWithProof.Contains(e.Id))
            .Select(e => e.InvoiceId)
            .ToHashSet();

        var suppliers = await c.Db.Suppliers.AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var missing = invoices
            .Where(i => !provenInvoiceIds.Contains(i.Id)
                        && !(i.SourcePurchaseReceiptId is { } r && receiptsWithProof.Contains(r)))
            .ToList();

        if (missing.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Factures fournisseur sans pièce justificative",
                $"{missing.Count} facture(s) fournisseur sans document attaché, ni sur le reçu d'achat " +
                "ni sur l'écriture comptable.",
                "TVA non déductible et charge rejetable en contrôle fiscal.",
                accountRef: "4011",
                amount: MillimeRounding.Round(missing.Sum(i => i.Total)),
                periodFrom: null,
                periodTo: null,
                lines: missing.Take(MaxDetailLines).Select(i => new AnomalyLineCandidate(
                    null, null, i.InvoiceDate, "4011",
                    suppliers.TryGetValue(i.SupplierId, out var name) ? name : "Fournisseur",
                    i.Total, 0, i.InvoiceNumber, "Absente")).ToList(),
                recommendations:
                [
                    "Réclamer la facture au fournisseur.",
                    "Attacher le document scanné à l'écriture ou au reçu d'achat."
                ],
                deepLinkRoute: "/supplier-invoices")
        ];
    }
}

/// <summary>
/// Doublons de facture fournisseur.
///
/// <para>Deux détections distinctes, volontairement séparées car elles n'appellent pas la même
/// action. Le doublon <b>strict</b> — même fournisseur, même numéro normalisé — est une double
/// saisie certaine. Le doublon <b>approché</b> — même fournisseur, même montant au millime, moins
/// de cinq jours d'écart, numéros différents — est un soupçon : facture re-saisie sous un autre
/// numéro, ou double règlement d'une même prestation. Le second demande une vérification humaine,
/// d'où une sévérité moindre.</para>
/// </summary>
public sealed class SupplierInvoiceDuplicateAuditRule : AccountingAuditRuleBase
{
    /// <summary>Fenêtre du doublon approché, en jours.</summary>
    private const int NearDuplicateWindowDays = 5;

    private const decimal MillimeTolerance = 0.001m;

    public override string Code => "supplier-invoice-duplicate";
    public override string ModuleCode => "purchases";
    public override int Category => (int)AnomalyCategory.Achats;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var invoices = await c.Db.SupplierInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == ctx.FiscalYear
                        && i.Status != SupplierInvoiceStatus.Cancelled)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.SupplierId,
                Total = i.TotalAmount.Amount
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count < 2) return Array.Empty<AnomalyCandidate>();

        var suppliers = await c.Db.Suppliers.AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        string SupplierName(Guid id) => suppliers.TryGetValue(id, out var n) ? n : "Fournisseur inconnu";

        var results = new List<AnomalyCandidate>();

        // ── Doublon strict ────────────────────────────────────────────────────────────────
        var strictGroups = invoices
            .Select(i => new { Invoice = i, Key = SupplierInvoiceNumberNormalizer.Normalize(i.InvoiceNumber) })
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => (x.Invoice.SupplierId, x.Key))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in strictGroups)
        {
            var items = group.Select(x => x.Invoice).OrderBy(i => i.InvoiceDate).ToList();
            results.Add(SingleGroup(
                Code, ModuleCode, Category, DefaultSeverity,
                "Facture fournisseur saisie en double",
                $"{SupplierName(group.Key.SupplierId)} — numéro {items[0].InvoiceNumber} enregistré " +
                $"{items.Count} fois.",
                "Charge et TVA déductible comptées plusieurs fois ; risque de double règlement.",
                accountRef: "4011",
                amount: MillimeRounding.Round(items.Skip(1).Sum(i => i.Total)),
                periodFrom: DateOnly.FromDateTime(items[0].InvoiceDate),
                periodTo: DateOnly.FromDateTime(items[^1].InvoiceDate),
                lines: items.Select(i => new AnomalyLineCandidate(
                    null, null, i.InvoiceDate, "4011", SupplierName(i.SupplierId),
                    i.Total, 0, i.InvoiceNumber, null)).ToList(),
                recommendations:
                [
                    "Annuler la saisie en double.",
                    "Vérifier qu'aucun règlement n'a été émis pour la facture surnuméraire."
                ],
                deepLinkRoute: "/supplier-invoices",
                discriminator: $"strict|{group.Key.SupplierId:N}|{group.Key.Key}"));
        }

        // Les factures déjà signalées en doublon strict ne sont pas rejouées en doublon approché.
        var strictlyReported = strictGroups.SelectMany(g => g.Select(x => x.Invoice.Id)).ToHashSet();

        // ── Doublon approché ──────────────────────────────────────────────────────────────
        foreach (var bySupplier in invoices.Where(i => !strictlyReported.Contains(i.Id)).GroupBy(i => i.SupplierId))
        {
            var ordered = bySupplier.OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var left = ordered[i];
                    var right = ordered[j];

                    var dayGap = (right.InvoiceDate.Date - left.InvoiceDate.Date).TotalDays;
                    // La liste est triée par date : au-delà de la fenêtre, les suivantes le sont aussi.
                    if (dayGap > NearDuplicateWindowDays) break;

                    if (Math.Abs(left.Total - right.Total) > MillimeTolerance) continue;

                    var leftKey = SupplierInvoiceNumberNormalizer.Normalize(left.InvoiceNumber);
                    var rightKey = SupplierInvoiceNumberNormalizer.Normalize(right.InvoiceNumber);
                    if (string.Equals(leftKey, rightKey, StringComparison.Ordinal)) continue;

                    // Paire ordonnée par identifiant : l'empreinte ne dépend pas de l'ordre de lecture.
                    var pair = left.Id.CompareTo(right.Id) <= 0
                        ? (First: left, Second: right)
                        : (First: right, Second: left);

                    results.Add(SingleGroup(
                        Code, ModuleCode, Category, (int)PreClosingSeverity.Warning,
                        "Doublon probable de facture fournisseur",
                        $"{SupplierName(bySupplier.Key)} — {left.InvoiceNumber} et {right.InvoiceNumber} : " +
                        $"même montant ({left.Total:N3} TND) à {dayGap:0} jour(s) d'intervalle.",
                        "Double comptabilisation possible d'une même prestation.",
                        accountRef: "4011",
                        amount: MillimeRounding.Round(right.Total),
                        periodFrom: DateOnly.FromDateTime(left.InvoiceDate),
                        periodTo: DateOnly.FromDateTime(right.InvoiceDate),
                        lines:
                        [
                            new AnomalyLineCandidate(null, null, left.InvoiceDate, "4011",
                                SupplierName(left.SupplierId), left.Total, 0, left.InvoiceNumber, null),
                            new AnomalyLineCandidate(null, null, right.InvoiceDate, "4011",
                                SupplierName(right.SupplierId), right.Total, 0, right.InvoiceNumber, null)
                        ],
                        recommendations:
                        [
                            "Comparer les deux pièces justificatives.",
                            "Annuler la saisie surnuméraire si les prestations sont identiques."
                        ],
                        deepLinkRoute: "/supplier-invoices",
                        discriminator: $"near|{pair.First.Id:N}|{pair.Second.Id:N}"));
                }
            }
        }

        return results;
    }
}

/// <summary>
/// Dérive du prix facturé par rapport au prix commandé.
///
/// <para>Compare le prix unitaire de chaque ligne de facture fournisseur à celui de la ligne de bon
/// de commande correspondante. Le rattachement direct <c>PurchaseOrderLineId</c> est utilisé quand
/// il existe ; à défaut, on prend la dernière commande du couple (fournisseur, article) antérieure
/// à la facture. Sans référence de commande, la ligne est ignorée : mieux vaut ne rien dire que
/// comparer à un prix arbitraire.</para>
///
/// <para>Seuil réglable par tenant via <c>AccountingControlRuleSetting.DecimalThreshold</c>
/// (en pourcentage). Défaut : 10 %.</para>
/// </summary>
public sealed class PurchasePriceDriftAuditRule : AccountingAuditRuleBase
{
    private const decimal DefaultThresholdPercent = 10m;
    private const int MaxDetailLines = 200;

    public override string Code => "purchase-price-drift";
    public override string ModuleCode => "purchases";
    public override int Category => (int)AnomalyCategory.Achats;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var thresholdPercent = ctx.GetRuleSetting(Code)?.DecimalThreshold ?? DefaultThresholdPercent;
        if (thresholdPercent <= 0) thresholdPercent = DefaultThresholdPercent;

        var invoiceLines = await c.Db.SupplierInvoiceLines.AsNoTracking()
            .Include(l => l.SupplierInvoice)
            .Where(l => l.SupplierInvoice!.InvoiceDate.Year == ctx.FiscalYear
                        && l.SupplierInvoice.Status != SupplierInvoiceStatus.Cancelled)
            .Select(l => new
            {
                l.Id,
                l.SupplierInvoiceId,
                l.SupplierInvoice!.InvoiceNumber,
                l.SupplierInvoice.InvoiceDate,
                l.SupplierInvoice.SupplierId,
                l.ProductId,
                l.ProductName,
                l.PurchaseOrderLineId,
                UnitPrice = l.UnitPrice.Amount
            })
            .ToListAsync(cancellationToken);

        if (invoiceLines.Count == 0) return Array.Empty<AnomalyCandidate>();

        // Référence 1 : la ligne de commande explicitement rattachée.
        var linkedIds = invoiceLines.Where(l => l.PurchaseOrderLineId != null)
            .Select(l => l.PurchaseOrderLineId!.Value).Distinct().ToList();

        var linkedPrices = linkedIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await c.Db.PurchaseOrderLines.AsNoTracking()
                .Where(l => linkedIds.Contains(l.Id))
                .Select(l => new { l.Id, Price = l.UnitPrice.Amount })
                .ToDictionaryAsync(x => x.Id, x => x.Price, cancellationToken);

        // Référence 2 : la dernière commande du couple (fournisseur, article).
        var orderHistory = await c.Db.PurchaseOrderLines.AsNoTracking()
            .Include(l => l.PurchaseOrder)
            .Select(l => new
            {
                l.PurchaseOrder!.SupplierId,
                l.ProductId,
                l.PurchaseOrder.OrderDate,
                Price = l.UnitPrice.Amount
            })
            .ToListAsync(cancellationToken);

        var suppliers = await c.Db.Suppliers.AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var drifted = new List<(string Supplier, string Product, string InvoiceNumber, DateTime Date,
            decimal Invoiced, decimal Ordered, decimal DriftPercent)>();

        foreach (var line in invoiceLines)
        {
            decimal? reference = null;

            if (line.PurchaseOrderLineId is { } linkId && linkedPrices.TryGetValue(linkId, out var linked))
            {
                reference = linked;
            }
            else
            {
                reference = orderHistory
                    .Where(o => o.SupplierId == line.SupplierId
                                && o.ProductId == line.ProductId
                                && o.OrderDate <= line.InvoiceDate)
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => (decimal?)o.Price)
                    .FirstOrDefault();
            }

            // Aucune commande de référence : on s'abstient plutôt que de comparer à rien.
            if (reference is not { } ordered || ordered <= 0) continue;

            var drift = Math.Abs(line.UnitPrice - ordered) / ordered * 100m;
            if (drift <= thresholdPercent) continue;

            drifted.Add((
                suppliers.TryGetValue(line.SupplierId, out var name) ? name : "Fournisseur",
                line.ProductName,
                line.InvoiceNumber,
                line.InvoiceDate,
                line.UnitPrice,
                ordered,
                MillimeRounding.Round(drift)));
        }

        if (drifted.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écart de prix entre facture et commande",
                $"{drifted.Count} ligne(s) facturée(s) à plus de {thresholdPercent:0.#} % d'écart " +
                "avec le prix du bon de commande.",
                "Surfacturation possible ; la marge d'achat s'érode sans validation.",
                accountRef: null,
                amount: MillimeRounding.Round(drifted.Sum(d => Math.Abs(d.Invoiced - d.Ordered))),
                periodFrom: null,
                periodTo: null,
                lines: drifted.Take(MaxDetailLines).Select(d => new AnomalyLineCandidate(
                    null, null, d.Date, null,
                    $"{d.Supplier} — {d.Product} : commandé {d.Ordered:N3}, facturé {d.Invoiced:N3} ({d.DriftPercent:0.#} %)",
                    d.Invoiced, d.Ordered, d.InvoiceNumber, null)).ToList(),
                recommendations:
                [
                    "Comparer la facture au bon de commande signé.",
                    "Demander un avoir au fournisseur si l'écart n'est pas justifié."
                ],
                deepLinkRoute: "/supplier-invoices")
        ];
    }
}
