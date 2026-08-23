using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

/// <summary>
/// Retenue à la source omise sur une prestation qui y est soumise.
///
/// <para>La retenue est due par le <b>payeur</b> : l'omettre ne fait pas économiser, elle transfère
/// la dette sur l'entreprise, majorée de pénalités. Les honoraires, commissions, loyers et
/// prestations de non-résidents sont les cas les plus fréquemment oubliés.</para>
///
/// <para><b>Le seuil n'est jamais codé en dur.</b> Il est lu dans
/// <c>WithholdingFiscalYearParameter.Rs7TtcThresholdTnd</c> de l'exercice : la loi de finances le
/// modifie, et une valeur figée dans le code produirait des anomalies fausses l'année suivante.
/// Sans paramètre pour l'exercice, la règle <b>s'abstient</b> plutôt que de deviner.</para>
/// </summary>
public sealed class WithholdingMissingOnFeesAuditRule : AccountingAuditRuleBase
{
    private const int MaxDetailLines = 200;

    /// <summary>Catégories dont l'omission est la plus coûteuse et la plus fréquente.</summary>
    private static readonly WithholdingCategory[] AlwaysWithheld =
    [
        WithholdingCategory.Honoraires,
        WithholdingCategory.Commissions,
        WithholdingCategory.Loyers,
        WithholdingCategory.NonResidents
    ];

    public override string Code => "withholding-missing-on-fees";
    public override string ModuleCode => "fiscal";
    public override int Category => (int)AnomalyCategory.Fiscalite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        // Fournisseurs dont le type de retenue par défaut relève d'une catégorie toujours retenue.
        var withheldTypeIds = await c.Db.WithholdingTaxTypes.AsNoTracking()
            .Where(t => t.IsActive && AlwaysWithheld.Contains(t.Category))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (withheldTypeIds.Count == 0) return Array.Empty<AnomalyCandidate>();

        var suppliers = await c.Db.Suppliers.AsNoTracking()
            .Where(s => s.DefaultWithholdingTaxTypeId != null
                        && withheldTypeIds.Contains(s.DefaultWithholdingTaxTypeId.Value))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        if (suppliers.Count == 0) return Array.Empty<AnomalyCandidate>();

        var supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);
        var supplierIds = suppliers.Select(s => s.Id).ToList();

        // Seuil de l'exercice. Absent ⇒ abstention : mieux vaut ne rien dire que retenir un seuil
        // périmé et produire des anomalies fausses sur tout un exercice.
        var threshold = await c.Db.WithholdingFiscalYearParameters.AsNoTracking()
            .Where(p => p.FiscalYear == ctx.FiscalYear)
            .Select(p => (decimal?)p.Rs7TtcThresholdTnd)
            .FirstOrDefaultAsync(cancellationToken);

        if (threshold is not { } thresholdTnd) return Array.Empty<AnomalyCandidate>();

        var missing = await c.Db.SupplierInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == ctx.FiscalYear
                        && i.Status != SupplierInvoiceStatus.Cancelled
                        && supplierIds.Contains(i.SupplierId)
                        && i.TotalAmount.Amount >= thresholdTnd
                        && (!i.IsSubjectToWithholding
                            || i.WithholdingAmount == null
                            || i.WithholdingAmount == 0))
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.SupplierId,
                Total = i.TotalAmount.Amount
            })
            .Take(MaxDetailLines)
            .ToListAsync(cancellationToken);

        if (missing.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Retenue à la source omise",
                $"{missing.Count} facture(s) de prestations soumises à retenue, au-delà du seuil de " +
                $"{thresholdTnd:N3} TND TTC, sans retenue appliquée.",
                "La retenue non prélevée reste due par l'entreprise, majorée de pénalités.",
                accountRef: "4324",
                amount: MillimeRounding.Round(missing.Sum(i => i.Total)),
                periodFrom: null,
                periodTo: null,
                lines: missing.Select(i => new AnomalyLineCandidate(
                    null, null, i.InvoiceDate, "4324",
                    supplierNames.TryGetValue(i.SupplierId, out var name) ? name : "Fournisseur",
                    i.Total, 0, i.InvoiceNumber, null)).ToList(),
                recommendations:
                [
                    "Vérifier la nature de la prestation et le taux applicable.",
                    "Régulariser la retenue et éditer le certificat au fournisseur."
                ],
                deepLinkRoute: "/withholding-tax")
        ];
    }
}

/// <summary>
/// FODEC facturé mais non comptabilisé, ou l'inverse.
///
/// <para>Le FODEC est collecté pour le compte de l'État : une facture qui l'affiche sans le
/// comptabiliser au 43652 crée une dette invisible. La règle compare, facture par facture, le FODEC
/// du document à celui de son écriture.</para>
/// </summary>
public sealed class FodecMissingAuditRule : AccountingAuditRuleBase
{
    private const decimal MillimeTolerance = 0.001m;
    private const int MaxDetailLines = 200;

    public override string Code => "fodec-missing";
    public override string ModuleCode => "fiscal";
    public override int Category => (int)AnomalyCategory.Fiscalite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var invoices = await c.Db.Invoices.AsNoTracking()
            .Where(i => i.IssueDate.Year == ctx.FiscalYear
                        && i.CancelledAt == null
                        && i.FodecAmount.Amount > 0)
            .Select(i => new
            {
                i.Id,
                Number = i.Number.Value,
                i.IssueDate,
                Fodec = i.FodecAmount.Amount
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0) return Array.Empty<AnomalyCandidate>();

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        var bookedFodec = await c.Db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.SourceEntityType == AccountingService.SourceInvoice
                        && e.SourceEntityId != null
                        && invoiceIds.Contains(e.SourceEntityId.Value)
                        && e.Status != JournalEntryStatus.Brouillon)
            .Select(e => new
            {
                InvoiceId = e.SourceEntityId!.Value,
                Fodec = e.Lines
                    .Where(l => l.AccountNumber.StartsWith(TunisianPostingAccounts.Fodec))
                    .Sum(l => l.CreditAmount.Amount - l.DebitAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var bookedByInvoice = new Dictionary<Guid, decimal>();
        foreach (var row in bookedFodec)
            bookedByInvoice[row.InvoiceId] = bookedByInvoice.GetValueOrDefault(row.InvoiceId) + row.Fodec;

        var divergent = invoices
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.IssueDate,
                Expected = MillimeRounding.Round(i.Fodec),
                Booked = MillimeRounding.Round(bookedByInvoice.GetValueOrDefault(i.Id))
            })
            // Facture sans écriture du tout : c'est une anomalie d'intégrité, couverte ailleurs.
            .Where(x => bookedByInvoice.ContainsKey(x.Id)
                        && Math.Abs(x.Expected - x.Booked) > MillimeTolerance)
            .Take(MaxDetailLines)
            .ToList();

        if (divergent.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "FODEC facturé non comptabilisé",
                $"{divergent.Count} facture(s) portent du FODEC dont le montant ne se retrouve pas " +
                $"au compte {TunisianPostingAccounts.Fodec}.",
                "Dette FODEC envers l'État sous-évaluée dans les comptes.",
                accountRef: TunisianPostingAccounts.Fodec,
                amount: MillimeRounding.Round(divergent.Sum(x => Math.Abs(x.Expected - x.Booked))),
                periodFrom: null,
                periodTo: null,
                lines: divergent.Select(x => new AnomalyLineCandidate(
                    null, null, x.IssueDate, TunisianPostingAccounts.Fodec,
                    $"Facture {x.Number} : {x.Expected:N3} facturé, {x.Booked:N3} comptabilisé",
                    x.Expected, x.Booked, x.Number, null)).ToList(),
                recommendations:
                [
                    "Comptabiliser le FODEC au crédit du 43652.",
                    "Vérifier le paramétrage FODEC des articles concernés."
                ],
                deepLinkRoute: "/accounting/entry-search")
        ];
    }
}

/// <summary>
/// Période de TVA restée ouverte au-delà de son échéance.
///
/// <para>Complète la règle <c>vat</c>, qui ne détecte que l'absence totale de déclaration. Ici, la
/// déclaration existe mais reste en brouillon ou simplement soumise, bien après la date limite :
/// elle n'a donc jamais été verrouillée, et rien ne garantit que ce qui a été déposé correspond à
/// ce qui est en base.</para>
///
/// <para>L'échéance vient de l'échéancier fiscal quand il la porte, sinon du délai légal par défaut
/// — le 28 du mois suivant en Tunisie. Le nombre de jours de grâce est réglable par dossier via
/// <c>AccountingControlRuleSetting.IntThreshold</c>.</para>
/// </summary>
public sealed class VatPeriodNotClosedAuditRule : AccountingAuditRuleBase
{
    /// <summary>Jour du mois suivant où la déclaration mensuelle est due, à défaut d'échéancier.</summary>
    private const int DefaultDueDayOfNextMonth = 28;

    /// <summary>Jours de grâce après l'échéance avant de signaler. Réglable par dossier.</summary>
    private const int DefaultGraceDays = 15;

    public override string Code => "vat-period-not-closed";
    public override string ModuleCode => "vat";
    public override int Category => (int)AnomalyCategory.Tva;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var graceDays = ctx.GetRuleSetting(Code)?.IntThreshold ?? DefaultGraceDays;
        if (graceDays < 0) graceDays = DefaultGraceDays;

        var declarations = await c.Db.VatDeclarations.AsNoTracking()
            .Where(d => d.Year == ctx.FiscalYear && d.Status != VatDeclarationStatus.Locked)
            .Select(d => new { d.Id, d.Year, d.Month, d.Status, d.SubmittedAt })
            .ToListAsync(cancellationToken);

        if (declarations.Count == 0) return Array.Empty<AnomalyCandidate>();

        // Échéances portées par l'échéancier fiscal, quand il en existe pour le mois.
        var scheduled = await c.Db.FiscalScheduleEntries.AsNoTracking()
            .Where(e => e.FiscalYear == ctx.FiscalYear
                        && e.ObligationType == FiscalObligationType.MonthlyDeclaration
                        && e.PeriodMonth != null
                        && !e.IsCancelled)
            .Select(e => new { Month = e.PeriodMonth!.Value, e.DueDate })
            .ToListAsync(cancellationToken);

        var dueByMonth = new Dictionary<int, DateTime>();
        foreach (var row in scheduled)
            dueByMonth[row.Month] = row.DueDate;

        var today = DateTime.UtcNow.Date;
        var overdue = new List<(int Month, DateTime DueDate, VatDeclarationStatus Status)>();

        foreach (var declaration in declarations)
        {
            var dueDate = dueByMonth.TryGetValue(declaration.Month, out var scheduledDue)
                ? scheduledDue
                : NextMonthDueDate(declaration.Year, declaration.Month);

            if (today > dueDate.AddDays(graceDays))
                overdue.Add((declaration.Month, dueDate, declaration.Status));
        }

        if (overdue.Count == 0) return Array.Empty<AnomalyCandidate>();

        var ordered = overdue.OrderBy(o => o.Month).ToList();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Période de TVA non verrouillée",
                $"{ordered.Count} déclaration(s) de TVA restent modifiables plus de {graceDays} jour(s) " +
                "après leur échéance.",
                "Rien ne garantit que le déposé corresponde encore à la comptabilité.",
                accountRef: "4367",
                amount: 0m,
                periodFrom: new DateOnly(ctx.FiscalYear, ordered[0].Month, 1),
                periodTo: new DateOnly(ctx.FiscalYear, ordered[^1].Month, 1),
                lines: ordered.Select(o => new AnomalyLineCandidate(
                    null, null, new DateTime(ctx.FiscalYear, o.Month, 1), "4367",
                    $"{o.Month:D2}/{ctx.FiscalYear} — échéance {o.DueDate:dd/MM/yyyy}, statut " +
                    (o.Status == VatDeclarationStatus.Draft ? "brouillon" : "soumise"),
                    0, 0, null, null)).ToList(),
                recommendations:
                [
                    "Verrouiller la déclaration une fois le dépôt confirmé.",
                    "Vérifier l'écart entre le déclaré et le comptabilisé avant verrouillage."
                ],
                deepLinkRoute: "/accounting/vat-declaration")
        ];
    }

    /// <summary>Échéance par défaut : le 28 du mois suivant la période déclarée.</summary>
    private static DateTime NextMonthDueDate(int year, int month)
    {
        var next = new DateTime(year, month, 1).AddMonths(1);
        var day = Math.Min(DefaultDueDayOfNextMonth, DateTime.DaysInMonth(next.Year, next.Month));
        return new DateTime(next.Year, next.Month, day);
    }
}
