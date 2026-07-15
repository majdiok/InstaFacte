using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services;

public class WithholdingComplianceService : IWithholdingComplianceService
{
    private readonly ISupplierInvoiceRepository _invoices;

    public WithholdingComplianceService(ISupplierInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    /// <summary>
    /// TEJ declarations are due by the 28th of the following month.
    /// e.g. January 2026 certificates must be filed by February 28, 2026.
    /// </summary>
    public DateTime GetNextTejDeadline(int year, int month)
    {
        var followingMonth = new DateTime(year, month, 1).AddMonths(1);
        var day28 = Math.Min(28, DateTime.DaysInMonth(followingMonth.Year, followingMonth.Month));
        return new DateTime(followingMonth.Year, followingMonth.Month, day28);
    }

    public bool IsDeadlineApproaching(int year, int month, int warningDays = 5)
    {
        var deadline = GetNextTejDeadline(year, month);
        var daysUntil = (deadline - DateTime.UtcNow.Date).Days;
        return daysUntil >= 0 && daysUntil <= warningDays;
    }

    /// <summary>
    /// Art. 52-g: withholding on purchases applies only when TTC >= 1000 TND.
    /// </summary>
    public bool IsThresholdApplicable(string operationCode, decimal amountTTC)
    {
        if (string.IsNullOrWhiteSpace(operationCode)) return false;
        var prefix = operationCode.Split('_')[0].ToUpperInvariant();
        if (prefix != "RS7") return true;
        return amountTTC >= WithholdingTaxCalculationService.Rs7TtcThresholdTnd;
    }

    public async Task<WithholdingComplianceCheckResult> CheckComplianceAsync(int year, int month, CancellationToken ct = default)
    {
        var alerts = new List<ComplianceAlert>();
        var deadline = GetNextTejDeadline(year, month);
        var daysUntil = (deadline - DateTime.UtcNow.Date).Days;

        var (paidCount, totalWithheld) = await _invoices.GetPaidWithholdingSummaryForTejPeriodAsync(year, month, ct);

        if (daysUntil < 0 && paidCount > 0)
        {
            alerts.Add(new ComplianceAlert(
                ComplianceAlertSeverity.Critical,
                "DEADLINE_PASSED",
                $"L'échéance de déclaration TEJ pour {month:D2}/{year} est dépassée ({deadline:dd/MM/yyyy}). " +
                "Déposez la déclaration RS sur le portail TEJ."));
        }
        else if (daysUntil >= 0 && daysUntil <= 5 && paidCount > 0)
        {
            alerts.Add(new ComplianceAlert(
                ComplianceAlertSeverity.Warning,
                "DEADLINE_APPROACHING",
                $"L'échéance TEJ est dans {daysUntil} jour(s) ({deadline:dd/MM/yyyy}). " +
                $"{paidCount} facture(s) fournisseur avec retenue à inclure dans la déclaration."));
        }

        var isCompliant = !alerts.Any(a => a.Severity == ComplianceAlertSeverity.Critical);

        return new WithholdingComplianceCheckResult(
            isCompliant,
            alerts,
            deadline,
            Math.Max(0, daysUntil),
            paidCount,
            totalWithheld);
    }
}
