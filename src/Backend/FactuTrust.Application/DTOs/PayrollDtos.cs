using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

// ─────────────────────────────── Employees ───────────────────────────────

/// <summary>Salarié — vue liste.</summary>
public sealed record EmployeeListDto
{
    public Guid Id { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public string? JobTitle { get; init; }
    public decimal? CurrentBaseSalary { get; init; }
    /// <summary>Régime hebdomadaire du contrat actif ("FortyEightHours" / "FortyHours").</summary>
    public string? CurrentWeeklyRegime { get; init; }
    public DateTime HireDate { get; init; }
    public bool IsActive { get; init; }
}

public sealed record ContractAllowanceDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool Taxable { get; init; }
    public bool SubjectToCnss { get; init; }
}

public sealed record EmploymentContractDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = null!;
    public string TypeDisplay { get; init; } = null!;
    public string Regime { get; init; } = null!;
    public string RegimeDisplay { get; init; } = null!;
    public string WeeklyRegime { get; init; } = null!;
    public string WeeklyRegimeDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal BaseSalary { get; init; }
    public decimal WorkAccidentRate { get; init; }
    public string? JobTitle { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ContractAllowanceDto> Allowances { get; init; } = Array.Empty<ContractAllowanceDto>();
}

public sealed record EmployeeDetailDto
{
    public Guid Id { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? Category { get; init; }
    public string? Echelon { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime HireDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string MaritalStatus { get; init; } = null!;
    public string MaritalStatusDisplay { get; init; } = null!;
    public bool IsHeadOfFamily { get; init; }
    public int DependentChildren { get; init; }
    public int StudentChildren { get; init; }
    public int DisabledChildren { get; init; }
    public int DependentParents { get; init; }
    public AddressDto? Address { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Rib { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<EmploymentContractDto> Contracts { get; init; } = Array.Empty<EmploymentContractDto>();
}

public sealed record CreateEmployeeDto
{
    public string EmployeeNumber { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? Category { get; init; }
    public string? Echelon { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime HireDate { get; init; }
    public string MaritalStatus { get; init; } = "Single";
    public bool IsHeadOfFamily { get; init; }
    public int DependentChildren { get; init; }
    public int StudentChildren { get; init; }
    public int DisabledChildren { get; init; }
    public int DependentParents { get; init; }
    public string? Street { get; init; }
    public string? StreetLine2 { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Governorate { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Rib { get; init; }
}

public sealed record UpdateEmployeeDto
{
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? Category { get; init; }
    public string? Echelon { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string MaritalStatus { get; init; } = "Single";
    public bool IsHeadOfFamily { get; init; }
    public int DependentChildren { get; init; }
    public int StudentChildren { get; init; }
    public int DisabledChildren { get; init; }
    public int DependentParents { get; init; }
    public string? Street { get; init; }
    public string? StreetLine2 { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Governorate { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Rib { get; init; }
}

public sealed record ContractAllowanceInputDto
{
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool Taxable { get; init; } = true;
    public bool SubjectToCnss { get; init; } = true;
}

public sealed record CreateContractDto
{
    public string Type { get; init; } = "Cdi";
    public string Regime { get; init; } = "Rsna";
    public string WeeklyRegime { get; init; } = "FortyEightHours";
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal BaseSalary { get; init; }
    public decimal WorkAccidentRate { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyList<ContractAllowanceInputDto> Allowances { get; init; } = Array.Empty<ContractAllowanceInputDto>();
}

public sealed record UpdateContractDto
{
    public string Type { get; init; } = "Cdi";
    public string Regime { get; init; } = "Rsna";
    public string WeeklyRegime { get; init; } = "FortyEightHours";
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal BaseSalary { get; init; }
    public decimal WorkAccidentRate { get; init; }
    public string? JobTitle { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<ContractAllowanceInputDto> Allowances { get; init; } = Array.Empty<ContractAllowanceInputDto>();
}

// ─────────────────────────────── Payroll runs ───────────────────────────────

public sealed record PayrollRunListDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public int PayslipCount { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalNet { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}

public sealed record PayrollRunDetailDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public int ParametersFiscalYear { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalIrpp { get; init; }
    public decimal TotalCss { get; init; }
    public decimal TotalIrppSmigExemption { get; init; }
    public decimal TotalNet { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalTfp { get; init; }
    public decimal TotalFoprolos { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalOtherDeductions { get; init; }
    public DateTime? CalculatedAt { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public string? ValidatedBy { get; init; }
    public DateTime? ClosedAt { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal RemainingToPay { get; init; }
    public string PaymentStatus { get; init; } = null!;
    public string PaymentStatusDisplay { get; init; } = null!;
    public bool HasPayments { get; init; }
    public IReadOnlyList<PayslipListDto> Payslips { get; init; } = Array.Empty<PayslipListDto>();
    public IReadOnlyList<PayrollOvertimeLineDto> OvertimeLines { get; init; } = Array.Empty<PayrollOvertimeLineDto>();
    public IReadOnlyList<PayrollVariableAllowanceLineDto> VariableAllowanceLines { get; init; } = Array.Empty<PayrollVariableAllowanceLineDto>();
}

public sealed record CreatePayrollRunDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string? Label { get; init; }
    /// <summary>Déprécié — le secteur est lu depuis les paramètres d'exercice. Conservé pour compatibilité API, ignoré.</summary>
    public bool IsIndustrialSector { get; init; }
}

public sealed record CalculatePayrollRunDto
{
    /// <summary>Déprécié — le secteur est lu depuis les paramètres d'exercice (PayrollYearParameters.IsIndustrialSector). Conservé pour compatibilité API, ignoré.</summary>
    public bool IsIndustrialSector { get; init; }
    /// <summary>Solder automatiquement les avances en cours lors du calcul.</summary>
    public bool SettleOutstandingAdvances { get; init; } = true;
}

// ─────────────────────────────── Payslips ───────────────────────────────

public sealed record PayslipListDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string EmployeeNumber { get; init; } = null!;
    public decimal GrossSalary { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal Irpp { get; init; }
    public decimal Css { get; init; }
    public decimal IrppSmigExemption { get; init; }
    public decimal NetSalary { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingToPay { get; init; }
    public string PaymentStatus { get; init; } = null!;
    public string PaymentStatusDisplay { get; init; } = null!;
    public DateTime? PaidAt { get; init; }
}

public sealed record PayslipLineDto
{
    public int Order { get; init; }
    public string Label { get; init; } = null!;
    public string Kind { get; init; } = null!;
    public string KindDisplay { get; init; } = null!;
    public decimal? Base { get; init; }
    public decimal? Rate { get; init; }
    public decimal Amount { get; init; }
}

public sealed record PayslipDetailDto
{
    public Guid Id { get; init; }
    public Guid PayrollRunId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string EmployeeNumber { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public string? Cin { get; init; }
    public string? JobTitle { get; init; }
    public string? Category { get; init; }
    public string? Echelon { get; init; }
    public bool IsHeadOfFamily { get; init; }
    public decimal? WorkedDays { get; init; }
    public string? CompanyAddress { get; init; }
    public DateTime? HireDate { get; init; }
    public decimal? LeaveBalanceRemaining { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal TaxableBaseAfterCnss { get; init; }
    public decimal ProfessionalExpenses { get; init; }
    public decimal FamilyDeductions { get; init; }
    public decimal MonthlyNetTaxable { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    public decimal IrppBeforeSmigExemption { get; init; }
    public decimal IrppSmigExemption { get; init; }
    public decimal Css { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }
    public decimal NetSalary { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingToPay { get; init; }
    public string PaymentStatus { get; init; } = null!;
    public string PaymentStatusDisplay { get; init; } = null!;
    public DateTime? PaidAt { get; init; }
    public IReadOnlyList<PayslipLineDto> Lines { get; init; } = Array.Empty<PayslipLineDto>();
}

// ─────────────────────────────── Parameters (settings) ───────────────────────────────

public sealed record IrppBracketDto
{
    public decimal LowerBound { get; init; }
    public decimal Rate { get; init; }
}

public sealed record PayrollParametersDto
{
    public Guid Id { get; init; }
    public int FiscalYear { get; init; }
    public decimal CnssEmployeeRate { get; init; }
    public decimal CnssEmployerRate { get; init; }
    public decimal CnssEmployeeRateRsa { get; init; }
    public decimal CnssEmployerRateRsa { get; init; }
    public bool EnforceSmigOnContracts { get; init; }
    public bool EnableExtendedOvertimeRates { get; init; }
    public bool EnableAllowanceQuadrantMatrix { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssAnnualExemptionThreshold { get; init; }
    public decimal ProfessionalExpensesRate { get; init; }
    public decimal ProfessionalExpensesAnnualCap { get; init; }
    public decimal HeadOfFamilyAnnualDeduction { get; init; }
    public decimal ChildAnnualDeduction { get; init; }
    public int MaxDeductibleChildren { get; init; }
    public decimal StudentChildAnnualDeduction { get; init; }
    public decimal DisabledChildAnnualDeduction { get; init; }
    public decimal ParentDeductionRatePercent { get; init; }
    public decimal ParentAnnualDeductionCap { get; init; }
    public bool IsIndustrialSector { get; init; }
    public decimal TfpRateIndustry { get; init; }
    public decimal TfpRateOther { get; init; }
    public decimal FoprolosRate { get; init; }
    public decimal MonthlySmig { get; init; }
    public string SmigIrppExemptionMode { get; init; } = null!;
    public string SmigIrppExemptionModeDisplay { get; init; } = null!;
    public decimal? SmigIrppExemptionRateOverride { get; init; }
    public IReadOnlyList<IrppBracketDto> IrppBrackets { get; init; } = Array.Empty<IrppBracketDto>();
}

public sealed record UpdatePayrollParametersDto
{
    public decimal CnssEmployeeRate { get; init; }
    public decimal CnssEmployerRate { get; init; }
    public decimal CnssEmployeeRateRsa { get; init; }
    public decimal CnssEmployerRateRsa { get; init; }
    public bool EnforceSmigOnContracts { get; init; }
    public bool EnableExtendedOvertimeRates { get; init; }
    public bool EnableAllowanceQuadrantMatrix { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssAnnualExemptionThreshold { get; init; }
    public decimal ProfessionalExpensesRate { get; init; }
    public decimal ProfessionalExpensesAnnualCap { get; init; }
    public decimal HeadOfFamilyAnnualDeduction { get; init; }
    public decimal ChildAnnualDeduction { get; init; }
    public int MaxDeductibleChildren { get; init; }
    public decimal StudentChildAnnualDeduction { get; init; }
    public decimal DisabledChildAnnualDeduction { get; init; }
    public decimal ParentDeductionRatePercent { get; init; }
    public decimal ParentAnnualDeductionCap { get; init; }
    public bool IsIndustrialSector { get; init; }
    public decimal TfpRateIndustry { get; init; }
    public decimal TfpRateOther { get; init; }
    public decimal FoprolosRate { get; init; }
    public decimal MonthlySmig { get; init; }
    public string SmigIrppExemptionMode { get; init; } = "None";
    public decimal? SmigIrppExemptionRateOverride { get; init; }
    public IReadOnlyList<IrppBracketDto> IrppBrackets { get; init; } = Array.Empty<IrppBracketDto>();
}

// ─────────────────────────────── Leaves & advances ───────────────────────────────

public sealed record LeaveRequestDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string Type { get; init; } = null!;
    public string TypeDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal Days { get; init; }
    public string? Reason { get; init; }
    public bool IsApproved { get; init; }
    public DateTime? ApprovedAt { get; init; }
}

public sealed record CreateLeaveDto
{
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = "Paid";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal Days { get; init; }
    public string? Reason { get; init; }
}

public sealed record EmployeeAdvanceDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
    public bool IsSettled { get; init; }
}

public sealed record CreateAdvanceDto
{
    public Guid EmployeeId { get; init; }
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
}

// ─────────────────────────────── Overtime ───────────────────────────────

/// <summary>
/// Régularisation IRPP/CSS annuelle d'un salarié, telle qu'affichée et éditée.
/// Les écarts sont signés : positif = rappel à prélever, négatif = restitution.
/// </summary>
public sealed record IrppRegularizationDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? EmployeeNumber { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }

    public int Reason { get; init; }
    public string ReasonLabel { get; init; } = null!;

    public int MonthsCounted { get; init; }
    public decimal CumulNetTaxable { get; init; }
    public decimal CumulIrppWithheld { get; init; }
    public decimal CumulCssWithheld { get; init; }
    public decimal IrppDue { get; init; }
    public decimal CssDue { get; init; }

    public decimal ComputedIrppDelta { get; init; }
    public decimal ComputedCssDelta { get; init; }
    public decimal? OverrideIrppDelta { get; init; }
    public decimal? OverrideCssDelta { get; init; }
    public bool IsOverridden { get; init; }

    public decimal EffectiveIrppDelta { get; init; }
    public decimal EffectiveCssDelta { get; init; }
    public decimal EffectiveTotalDelta { get; init; }

    /// <summary>Vrai si la régularisation aboutit à un prélèvement complémentaire.</summary>
    public bool IsAdditionalWithholding { get; init; }

    public string? Notes { get; init; }

    /// <summary>Détail mois par mois ayant servi au calcul.</summary>
    public IReadOnlyList<IrppRegularizationMonthDto> Months { get; init; } = Array.Empty<IrppRegularizationMonthDto>();
}

/// <summary>Contribution d'un mois au cumul annuel (ligne du tableau « Détail du calcul »).</summary>
public sealed record IrppRegularizationMonthDto
{
    public int Month { get; init; }
    public string MonthLabel { get; init; } = null!;
    public decimal MonthlyNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    public decimal Css { get; init; }
    /// <summary>Faux pour le mois en cours de calcul, qui n'est pas encore arrêté.</summary>
    public bool IsSettled { get; init; }
}

/// <summary>Prévisualisation d'une régularisation, sans persistance (bouton « Calculer »).</summary>
public sealed record IrppRegularizationPreviewDto
{
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? EmployeeNumber { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }

    public int Reason { get; init; }
    public string ReasonLabel { get; init; } = null!;

    public int MonthsCounted { get; init; }
    public decimal CumulNetTaxable { get; init; }
    public decimal CumulIrppWithheld { get; init; }
    public decimal CumulCssWithheld { get; init; }
    public decimal IrppDue { get; init; }
    public decimal CssDue { get; init; }
    public decimal IrppDelta { get; init; }
    public decimal CssDelta { get; init; }
    public decimal TotalDelta { get; init; }
    public bool IsAdditionalWithholding { get; init; }

    /// <summary>Vrai si l'exercice n'a pas activé la régularisation : le calcul reste indicatif.</summary>
    public bool IsFeatureDisabled { get; init; }

    /// <summary>Vrai si l'année est incomplète (embauche ou départ en cours d'exercice).</summary>
    public bool IsPartialYear { get; init; }

    public IReadOnlyList<IrppRegularizationMonthDto> Months { get; init; } = Array.Empty<IrppRegularizationMonthDto>();
}

/// <summary>Ajustement manuel d'une régularisation.</summary>
public sealed record UpsertIrppRegularizationDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    /// <summary>Null = revenir au montant calculé.</summary>
    public decimal? OverrideIrppDelta { get; init; }
    /// <summary>Null = revenir au montant calculé.</summary>
    public decimal? OverrideCssDelta { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Compte rendu d'une génération batch des régularisations d'un cycle.</summary>
public sealed record GenerateIrppRegularizationsResultDto
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    /// <summary>Lignes laissées intactes car ajustées manuellement.</summary>
    public int PreservedOverrides { get; init; }
    public decimal TotalDelta { get; init; }
}

public sealed record PayrollOvertimeLineDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Hours { get; init; }
    public decimal RatePercent { get; init; }
    public string RatePercentDisplay { get; init; } = null!;
    public decimal ComputedAmount { get; init; }
    public decimal? OverrideAmount { get; init; }
    public bool IsOverridden { get; init; }
    public decimal EffectiveAmount { get; init; }
}

public sealed record UpsertOvertimeLineDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Hours { get; init; }
    public decimal RatePercent { get; init; } = 125m;
    public decimal? OverrideAmount { get; init; }
}

public sealed record OvertimePreviewDto
{
    public decimal HourlyRate { get; init; }
    public decimal ComputedAmount { get; init; }
    public decimal EffectiveAmount { get; init; }
    public bool IsOverridden { get; init; }
}

// ─────────────────────────────── Variable allowances ───────────────────────────────

public sealed record PayrollVariableAllowanceLineDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool Taxable { get; init; }
    public bool SubjectToCnss { get; init; }
}

public sealed record UpsertVariableAllowanceLineDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool Taxable { get; init; } = true;
    public bool SubjectToCnss { get; init; } = true;
}

// ─────────────────────────────── Social funds ───────────────────────────────

public sealed record SocialFundSchemeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }
    public decimal EmployeeRatePercent { get; init; }
    public decimal EmployerRatePercent { get; init; }
    public SocialFundBase Base { get; init; }
    public decimal FixedEmployeeAmount { get; init; }
    public decimal FixedEmployerAmount { get; init; }
    public decimal? MonthlyEmployeeCap { get; init; }
    public string EmployeeAccountSce { get; init; } = null!;
    public string EmployerAccountSce { get; init; } = null!;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
}

public sealed record UpsertSocialFundSchemeDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; } = true;
    public decimal EmployeeRatePercent { get; init; }
    public decimal EmployerRatePercent { get; init; }
    public SocialFundBase Base { get; init; }
    public decimal FixedEmployeeAmount { get; init; }
    public decimal FixedEmployerAmount { get; init; }
    public decimal? MonthlyEmployeeCap { get; init; }
    public string EmployeeAccountSce { get; init; } = "428.1";
    public string EmployerAccountSce { get; init; } = "647";
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
}

public sealed record EmployeeSocialFundEnrollmentDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public Guid SocialFundSchemeId { get; init; }
    public string? SchemeName { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? OverrideEmployeeAmount { get; init; }
    public decimal? OverrideEmployerAmount { get; init; }
}

public sealed record UpsertEmployeeSocialFundEnrollmentDto
{
    public Guid EmployeeId { get; init; }
    public Guid SocialFundSchemeId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? OverrideEmployeeAmount { get; init; }
    public decimal? OverrideEmployerAmount { get; init; }
}

// ─────────────────────────────── Meal vouchers ───────────────────────────────

public sealed record PayrollMealVoucherLineDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public int Days { get; init; }
    public decimal FaceValue { get; init; }
    public decimal EmployerContributionRate { get; init; }
    public decimal TotalValue { get; init; }
    public decimal EmployerContribution { get; init; }
    public decimal EmployeeContribution { get; init; }
}

public sealed record UpsertPayrollMealVoucherLineDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public int Days { get; init; }
    public decimal FaceValue { get; init; }
    public decimal EmployerContributionRate { get; init; }
}

// ─────────────────────────────── In-kind benefits ───────────────────────────────

public sealed record EmployeeInKindBenefitDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public BenefitInKindType Type { get; init; }
    public string Label { get; init; } = null!;
    public decimal MonthlyValue { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Description { get; init; }
}

public sealed record UpsertEmployeeInKindBenefitDto
{
    public Guid EmployeeId { get; init; }
    public BenefitInKindType Type { get; init; }
    public string Label { get; init; } = null!;
    public decimal MonthlyValue { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Description { get; init; }
}

// ─────────────────────────────── Employee loans ───────────────────────────────

public sealed record EmployeeLoanInstallmentDto
{
    public Guid Id { get; init; }
    public int SequenceNumber { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Amount { get; init; }
    public bool IsSettled { get; init; }
    public Guid? SettledInPayrollRunId { get; init; }
}

public sealed record EmployeeLoanDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string Reference { get; init; } = null!;
    public decimal Principal { get; init; }
    public int InstallmentCount { get; init; }
    public decimal MonthlyInstallmentAmount { get; init; }
    public int StartYear { get; init; }
    public int StartMonth { get; init; }
    public string? Notes { get; init; }
    public EmployeeLoanStatus Status { get; init; }
    public decimal RemainingBalance { get; init; }
    public IReadOnlyList<EmployeeLoanInstallmentDto> Installments { get; init; } = Array.Empty<EmployeeLoanInstallmentDto>();
}

public sealed record CreateEmployeeLoanDto
{
    public Guid EmployeeId { get; init; }
    public string Reference { get; init; } = null!;
    public decimal Principal { get; init; }
    public int InstallmentCount { get; init; }
    public int StartYear { get; init; }
    public int StartMonth { get; init; }
    public string? Notes { get; init; }
}

// ─────────────────────────────── Garnishments ───────────────────────────────

public sealed record EmployeeGarnishmentInstallmentDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public Guid PayrollRunId { get; init; }
    public decimal RequestedAmount { get; init; }
    public decimal AppliedAmount { get; init; }
    public decimal CarriedOverAmount { get; init; }
}

public sealed record EmployeeGarnishmentDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public GarnishmentType Type { get; init; }
    public string Reference { get; init; } = null!;
    public DateTime IssuedAt { get; init; }
    public string BeneficiaryName { get; init; } = null!;
    public string? BeneficiaryRib { get; init; }
    public int Priority { get; init; }
    public GarnishmentAmountKind Kind { get; init; }
    public decimal? FixedAmount { get; init; }
    public decimal? PercentOfNet { get; init; }
    public decimal? TotalAmountDue { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public EmployeeGarnishmentStatus Status { get; init; }
    public decimal TotalApplied { get; init; }
    public IReadOnlyList<EmployeeGarnishmentInstallmentDto> Installments { get; init; } = Array.Empty<EmployeeGarnishmentInstallmentDto>();
}

public sealed record CreateEmployeeGarnishmentDto
{
    public Guid EmployeeId { get; init; }
    public GarnishmentType Type { get; init; }
    public string Reference { get; init; } = null!;
    public DateTime IssuedAt { get; init; }
    public string BeneficiaryName { get; init; } = null!;
    public string? BeneficiaryRib { get; init; }
    public int Priority { get; init; }
    public GarnishmentAmountKind Kind { get; init; }
    public decimal? FixedAmount { get; init; }
    public decimal? PercentOfNet { get; init; }
    public decimal? TotalAmountDue { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

// ─────────────────────────────── Leave balance ───────────────────────────────

public sealed record LeaveBalanceDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal AccruedInYear { get; init; }
    public decimal TotalAcquired { get; init; }
    public decimal Consumed { get; init; }
    public decimal Remaining { get; init; }
}

public sealed record SetLeaveOpeningBalanceDto
{
    public decimal OpeningBalanceDays { get; init; }
}

// ─────────────────────────────── DTS (déclaration CNSS trimestrielle) ───────────────────────────────

public sealed record DtsLineDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal CnssEmployer { get; init; }
    public int MonthsCount { get; init; }
}

public sealed record DtsDeclarationDto
{
    public int Year { get; init; }
    public int Quarter { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalContributions { get; init; }
    public int EmployeeCount { get; init; }
    /// <summary>Mois du trimestre pour lesquels un cycle Validé/Clôturé existe.</summary>
    public IReadOnlyList<int> IncludedMonths { get; init; } = Array.Empty<int>();
    /// <summary>Mois du trimestre sans cycle Validé/Clôturé.</summary>
    public IReadOnlyList<int> MissingMonths { get; init; } = Array.Empty<int>();
    /// <summary>True si les trois mois du trimestre ont un cycle Validé/Clôturé.</summary>
    public bool IsComplete { get; init; }
    public IReadOnlyList<DtsLineDto> Lines { get; init; } = Array.Empty<DtsLineDto>();
}

// ─────────────────────────────── Export virement bancaire ───────────────────────────────

public sealed record PayrollBankTransferLineDto
{
    public Guid EmployeeId { get; init; }
    public Guid PayslipId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string FullName { get; init; } = null!;
    /// <summary>RIB complet (export fichier). En UI, masquer partiellement côté client.</summary>
    public string Rib { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public decimal NetSalary { get; init; }
    public string TransferLabel { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
}

public sealed record PayrollBankTransferExcludedLineDto
{
    public Guid EmployeeId { get; init; }
    public Guid? PayslipId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public decimal NetSalary { get; init; }
    public string Reason { get; init; } = null!;
    public string ReasonDisplay { get; init; } = null!;
}

public sealed record PayrollBankTransferWarningDto
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
}

public sealed record PayrollBankTransferDebtorDto
{
    public Guid? BankAccountId { get; init; }
    public string? Rib { get; init; }
    public string? Iban { get; init; }
    public string? BankName { get; init; }
    public string? BankCode { get; init; }
}

public sealed record PayrollBankTransferPreviewDto
{
    public Guid PayrollRunId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string PeriodLabel { get; init; } = null!;
    public string TransferLabel { get; init; } = null!;
    public string? CompanyName { get; init; }
    public PayrollBankTransferDebtorDto? DebtorAccount { get; init; }
    public int EligibleCount { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<PayrollBankTransferLineDto> Lines { get; init; } = Array.Empty<PayrollBankTransferLineDto>();
    public IReadOnlyList<PayrollBankTransferExcludedLineDto> ExcludedLines { get; init; } = Array.Empty<PayrollBankTransferExcludedLineDto>();
    public IReadOnlyList<PayrollBankTransferWarningDto> Warnings { get; init; } = Array.Empty<PayrollBankTransferWarningDto>();
}
