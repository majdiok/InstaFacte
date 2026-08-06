using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Services;

/// <summary>
/// Agrège les entrées de paie avancées (caisses, tickets restaurant, avantages en nature, prêts, saisies).
/// </summary>
public sealed class PayrollInputBuilder
{
    private const decimal MonthlyWorkingDays = 26m;

    private readonly AccountingSettings _settings;

    public PayrollInputBuilder(IOptions<AccountingSettings> settings)
    {
        _settings = settings.Value;
    }

    public sealed class MonthBatchData
    {
        public IReadOnlyList<EmployeeSocialFundEnrollment> Enrollments { get; init; } = Array.Empty<EmployeeSocialFundEnrollment>();
        public IReadOnlyDictionary<Guid, SocialFundScheme> Schemes { get; init; } = new Dictionary<Guid, SocialFundScheme>();
        public IReadOnlyList<PayrollMealVoucherLine> MealVouchers { get; init; } = Array.Empty<PayrollMealVoucherLine>();
        public IReadOnlyList<EmployeeInKindBenefit> InKindBenefits { get; init; } = Array.Empty<EmployeeInKindBenefit>();
        public IReadOnlyList<EmployeeLoan> LoansWithDueInstallments { get; init; } = Array.Empty<EmployeeLoan>();
        public IReadOnlyList<EmployeeGarnishment> ActiveGarnishments { get; init; } = Array.Empty<EmployeeGarnishment>();
        /// <summary>
        /// Régularisations IRPP/CSS annuelles du mois (décembre, soldes de tout compte).
        /// Vide hors mois de régularisation : le calcul est alors strictement inchangé.
        /// </summary>
        public IReadOnlyList<PayrollIrppRegularization> IrppRegularizations { get; init; } = Array.Empty<PayrollIrppRegularization>();
        /// <summary>Suspensions approuvées chevauchant le mois (prorata automatique).</summary>
        public IReadOnlyList<EmployeePayrollSuspension> Suspensions { get; init; } = Array.Empty<EmployeePayrollSuspension>();
    }

    public PayrollComputationInput Build(
        Employee employee,
        EmploymentContract contract,
        IReadOnlyList<LeaveRequest> monthLeaves,
        decimal advanceTotal,
        IReadOnlyList<PayrollOvertimeLine> overtimeLines,
        IReadOnlyList<PayrollVariableAllowanceLine> variableAllowanceLines,
        PayrollYearParameters parameters,
        MonthBatchData batch,
        int year,
        int month,
        int? effectiveDependentParents = null)
    {
        var referenceDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        var allowanceInputs = new List<AllowanceLineInput>();

        foreach (var allowance in contract.Allowances)
            allowanceInputs.Add(new AllowanceLineInput(allowance.Label, allowance.Amount, allowance.Taxable, allowance.SubjectToCnss));

        foreach (var allowance in variableAllowanceLines)
            allowanceInputs.Add(new AllowanceLineInput(allowance.Label, allowance.Amount, allowance.Taxable, allowance.SubjectToCnss));

        if (_settings.PayrollMealVouchersEnabled)
        {
            foreach (var mealLine in batch.MealVouchers.Where(m => m.EmployeeId == employee.Id))
                ApplyMealVoucher(mealLine, parameters, allowanceInputs);
        }

        var buckets = AllowanceBucketAggregator.Aggregate(
            allowanceInputs,
            parameters.EnableAllowanceQuadrantMatrix);

        var unpaidDays = monthLeaves
            .Where(l => l.EmployeeId == employee.Id && l.Type.ReducesGross())
            .Sum(l => l.Days);
        var dailyRate = contract.BaseSalary / MonthlyWorkingDays;
        var unpaidAbsenceAmount = Math.Round(dailyRate * unpaidDays, 3, MidpointRounding.AwayFromZero);
        var overtimeAmount = overtimeLines.Sum(l => l.EffectiveAmount);

        decimal prorataDeduction = 0m;
        decimal prorataWorkedDays = 0m;
        decimal prorataNonWorkedDays = 0m;

        if (parameters.EnableAutomaticProrata)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var effectiveStart = MaxDate(monthStart, contract.StartDate, employee.HireDate);
            var effectiveEnd = MinDate(monthEnd, contract.EndDate ?? monthEnd, employee.TerminationDate ?? monthEnd);

            var suspensionPeriods = batch.Suspensions
                .Where(s => s.EmployeeId == employee.Id)
                .Select(s => new PayrollProrataSuspensionPeriod(s.StartDate, s.EndDate, s.IsPaid, s.IsApproved))
                .ToList();

            var prorata = PayrollProrataCalculator.Compute(new PayrollProrataMonthInput
            {
                Year = year,
                Month = month,
                BaseSalary = contract.BaseSalary,
                EffectiveStart = effectiveStart,
                EffectiveEnd = effectiveEnd,
                IsEnabled = true,
                Suspensions = suspensionPeriods
            });

            prorataDeduction = prorata.DeductionAmount;
            prorataWorkedDays = prorata.WorkedDays;
            prorataNonWorkedDays = prorata.NonWorkedDays;
        }

        var deductionLines = new List<DeductionLineInput>();
        var employerChargeLines = new List<EmployerChargeLineInput>();
        decimal inKindTotal = 0m;

        if (advanceTotal > 0)
            deductionLines.Add(new DeductionLineInput("Avance sur salaire", advanceTotal, DeductionKind.Advance));

        if (_settings.PayrollInKindBenefitsEnabled)
        {
            foreach (var benefit in batch.InKindBenefits.Where(b => b.EmployeeId == employee.Id))
            {
                inKindTotal = R(inKindTotal + benefit.MonthlyValue);
                deductionLines.Add(new DeductionLineInput(
                    $"Compensation avantage en nature — {benefit.Label}",
                    benefit.MonthlyValue,
                    DeductionKind.InKindBenefitOffset,
                    benefit.Id));
            }
        }

        if (_settings.PayrollEmployeeLoansEnabled)
        {
            foreach (var loan in batch.LoansWithDueInstallments.Where(l => l.EmployeeId == employee.Id))
            {
                foreach (var installment in loan.Installments.Where(i => i.Year == year && i.Month == month && !i.IsSettled))
                {
                    deductionLines.Add(new DeductionLineInput(
                        $"Prêt {loan.Reference} — échéance {installment.SequenceNumber}",
                        installment.Amount,
                        DeductionKind.Loan,
                        installment.Id));
                }
            }
        }

        // Régularisation annuelle : appliquée avant les retenues post-impôt, de sorte que la
        // quotité saisissable se calcule bien sur le net réellement perçu.
        var regularization = batch.IrppRegularizations.FirstOrDefault(r => r.EmployeeId == employee.Id);

        var baseInput = new PayrollComputationInput
        {
            IrppRegularization = regularization?.EffectiveIrppDelta ?? 0m,
            CssRegularization = regularization?.EffectiveCssDelta ?? 0m,
            BaseSalary = contract.BaseSalary,
            TaxableCnssableAllowances = buckets.TaxableCnssable,
            TaxableOnlyAllowances = buckets.TaxableOnly,
            CnssOnlyAllowances = buckets.CnssOnly,
            NonTaxableAllowances = buckets.NonTaxable,
            AllowanceLines = buckets.Lines,
            OvertimeAmount = overtimeAmount,
            UnpaidAbsenceAmount = unpaidAbsenceAmount,
            ProrataDeductionAmount = prorataDeduction,
            ProrataWorkedDays = prorataWorkedDays,
            ProrataNonWorkedDays = prorataNonWorkedDays,
            InKindTaxableCnssableBenefits = inKindTotal,
            Regime = contract.Regime,
            WorkAccidentRate = contract.WorkAccidentRate,
            IsIndustrialSector = parameters.IsIndustrialSector,
            IsHeadOfFamily = employee.IsHeadOfFamily,
            DependentChildren = employee.DependentChildren,
            StudentChildren = employee.StudentChildren,
            DisabledChildren = employee.DisabledChildren,
            DependentParents = effectiveDependentParents ?? employee.DependentParents,
            DeductionLines = deductionLines
        };

        if (_settings.PayrollSocialFundsEnabled)
            ApplySocialFunds(employee, batch, baseInput, parameters, referenceDate, deductionLines, employerChargeLines);

        if (_settings.PayrollMealVouchersEnabled)
        {
            foreach (var mealLine in batch.MealVouchers.Where(m => m.EmployeeId == employee.Id))
            {
                var employeeShare = mealLine.EmployeeContribution;
                if (employeeShare > 0)
                    deductionLines.Add(new DeductionLineInput(
                        "Tickets restaurant (part employée)",
                        employeeShare,
                        DeductionKind.MealVoucherEmployeeShare,
                        mealLine.Id));
            }
        }

        baseInput = CopyWithDeductions(baseInput, deductionLines, employerChargeLines);

        if (!_settings.PayrollGarnishmentsEnabled)
            return baseInput;

        var interim = PayrollCalculator.Compute(baseInput, parameters);
        var postTaxLines = AllocateGarnishments(
            employee.Id,
            batch.ActiveGarnishments,
            interim.NetSalary,
            parameters.GarnishmentBrackets.ToList(),
            year,
            month);

        return CopyWithPostTaxDeductions(baseInput, postTaxLines);
    }

    private static void ApplyMealVoucher(
        PayrollMealVoucherLine line,
        PayrollYearParameters parameters,
        List<AllowanceLineInput> allowanceInputs)
    {
        var totalValue = line.TotalValue;
        var exemptValue = R(Math.Min(totalValue, line.Days * parameters.MealVoucherDailyExemptionCap));
        var exemptRatio = totalValue > 0 ? exemptValue / totalValue : 0m;
        var exemptEmployer = R(line.EmployerContribution * exemptRatio);
        var taxableEmployer = R(line.EmployerContribution - exemptEmployer);

        if (exemptEmployer > 0)
            allowanceInputs.Add(new AllowanceLineInput("Tickets restaurant (part employeur exonérée)", exemptEmployer, false, false));
        if (taxableEmployer > 0)
            allowanceInputs.Add(new AllowanceLineInput("Tickets restaurant (part employeur imposable)", taxableEmployer, true, true));
    }

    private static void ApplySocialFunds(
        Employee employee,
        MonthBatchData batch,
        PayrollComputationInput baseInput,
        PayrollYearParameters parameters,
        DateTime referenceDate,
        List<DeductionLineInput> deductionLines,
        List<EmployerChargeLineInput> employerChargeLines)
    {
        var enrollments = batch.Enrollments.Where(e => e.EmployeeId == employee.Id).ToList();
        if (enrollments.Count == 0)
            return;

        var interim = PayrollCalculator.Compute(baseInput, parameters);

        foreach (var enrollment in enrollments)
        {
            var scheme = batch.Schemes.GetValueOrDefault(enrollment.SocialFundSchemeId);
            if (scheme is null || !scheme.IsEffectiveOn(referenceDate))
                continue;

            var contribution = SocialFundContributionCalculator.Compute(
                scheme,
                enrollment,
                interim.CnssableGross,
                interim.MonthlyNetTaxable);

            if (contribution.EmployeeAmount > 0)
                deductionLines.Add(new DeductionLineInput(
                    scheme.Name,
                    contribution.EmployeeAmount,
                    DeductionKind.MutuelleEmployee,
                    enrollment.Id));

            if (contribution.EmployerAmount > 0)
                employerChargeLines.Add(new EmployerChargeLineInput(
                    $"{scheme.Name} (part employeur)",
                    contribution.EmployerAmount,
                    scheme.EmployerAccountSce));
        }
    }

    private static List<DeductionLineInput> AllocateGarnishments(
        Guid employeeId,
        IReadOnlyList<EmployeeGarnishment> garnishments,
        decimal netBeforeGarnishments,
        IReadOnlyList<PayrollGarnishmentBracket> brackets,
        int year,
        int month)
    {
        var active = garnishments.Where(g => g.EmployeeId == employeeId).ToList();
        if (active.Count == 0)
            return [];

        var hasAlimony = active.Any(g => g.Type == GarnishmentType.Alimony);
        var available = GarnishmentCalculator.ComputeAvailableSeizable(netBeforeGarnishments, brackets, hasAlimony);

        var requests = active.Select(g => new GarnishmentCalculator.GarnishmentRequest(
            g.Id,
            g.Type,
            $"{g.Type switch { GarnishmentType.Alimony => "Pension alimentaire", _ => "Saisie" }} — {g.BeneficiaryName}",
            g.Priority,
            g.IssuedAt,
            g.ComputeRequestedAmount(netBeforeGarnishments),
            g.BeneficiaryRib)).ToList();

        var allocations = GarnishmentCalculator.Allocate(available, requests);

        return allocations
            .Where(a => a.AppliedAmount > 0)
            .Select(a => new DeductionLineInput(
                a.Label,
                a.AppliedAmount,
                a.Type == GarnishmentType.Alimony ? DeductionKind.Alimony : DeductionKind.Garnishment,
                a.GarnishmentId))
            .ToList();
    }

    private static PayrollComputationInput CopyWithDeductions(
        PayrollComputationInput input,
        List<DeductionLineInput> deductionLines,
        List<EmployerChargeLineInput> employerChargeLines) =>
        new()
        {
            BaseSalary = input.BaseSalary,
            TaxableCnssableAllowances = input.TaxableCnssableAllowances,
            TaxableOnlyAllowances = input.TaxableOnlyAllowances,
            CnssOnlyAllowances = input.CnssOnlyAllowances,
            NonTaxableAllowances = input.NonTaxableAllowances,
            AllowanceLines = input.AllowanceLines,
            OvertimeAmount = input.OvertimeAmount,
            UnpaidAbsenceAmount = input.UnpaidAbsenceAmount,
            ProrataDeductionAmount = input.ProrataDeductionAmount,
            ProrataWorkedDays = input.ProrataWorkedDays,
            ProrataNonWorkedDays = input.ProrataNonWorkedDays,
            InKindTaxableCnssableBenefits = input.InKindTaxableCnssableBenefits,
            IrppRegularization = input.IrppRegularization,
            CssRegularization = input.CssRegularization,
            OtherDeductions = R(deductionLines.Sum(d => d.Amount)),
            Regime = input.Regime,
            WorkAccidentRate = input.WorkAccidentRate,
            IsIndustrialSector = input.IsIndustrialSector,
            IsHeadOfFamily = input.IsHeadOfFamily,
            DependentChildren = input.DependentChildren,
            StudentChildren = input.StudentChildren,
            DisabledChildren = input.DisabledChildren,
            DependentParents = input.DependentParents,
            DeductionLines = deductionLines,
            EmployerChargeLines = employerChargeLines
        };

    private static PayrollComputationInput CopyWithPostTaxDeductions(
        PayrollComputationInput input,
        List<DeductionLineInput> postTaxLines) =>
        new()
        {
            BaseSalary = input.BaseSalary,
            TaxableCnssableAllowances = input.TaxableCnssableAllowances,
            TaxableOnlyAllowances = input.TaxableOnlyAllowances,
            CnssOnlyAllowances = input.CnssOnlyAllowances,
            NonTaxableAllowances = input.NonTaxableAllowances,
            AllowanceLines = input.AllowanceLines,
            OvertimeAmount = input.OvertimeAmount,
            UnpaidAbsenceAmount = input.UnpaidAbsenceAmount,
            ProrataDeductionAmount = input.ProrataDeductionAmount,
            ProrataWorkedDays = input.ProrataWorkedDays,
            ProrataNonWorkedDays = input.ProrataNonWorkedDays,
            InKindTaxableCnssableBenefits = input.InKindTaxableCnssableBenefits,
            IrppRegularization = input.IrppRegularization,
            CssRegularization = input.CssRegularization,
            OtherDeductions = R(input.DeductionLines.Sum(d => d.Amount) + postTaxLines.Sum(d => d.Amount)),
            Regime = input.Regime,
            WorkAccidentRate = input.WorkAccidentRate,
            IsIndustrialSector = input.IsIndustrialSector,
            IsHeadOfFamily = input.IsHeadOfFamily,
            DependentChildren = input.DependentChildren,
            StudentChildren = input.StudentChildren,
            DisabledChildren = input.DisabledChildren,
            DependentParents = input.DependentParents,
            DeductionLines = input.DeductionLines,
            PostTaxDeductionLines = postTaxLines,
            EmployerChargeLines = input.EmployerChargeLines
        };

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static DateTime MaxDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a.Date, b.Date, c.Date }.Max();

    private static DateTime MinDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a.Date, b.Date, c.Date }.Min();
}
