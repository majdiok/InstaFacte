using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>
/// Journal de paie d'un mois : détail par salarié (rubriques du bulletin gelé) et ventilation
/// comptable OD avec contrôle d'équilibre. La ventilation reprend l'écriture réellement
/// comptabilisée quand elle existe, sinon une simulation issue des totaux figés du cycle.
/// </summary>
public sealed record GeneratePayrollJournalQuery(
    int Year,
    int Month,
    bool IncludeCalculated = false) : IRequest<Result<PayrollJournalDto>>;

public sealed class GeneratePayrollJournalQueryHandler
    : IRequestHandler<GeneratePayrollJournalQuery, Result<PayrollJournalDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly AccountingSettings _settings;

    public GeneratePayrollJournalQueryHandler(
        IPayrollRunRepository runs,
        IJournalEntryRepository journalEntries,
        IOptions<AccountingSettings> settings)
    {
        _runs = runs;
        _journalEntries = journalEntries;
        _settings = settings.Value;
    }

    public async Task<Result<PayrollJournalDto>> Handle(GeneratePayrollJournalQuery request, CancellationToken cancellationToken)
    {
        if (request.Year is < 2000 or > 2100)
            return Result.Failure<PayrollJournalDto>(Error.Validation("Year", "L'année doit être comprise entre 2000 et 2100."));
        if (request.Month is < 1 or > 12)
            return Result.Failure<PayrollJournalDto>(Error.Validation("Month", "Le mois doit être compris entre 1 et 12."));

        var periodLabel = PayrollReportHelpers.PeriodLabel(request.Year, request.Month, request.Month);

        var run = await _runs.GetByPeriodWithPayslipsAsync(request.Year, request.Month, cancellationToken);
        if (run is null)
        {
            return Result.Failure<PayrollJournalDto>(new Error(
                "PayrollRun.NotFound",
                $"Aucun cycle de paie pour {periodLabel}."));
        }

        if (run.Status == PayrollRunStatus.Draft)
        {
            return Result.Failure<PayrollJournalDto>(Error.Conflict(
                $"Le cycle {periodLabel} est en brouillon : aucun bulletin à éditer."));
        }

        if (run.Status == PayrollRunStatus.Calculated && !request.IncludeCalculated)
        {
            return Result.Failure<PayrollJournalDto>(Error.Conflict(
                $"Le cycle {periodLabel} n'est pas validé. Cochez « inclure les cycles calculés » pour obtenir un état provisoire."));
        }

        var payslips = run.Payslips.ToList();

        var lines = payslips
            .Select(ToEmployeeLine)
            .OrderBy(l => l.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var accounting = await BuildAccountingViewAsync(run, cancellationToken);

        var totalCnssEmployer = R(payslips.Sum(p => p.CnssEmployer));
        var totalWorkAccident = R(payslips.Sum(p => p.WorkAccidentContribution));
        var totalTfp = R(payslips.Sum(p => p.Tfp));
        var totalFoprolos = R(payslips.Sum(p => p.Foprolos));
        var totalCssEmployer = R(payslips.Sum(p => p.CssEmployer));
        var totalEmployerCharges = R(totalCnssEmployer + totalWorkAccident + totalTfp + totalFoprolos + totalCssEmployer);

        var totalDebit = R(accounting.Lines.Sum(l => l.Debit));
        var totalCredit = R(accounting.Lines.Sum(l => l.Credit));

        var dto = new PayrollJournalDto
        {
            PayrollRunId = run.Id,
            Year = run.Year,
            Month = run.Month,
            PeriodLabel = periodLabel,
            Status = run.Status.ToString(),
            StatusDisplay = run.Status.ToDisplayString(),
            IsProvisional = run.Status == PayrollRunStatus.Calculated,
            EmployeeCount = lines.Count,
            TotalGross = run.TotalGross,
            TotalCnssableGross = R(payslips.Sum(p => p.CnssableGross)),
            TotalCnssEmployee = run.TotalCnssEmployee,
            TotalProfessionalExpenses = R(payslips.Sum(p => p.ProfessionalExpenses)),
            TotalFamilyDeductions = R(payslips.Sum(p => p.FamilyDeductions)),
            TotalNetTaxable = R(payslips.Sum(p => p.MonthlyNetTaxable)),
            TotalIrpp = run.TotalIrpp,
            TotalIrppRegularization = run.TotalIrppRegularization,
            TotalCss = run.TotalCss,
            TotalCssRegularization = run.TotalCssRegularization,
            TotalOtherDeductions = run.TotalOtherDeductions,
            TotalNonTaxableAllowances = R(payslips.Sum(p => p.NonTaxableAllowances)),
            TotalNetSalary = run.TotalNet,
            TotalCnssEmployer = totalCnssEmployer,
            TotalWorkAccident = totalWorkAccident,
            TotalTfp = totalTfp,
            TotalFoprolos = totalFoprolos,
            TotalCssEmployer = totalCssEmployer,
            TotalEmployerCharges = totalEmployerCharges,
            TotalEmployerCost = R(run.TotalGross + totalEmployerCharges),
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = totalDebit == totalCredit,
            AccountingLinesArePosted = accounting.IsPosted,
            AccountingEntryNumber = accounting.EntryNumber,
            AccountingEntryDate = accounting.EntryDate,
            AccountingJournalCode = accounting.JournalCode,
            Lines = lines,
            AccountingLines = accounting.Lines
        };

        return Result.Success(dto);
    }

    private static PayrollJournalEmployeeLineDto ToEmployeeLine(Payslip p)
    {
        var employerCharges = R(p.CnssEmployer + p.WorkAccidentContribution + p.Tfp + p.Foprolos + p.CssEmployer);

        return new PayrollJournalEmployeeLineDto
        {
            PayslipId = p.Id,
            EmployeeId = p.EmployeeId,
            EmployeeNumber = p.EmployeeNumber,
            EmployeeName = p.EmployeeName,
            CnssNumber = p.CnssNumber,
            GrossSalary = p.GrossSalary,
            CnssableGross = p.CnssableGross,
            CnssEmployee = p.CnssEmployee,
            ProfessionalExpenses = p.ProfessionalExpenses,
            FamilyDeductions = p.FamilyDeductions,
            MonthlyNetTaxable = p.MonthlyNetTaxable,
            Irpp = p.Irpp,
            IrppRegularization = p.IrppRegularization,
            Css = p.Css,
            CssRegularization = p.CssRegularization,
            OtherDeductions = p.OtherDeductions,
            NonTaxableAllowances = p.NonTaxableAllowances,
            NetSalary = p.NetSalary,
            CnssEmployer = p.CnssEmployer,
            WorkAccidentContribution = p.WorkAccidentContribution,
            Tfp = p.Tfp,
            Foprolos = p.Foprolos,
            CssEmployer = p.CssEmployer,
            TotalEmployerCharges = employerCharges,
            TotalCost = R(p.GrossSalary + employerCharges)
        };
    }

    /// <summary>
    /// Ventilation comptable : l'écriture réellement comptabilisée fait foi. À défaut (cycle
    /// seulement calculé, ou plan comptable absent au moment de la validation), on simule
    /// l'écriture à partir des totaux figés du cycle — le drapeau <c>IsPosted</c> le signale.
    /// </summary>
    private async Task<AccountingView> BuildAccountingViewAsync(PayrollRun run, CancellationToken cancellationToken)
    {
        // R-27 : l'écriture active fait foi. Après réouverture→revalidation, l'écriture extournée
        // est ignorée et seule la nouvelle écriture active est présentée.
        var entry = await _journalEntries.GetActiveBySourceAsync(
            PayrollReportHelpers.PayrollRunSourceType, run.Id, cancellationToken);

        if (entry is not null)
        {
            var postedLines = entry.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new PayrollJournalAccountingLineDto
                {
                    AccountNumber = l.AccountNumber,
                    AccountLabel = PayrollReportHelpers.AccountLabel(l.AccountNumber),
                    Label = l.Label,
                    Debit = l.DebitAmount.Amount,
                    Credit = l.CreditAmount.Amount
                })
                .ToList();

            return new AccountingView(postedLines, true, entry.EntryNumber, entry.EntryDate, entry.JournalCode);
        }

        // Aligné sur AccountingService : la somme des bulletins prime quand ils sont chargés
        // (couvre les cycles calculés avant l'introduction de TotalOtherDeductions).
        var otherDeductions = run.Payslips.Count > 0
            ? R(run.Payslips.Sum(p => p.OtherDeductions))
            : run.TotalOtherDeductions;

        // Simulation alignée sur la génération réelle : même profil (§5.3) et même carte de comptes.
        var profile = ResolvePayrollAccountProfile(run.Year, run.Month);
        var accountMap = new PayrollJournalEntryAccountMap
        {
            LoansAccount = _settings.PayrollEmployeeLoansAccount,
            GarnishmentsAccount = _settings.PayrollGarnishmentsAccount,
            MutuelleEmployeeAccount = _settings.PayrollMutuelleEmployeeAccount,
            MealVoucherEmployeeAccount = _settings.PayrollMealVoucherEmployeeAccount,
            InKindBenefitOffsetAccount = profile == PayrollAccountProfile.Sce2026
                ? _settings.PayrollInKindOffsetAccount
                : PayrollJournalEntryBuilder.AdvancesAccount
        };

        var label = $"Paie {run.Month:D2}/{run.Year}";
        var hasTypedDeductions = run.Payslips.Any(p =>
            p.Lines.Any(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind.HasValue));

        var built = hasTypedDeductions
            ? PayrollJournalEntryBuilder.BuildLinesFromRun(run, label, accountMap, null, profile)
            : PayrollJournalEntryBuilder.BuildLines(
                run.TotalGross,
                run.TotalNet,
                run.TotalCnssEmployee,
                run.TotalCnssEmployer,
                run.TotalIrpp,
                run.TotalCss,
                run.TotalTfp,
                run.TotalFoprolos,
                run.TotalWorkAccident,
                otherDeductions,
                label,
                run.TotalIrppRegularization,
                run.TotalCssRegularization,
                run.TotalCssEmployer,
                profile: profile);

        if (built.IsFailure)
            return new AccountingView(Array.Empty<PayrollJournalAccountingLineDto>(), false, null, null, null);

        var simulated = built.Value
            .Select(l => new PayrollJournalAccountingLineDto
            {
                AccountNumber = l.AccountNumber,
                AccountLabel = PayrollReportHelpers.AccountLabel(l.AccountNumber),
                Label = l.Label,
                Debit = l.Debit,
                Credit = l.Credit
            })
            .ToList();

        return new AccountingView(simulated, false, null, null, null);
    }

    /// <summary>
    /// Profil d'imputation du cycle (plan §5.3) — réplique de AccountingService pour aligner la
    /// simulation sur la génération réelle.
    /// </summary>
    private PayrollAccountProfile ResolvePayrollAccountProfile(int year, int month)
    {
        var effective = _settings.PayrollAccountProfileEffectiveDate;
        if (effective is null)
            return _settings.PayrollAccountProfile;

        var periodEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return periodEnd < effective.Value
            ? PayrollAccountProfile.Legacy
            : _settings.PayrollAccountProfile;
    }

    private sealed record AccountingView(
        IReadOnlyList<PayrollJournalAccountingLineDto> Lines,
        bool IsPosted,
        int? EntryNumber,
        DateTime? EntryDate,
        string? JournalCode);

    private static decimal R(decimal value) => PayrollReportHelpers.Round(value);
}
