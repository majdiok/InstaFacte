using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

/// <summary>
/// Famille « fraude douce » : schémas inhabituels que les contrôles de conformité classiques
/// laissent passer parce que chaque pièce, prise isolément, est régulière.
///
/// <para><b>Aucune de ces règles n'affirme la fraude.</b> Elles isolent des configurations qui
/// méritent un regard — un contrôle interne défaillant produit exactement les mêmes signaux qu'une
/// malversation. La détection est <b>statistique et reproductible</b> : deux exécutions sur les
/// mêmes données donnent le même résultat, condition pour qu'une observation tienne devant un
/// auditeur. Le vocabulaire des libellés reste factuel, jamais accusatoire.</para>
/// </summary>
internal static class SoftFraudRuleConstants
{
    internal const int MaxDetailLines = 200;

    /// <summary>
    /// Décalage horaire de la Tunisie par rapport à UTC. Les horodatages techniques
    /// (<c>Entity.CreatedAt</c>) sont en UTC ; les raisonner en heure locale est indispensable pour
    /// parler d'« heures ouvrées ».
    /// </summary>
    internal const int TunisiaUtcOffsetHours = 1;

    internal static DateTime ToLocal(DateTime utc) => utc.AddHours(TunisiaUtcOffsetHours);
}

/// <summary>
/// Concentration anormale de montants juste sous un seuil réglementaire.
///
/// <para>Fractionner une dépense pour rester sous le seuil de retenue à la source est la forme la
/// plus banale de contournement. Une facture isolée sous le seuil n'a rien d'anormal — c'est la
/// <b>répétition</b> chez un même fournisseur qui interpelle. La règle compte donc les factures
/// tombant dans une bande étroite sous le seuil et ne signale qu'au-delà d'un nombre d'occurrences.</para>
///
/// <para>Le seuil vient de l'exercice, jamais du code. Sans paramètre, la règle s'abstient.</para>
/// </summary>
public sealed class ThresholdStructuringAuditRule : AccountingAuditRuleBase
{
    /// <summary>Largeur de la bande sous le seuil, en pourcentage. 10 % = [900, 1000[ pour un seuil de 1000.</summary>
    private const decimal BandPercent = 10m;

    /// <summary>Occurrences à partir desquelles la répétition cesse d'être fortuite. Réglable par dossier.</summary>
    private const int DefaultMinOccurrences = 3;

    public override string Code => "threshold-structuring";
    public override string ModuleCode => "audit-trail";
    public override int Category => (int)AnomalyCategory.Achats;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var minOccurrences = ctx.GetRuleSetting(Code)?.IntThreshold ?? DefaultMinOccurrences;
        if (minOccurrences < 2) minOccurrences = DefaultMinOccurrences;

        var threshold = await c.Db.WithholdingFiscalYearParameters.AsNoTracking()
            .Where(p => p.FiscalYear == ctx.FiscalYear)
            .Select(p => (decimal?)p.Rs7TtcThresholdTnd)
            .FirstOrDefaultAsync(cancellationToken);

        // Sans seuil de référence, il n'y a pas de bande à observer.
        if (threshold is not { } thresholdTnd || thresholdTnd <= 0) return Array.Empty<AnomalyCandidate>();

        var lowerBound = MillimeRounding.Round(thresholdTnd * (100m - BandPercent) / 100m);

        var inBand = await c.Db.SupplierInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == ctx.FiscalYear
                        && i.Status != SupplierInvoiceStatus.Cancelled
                        && i.TotalAmount.Amount >= lowerBound
                        && i.TotalAmount.Amount < thresholdTnd)
            .Select(i => new
            {
                i.Id,
                i.SupplierId,
                i.InvoiceNumber,
                i.InvoiceDate,
                Total = i.TotalAmount.Amount
            })
            .ToListAsync(cancellationToken);

        var clusters = inBand
            .GroupBy(i => i.SupplierId)
            .Where(g => g.Count() >= minOccurrences)
            .ToList();

        if (clusters.Count == 0) return Array.Empty<AnomalyCandidate>();

        var supplierNames = await c.Db.Suppliers.AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return clusters.Select(cluster =>
        {
            var items = cluster.OrderBy(i => i.InvoiceDate).ToList();
            var name = supplierNames.GetValueOrDefault(cluster.Key, "Fournisseur");

            return SingleGroup(
                Code, ModuleCode, Category, DefaultSeverity,
                "Montants répétés juste sous le seuil",
                $"{name} : {items.Count} facture(s) comprises entre {lowerBound:N3} et " +
                $"{thresholdTnd:N3} TND, soit juste sous le seuil de retenue à la source.",
                "Fractionnement possible pour rester sous le seuil : à documenter.",
                accountRef: "4011",
                amount: MillimeRounding.Round(items.Sum(i => i.Total)),
                periodFrom: DateOnly.FromDateTime(items[0].InvoiceDate),
                periodTo: DateOnly.FromDateTime(items[^1].InvoiceDate),
                lines: items.Take(SoftFraudRuleConstants.MaxDetailLines).Select(i => new AnomalyLineCandidate(
                    null, null, i.InvoiceDate, "4011", name, i.Total, 0, i.InvoiceNumber, null)).ToList(),
                recommendations:
                [
                    "Vérifier si ces factures couvrent une même prestation fractionnée.",
                    "Documenter la justification commerciale du découpage."
                ],
                deepLinkRoute: "/supplier-invoices",
                discriminator: cluster.Key.ToString("N"));
        }).ToList();
    }
}

/// <summary>
/// Fournisseur créé et réglé dans la même journée.
///
/// <para>Le délai entre la création d'un tiers et son premier règlement est normalement de
/// plusieurs jours : commande, réception, facture, échéance. Un règlement le jour même court-circuite
/// tout contrôle intermédiaire. C'est le schéma classique du fournisseur fictif — et aussi celui
/// d'un achat urgent parfaitement légitime, d'où une sévérité d'avertissement.</para>
/// </summary>
public sealed class SupplierCreatedThenPaidAuditRule : AccountingAuditRuleBase
{
    /// <summary>Délai en dessous duquel le rapprochement est signalé, en jours. Réglable par dossier.</summary>
    private const int DefaultMaxDelayDays = 1;

    public override string Code => "supplier-created-then-paid";
    public override string ModuleCode => "audit-trail";
    public override int Category => (int)AnomalyCategory.Achats;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var maxDelayDays = ctx.GetRuleSetting(Code)?.IntThreshold ?? DefaultMaxDelayDays;
        if (maxDelayDays < 0) maxDelayDays = DefaultMaxDelayDays;

        // Premier règlement par fournisseur, via la facture qui le porte.
        var payments = await c.Db.SupplierPayments.AsNoTracking()
            .Join(c.Db.SupplierInvoices.AsNoTracking(),
                p => p.SupplierInvoiceId, i => i.Id, (p, i) => new
                {
                    i.SupplierId,
                    p.PaymentDate,
                    Amount = p.Amount.Amount,
                    i.InvoiceNumber
                })
            .Where(x => x.PaymentDate.Year == ctx.FiscalYear)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0) return Array.Empty<AnomalyCandidate>();

        var firstPayments = payments
            .GroupBy(p => p.SupplierId)
            .Select(g => g.OrderBy(p => p.PaymentDate).First())
            .ToList();

        var supplierIds = firstPayments.Select(p => p.SupplierId).ToList();

        var suppliers = await c.Db.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.CreatedAt })
            .ToListAsync(cancellationToken);

        var suspicious = (
            from payment in firstPayments
            join supplier in suppliers on payment.SupplierId equals supplier.Id
            let delay = (payment.PaymentDate.Date - supplier.CreatedAt.Date).Days
            where delay >= 0 && delay <= maxDelayDays
            select new
            {
                supplier.Name,
                supplier.CreatedAt,
                payment.PaymentDate,
                payment.Amount,
                payment.InvoiceNumber,
                Delay = delay
            }).ToList();

        if (suspicious.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Fournisseur créé puis réglé aussitôt",
                $"{suspicious.Count} fournisseur(s) ont reçu leur premier règlement dans les " +
                $"{maxDelayDays} jour(s) suivant leur création.",
                "Aucun contrôle intermédiaire n'a pu s'exercer entre la création et le paiement.",
                accountRef: "4011",
                amount: MillimeRounding.Round(suspicious.Sum(s => s.Amount)),
                periodFrom: null,
                periodTo: null,
                lines: suspicious.Take(SoftFraudRuleConstants.MaxDetailLines).Select(s => new AnomalyLineCandidate(
                    null, null, s.PaymentDate, "4011",
                    $"{s.Name} — créé le {s.CreatedAt:dd/MM/yyyy}, réglé le {s.PaymentDate:dd/MM/yyyy}",
                    s.Amount, 0, s.InvoiceNumber, null)).ToList(),
                recommendations:
                [
                    "Vérifier l'existence réelle du fournisseur et la réalité de la prestation.",
                    "Contrôler le RIB de règlement et la pièce justificative."
                ],
                deepLinkRoute: "/suppliers")
        ];
    }
}

/// <summary>
/// Écriture validée par son propre auteur.
///
/// <para>La séparation des tâches veut que celui qui saisit ne soit pas celui qui valide. Quand les
/// deux se confondent, aucun regard extérieur ne s'est posé sur l'écriture.</para>
///
/// <para><b>Les écritures automatiques sont exclues</b> : elles sont créées et validées par le
/// système, la confusion y est structurelle et sans signification. Ne pas les exclure ferait
/// remonter la quasi-totalité du journal.</para>
/// </summary>
public sealed class SelfValidatedEntryAuditRule : AccountingAuditRuleBase
{
    public override string Code => "self-validation";
    public override string ModuleCode => "audit-trail";
    public override int Category => (int)AnomalyCategory.Integrite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var entries = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear
                        && !e.IsAutoGenerated
                        && e.ValidatedBy != null
                        && e.CreatedBy != null
                        && e.ValidatedBy == e.CreatedBy)
            .Select(e => new
            {
                e.Id,
                e.EntryDate,
                e.JournalCode,
                e.EntryNumber,
                e.Label,
                e.ValidatedBy
            })
            .Take(SoftFraudRuleConstants.MaxDetailLines)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écriture validée par son auteur",
                $"{entries.Count} écriture(s) manuelle(s) ont été validées par la personne qui les a saisies.",
                "Séparation des tâches non respectée : aucun contrôle indépendant.",
                accountRef: null,
                amount: 0m,
                periodFrom: null,
                periodTo: null,
                lines: entries.Select(e => new AnomalyLineCandidate(
                    e.Id, null, e.EntryDate, null,
                    $"{e.Label} — saisie et validée par {e.ValidatedBy}", 0, 0,
                    $"{e.JournalCode}-{e.EntryNumber}", null)).ToList(),
                recommendations:
                [
                    "Confier la validation à un utilisateur distinct du saisisseur.",
                    "Revoir les habilitations de validation comptable."
                ],
                deepLinkRoute: "/accounting/entry-search")
        ];
    }
}

/// <summary>
/// Écriture saisie hors des heures ouvrées.
///
/// <para>Une saisie nocturne ou dominicale n'est pas fautive en soi — les clôtures se font souvent
/// tard. Elle mérite néanmoins d'être visible : c'est le moment où les contrôles humains sont
/// absents. Sévérité informative, volontairement : en faire un avertissement noierait le réviseur.</para>
///
/// <para>L'horodatage technique est en UTC ; il est ramené en heure de Tunisie avant d'être
/// interprété, sans quoi une saisie de 8 h du matin paraîtrait nocturne.</para>
/// </summary>
public sealed class OffHoursEntryAuditRule : AccountingAuditRuleBase
{
    private const int BusinessStartHour = 6;
    private const int BusinessEndHour = 21;

    public override string Code => "off-hours-entry";
    public override string ModuleCode => "audit-trail";
    public override int Category => (int)AnomalyCategory.Integrite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Info;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var entries = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear && !e.IsAutoGenerated)
            .Select(e => new
            {
                e.Id,
                e.EntryDate,
                e.JournalCode,
                e.EntryNumber,
                e.Label,
                e.CreatedAt,
                e.CreatedBy
            })
            .ToListAsync(cancellationToken);

        var offHours = entries
            .Select(e => new { Entry = e, Local = SoftFraudRuleConstants.ToLocal(e.CreatedAt) })
            .Where(x => x.Local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                        || x.Local.Hour < BusinessStartHour
                        || x.Local.Hour >= BusinessEndHour)
            .Take(SoftFraudRuleConstants.MaxDetailLines)
            .ToList();

        if (offHours.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Saisie hors heures ouvrées",
                $"{offHours.Count} écriture(s) manuelle(s) saisies la nuit ou le week-end.",
                "Moment où aucun contrôle humain ne s'exerce : à corréler avec les autres signaux.",
                accountRef: null,
                amount: 0m,
                periodFrom: null,
                periodTo: null,
                lines: offHours.Select(x => new AnomalyLineCandidate(
                    x.Entry.Id, null, x.Entry.EntryDate, null,
                    $"{x.Entry.Label} — saisie le {x.Local:dddd dd/MM/yyyy à HH'h'mm}" +
                    (x.Entry.CreatedBy is null ? "" : $" par {x.Entry.CreatedBy}"),
                    0, 0, $"{x.Entry.JournalCode}-{x.Entry.EntryNumber}", null)).ToList(),
                recommendations:
                [
                    "Recouper avec les autres signaux du dossier avant toute conclusion.",
                    "Documenter les périodes de clôture qui justifient ces horaires."
                ],
                deepLinkRoute: "/accounting/entry-search")
        ];
    }
}

/// <summary>
/// Écriture antidatée.
///
/// <para>Un décalage important entre la date d'opération et la date de saisie signale soit une
/// régularisation tardive, soit une reconstitution après coup. Les deux méritent d'être
/// documentées, particulièrement si l'écriture porte sur une période déjà déclarée.</para>
///
/// <para>Le seuil de jours est réglable par dossier : un cabinet qui reprend une comptabilité en
/// retard signalerait sinon l'intégralité de sa reprise.</para>
/// </summary>
public sealed class BackdatedEntryAuditRule : AccountingAuditRuleBase
{
    private const int DefaultThresholdDays = 60;

    public override string Code => "backdated-entry";
    public override string ModuleCode => "audit-trail";
    public override int Category => (int)AnomalyCategory.Ecritures;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var thresholdDays = ctx.GetRuleSetting(Code)?.IntThreshold ?? DefaultThresholdDays;
        if (thresholdDays <= 0) thresholdDays = DefaultThresholdDays;

        var entries = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear && !e.IsAutoGenerated)
            .Select(e => new
            {
                e.Id,
                e.EntryDate,
                e.JournalCode,
                e.EntryNumber,
                e.Label,
                e.CreatedAt,
                e.CreatedBy
            })
            .ToListAsync(cancellationToken);

        var backdated = entries
            .Select(e => new { Entry = e, Gap = (e.CreatedAt.Date - e.EntryDate.Date).Days })
            .Where(x => x.Gap > thresholdDays)
            .OrderByDescending(x => x.Gap)
            .Take(SoftFraudRuleConstants.MaxDetailLines)
            .ToList();

        if (backdated.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écritures antidatées",
                $"{backdated.Count} écriture(s) saisies plus de {thresholdDays} jours après leur date " +
                "d'opération.",
                "Régularisation tardive ou reconstitution : impact possible sur des périodes déjà déclarées.",
                accountRef: null,
                amount: 0m,
                periodFrom: null,
                periodTo: null,
                lines: backdated.Select(x => new AnomalyLineCandidate(
                    x.Entry.Id, null, x.Entry.EntryDate, null,
                    $"{x.Entry.Label} — opération du {x.Entry.EntryDate:dd/MM/yyyy}, saisie le " +
                    $"{x.Entry.CreatedAt:dd/MM/yyyy} ({x.Gap} jours)",
                    0, 0, $"{x.Entry.JournalCode}-{x.Entry.EntryNumber}", null)).ToList(),
                recommendations:
                [
                    "Documenter le motif de la régularisation tardive.",
                    "Vérifier si la période concernée a déjà fait l'objet d'une déclaration."
                ],
                deepLinkRoute: "/accounting/entry-search")
        ];
    }
}
