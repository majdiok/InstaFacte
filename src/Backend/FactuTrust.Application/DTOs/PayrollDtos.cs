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
    public IReadOnlyList<PayslipListDto> Payslips { get; init; } = Array.Empty<PayslipListDto>();
    public IReadOnlyList<PayrollOvertimeLineDto> OvertimeLines { get; init; } = Array.Empty<PayrollOvertimeLineDto>();
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
    public decimal NetSalary { get; init; }
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
    public decimal Css { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }
    public decimal NetSalary { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }
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
