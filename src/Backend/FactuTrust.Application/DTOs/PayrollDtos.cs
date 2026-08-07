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
    /// <summary>None | Complete | Incomplete | Conflict</summary>
    public string ParentClaimsStatus { get; init; } = "None";
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
    public DateTime? CivpStartDate { get; init; }
    public DateTime? CivpEndDate { get; init; }
    public decimal CivpStateGrant { get; init; }
    public decimal CivpEmployerAllowance { get; init; }
    public string? AnetiReference { get; init; }
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
    /// <summary>Déclarations nominatives (CIN) des parents à charge.</summary>
    public IReadOnlyList<DependentParentClaimDto> DependentParentClaims { get; init; } = Array.Empty<DependentParentClaimDto>();
    /// <summary>None | Complete | Incomplete | Conflict</summary>
    public string ParentClaimsStatus { get; init; } = "None";
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
    /// <summary>Conservé pour compatibilité ; dérivé du nombre de <see cref="DependentParentClaims"/> si fourni.</summary>
    public int DependentParents { get; init; }
    public IReadOnlyList<DependentParentClaimDto> DependentParentClaims { get; init; } = Array.Empty<DependentParentClaimDto>();
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
    /// <summary>Conservé pour compatibilité ; dérivé du nombre de <see cref="DependentParentClaims"/> si fourni.</summary>
    public int DependentParents { get; init; }
    public IReadOnlyList<DependentParentClaimDto> DependentParentClaims { get; init; } = Array.Empty<DependentParentClaimDto>();
    public string? Street { get; init; }
    public string? StreetLine2 { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Governorate { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Rib { get; init; }
}

/// <summary>Déclaration nominative d'un parent à charge (CIN obligatoire).</summary>
public sealed record DependentParentClaimDto
{
    public Guid? Id { get; init; }
    public string ParentCin { get; init; } = null!;
    /// <summary>Father | Mother</summary>
    public string Kinship { get; init; } = null!;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

/// <summary>Résultat du calcul d'un cycle (warnings non bloquants).</summary>
public sealed record CalculatePayrollRunResultDto
{
    public int PayslipCount { get; init; }
    public IReadOnlyList<PayrollCalculationWarningDto> Warnings { get; init; } = Array.Empty<PayrollCalculationWarningDto>();
}

public sealed record PayrollCalculationWarningDto
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
    public Guid? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
}

public sealed record ParentClaimConflictDto
{
    public string ParentCin { get; init; } = null!;
    public Guid EmployeeIdA { get; init; }
    public string? EmployeeNameA { get; init; }
    public Guid EmployeeIdB { get; init; }
    public string? EmployeeNameB { get; init; }
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
    public DateTime? CivpStartDate { get; init; }
    public DateTime? CivpEndDate { get; init; }
    public decimal CivpStateGrant { get; init; }
    public decimal CivpEmployerAllowance { get; init; }
    public string? AnetiReference { get; init; }
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
    public DateTime? CivpStartDate { get; init; }
    public DateTime? CivpEndDate { get; init; }
    public decimal CivpStateGrant { get; init; }
    public decimal CivpEmployerAllowance { get; init; }
    public string? AnetiReference { get; init; }
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
    public decimal TotalCssEmployer { get; init; }
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
    public decimal ProrataWorkedDays { get; init; }
    public decimal ProrataNonWorkedDays { get; init; }
    public decimal ProrataDeductionAmount { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }
    public decimal CssEmployer { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingToPay { get; init; }
    public string PaymentStatus { get; init; } = null!;
    public string PaymentStatusDisplay { get; init; } = null!;
    public DateTime? PaidAt { get; init; }
    public IReadOnlyList<PayslipLineDto> Lines { get; init; } = Array.Empty<PayslipLineDto>();
}

// ─────────────────────────────── Prorata & suspensions ───────────────────────────────

public sealed record PayrollProrataPreviewLineDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string EmployeeNumber { get; init; } = null!;
    public decimal WorkedDays { get; init; }
    public decimal NonWorkedDays { get; init; }
    public decimal DeductionAmount { get; init; }
    public string Reason { get; init; } = null!;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record PayrollProrataPreviewDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public bool IsEnabled { get; init; }
    public int EmployeeCount { get; init; }
    public decimal TotalDeduction { get; init; }
    public IReadOnlyList<PayrollProrataPreviewLineDto> Lines { get; init; } = Array.Empty<PayrollProrataPreviewLineDto>();
}

public sealed record EmployeePayrollSuspensionDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = null!;
    public string TypeDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool IsPaid { get; init; }
    public string? Reason { get; init; }
    public bool IsApproved { get; init; }
    public DateTime? ApprovedAt { get; init; }
}

public sealed record CreateEmployeePayrollSuspensionDto
{
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool IsPaid { get; init; }
    public string? Reason { get; init; }
}

public sealed record UpdateEmployeePayrollSuspensionDto
{
    public string Type { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool IsPaid { get; init; }
    public string? Reason { get; init; }
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
    public bool EnableIrppRegularization { get; init; }
    public bool EnableAutomaticProrata { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssAnnualExemptionThreshold { get; init; }
    public decimal CssEmployerRate { get; init; }
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
    public decimal? CnssMonthlyCeiling { get; init; }
    public decimal? CnssDailyCeiling { get; init; }
    public decimal? CssMonthlyCeiling { get; init; }
    public decimal? AccidentWorkMonthlyCeiling { get; init; }
    public int SickLeaveWaitingDays { get; init; }
    public decimal SickLeaveIjRatePercent { get; init; }
    public int MaternityLeaveDurationDays { get; init; }
    public int PaternityLeaveDurationDays { get; init; }
    public decimal MaternityEmployerTopUpDefault { get; init; }
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
    public bool EnableIrppRegularization { get; init; }
    public bool EnableAutomaticProrata { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssAnnualExemptionThreshold { get; init; }
    public decimal CssEmployerRate { get; init; }
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
    public decimal? CnssMonthlyCeiling { get; init; }
    public decimal? CnssDailyCeiling { get; init; }
    public decimal? CssMonthlyCeiling { get; init; }
    public decimal? AccidentWorkMonthlyCeiling { get; init; }
    public int SickLeaveWaitingDays { get; init; }
    public decimal SickLeaveIjRatePercent { get; init; }
    public int MaternityLeaveDurationDays { get; init; }
    public int PaternityLeaveDurationDays { get; init; }
    public decimal MaternityEmployerTopUpDefault { get; init; }
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
    public string? MedicalCertificateNumber { get; init; }
    public DateTime? MedicalCertificateDate { get; init; }
    public bool SubrogationEnabled { get; init; }
    public decimal? EmployerTopUpPercent { get; init; }
    public int? EmployerTopUpDays { get; init; }
    public DateTime? ExpectedBirthDate { get; init; }
    public DateTime? ActualBirthDate { get; init; }
    public string? ChildBirthCertificateNumber { get; init; }
}

public sealed record CreateLeaveDto
{
    public Guid EmployeeId { get; init; }
    public string Type { get; init; } = "Paid";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal Days { get; init; }
    public string? Reason { get; init; }
    public string? MedicalCertificateNumber { get; init; }
    public DateTime? MedicalCertificateDate { get; init; }
    public bool SubrogationEnabled { get; init; }
    public decimal? EmployerTopUpPercent { get; init; }
    public int? EmployerTopUpDays { get; init; }
    public DateTime? ExpectedBirthDate { get; init; }
    public DateTime? ActualBirthDate { get; init; }
    public string? ChildBirthCertificateNumber { get; init; }
}

public sealed record DeclareBirthDto
{
    public Guid EmployeeId { get; init; }
    public DateTime ActualBirthDate { get; init; }
    public string? ChildBirthCertificateNumber { get; init; }
    public bool CreateMaternityLeave { get; init; }
    public bool CreatePaternityLeave { get; init; }
}

public sealed record CnssIjClaimDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public Guid LeaveRequestId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public DateTime? PaidAt { get; init; }
}

public sealed record MarkCnssIjClaimPaidRequest
{
    public DateTime PaidAt { get; init; }
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
    public string Source { get; init; } = "Manual";
    public Guid? AnnualBonusRuleId { get; init; }
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

// ─────────────────────────────── Public holidays ───────────────────────────────

public sealed record PayrollPublicHolidayDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public DateTime Date { get; init; }
    public string Label { get; init; } = null!;
    public PublicHolidayKind Kind { get; init; }
    public string KindDisplay { get; init; } = null!;
    public bool IsPaid { get; init; }
    public bool IsEstimated { get; init; }
    public string? DecreeReference { get; init; }
}

public sealed record UpsertPayrollPublicHolidayDto
{
    public int Year { get; init; }
    public DateTime Date { get; init; }
    public string Label { get; init; } = null!;
    public PublicHolidayKind Kind { get; init; }
    public bool IsPaid { get; init; } = true;
    public bool IsEstimated { get; init; }
    public string? DecreeReference { get; init; }
}

public sealed record SeedPayrollPublicHolidaysDto
{
    public IReadOnlyList<int> Years { get; init; } = Array.Empty<int>();
    public bool OverwriteExisting { get; init; }
}

public sealed record SeedPayrollPublicHolidaysResultDto
{
    public int InsertedCount { get; init; }
    public int SkippedCount { get; init; }
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
    public decimal Pending { get; init; }
    public decimal Available { get; init; }
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

// ─────────────────────────────── Bordereau CNSS mensuel ───────────────────────────────

public sealed record CnssContributionRemittanceLineDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccident { get; init; }
    public decimal LineTotal { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record CnssContributionRemittanceDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string EmployerCompanyName { get; init; } = null!;
    public string EmployerNif { get; init; } = null!;
    public string? EmployerCnssNumber { get; init; }
    public string? EmployerAddressLine { get; init; }
    public Guid? PayrollRunId { get; init; }
    public int? SourceRunStatus { get; init; }
    public bool IsEligible { get; init; }
    public bool HasExistingPayment { get; init; }
    public int? PaymentStatus { get; init; }
    public string? PaymentStatusDisplay { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalDue { get; init; }
    public int EmployeeCount { get; init; }
    public string DocumentReference { get; init; } = null!;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CnssContributionRemittanceLineDto> Lines { get; init; } = Array.Empty<CnssContributionRemittanceLineDto>();
    public CnssContributionPaymentDto? Payment { get; init; }
}

public sealed record CnssContributionPaymentDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public Guid PayrollRunId { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalDue { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDate { get; init; }
    public string Method { get; init; } = null!;
    public string MethodDisplay { get; init; } = null!;
    public Guid? BankAccountId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public bool IsCancelled { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed record RecordCnssContributionPaymentRequest
{
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime PaymentDate { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public Guid? BankAccountId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

public sealed record CancelCnssContributionPaymentRequest
{
    public string Reason { get; init; } = null!;
}

// ─────────────────────────────── Certificats de retenue à la source (IRPP/CSS) ───────────────────────────────

public sealed record PayrollWithholdingCertificateMonthDto
{
    public int Month { get; init; }
    public string MonthLabel { get; init; } = null!;
    public decimal MonthlyNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    public decimal IrppRegularization { get; init; }
    public decimal Css { get; init; }
    public decimal CssRegularization { get; init; }
    public bool HasPayslip { get; init; }
}

public sealed record PayrollWithholdingCertificateLineDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? AddressLine { get; init; }
    public bool IsHeadOfFamily { get; init; }
    public int MonthsCount { get; init; }
    public bool IsPartialYear { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalProfessionalExpenses { get; init; }
    public decimal TotalFamilyDeductions { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal TotalIrppWithheld { get; init; }
    public decimal TotalCssWithheld { get; init; }
    public decimal TotalWithholding { get; init; }
    public decimal TotalIrppSmigExemption { get; init; }
    public string DocumentReference { get; init; } = null!;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PayrollWithholdingCertificateMonthDto> Months { get; init; } = Array.Empty<PayrollWithholdingCertificateMonthDto>();
}

public sealed record PayrollWithholdingCertificateBatchDto
{
    public int Year { get; init; }
    public string EmployerCompanyName { get; init; } = null!;
    public string EmployerNif { get; init; } = null!;
    public string? EmployerAddressLine { get; init; }
    public int EmployeeCount { get; init; }
    public IReadOnlyList<int> IncludedMonths { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> MissingMonths { get; init; } = Array.Empty<int>();
    public bool IsComplete { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalAnnualNetTaxable { get; init; }
    public decimal TotalIrppWithheld { get; init; }
    public decimal TotalCssWithheld { get; init; }
    public decimal TotalWithholding { get; init; }
    public IReadOnlyList<PayrollWithholdingCertificateLineDto> Lines { get; init; } = Array.Empty<PayrollWithholdingCertificateLineDto>();
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

// ─────────────────────────────── États de contrôle (livre de paie, journal de paie) ───────────────────────────────

/// <summary>
/// Ligne du livre de paie : cumuls d'un salarié sur la plage de mois demandée.
/// Les montants proviennent des bulletins gelés — aucun recalcul.
/// </summary>
public sealed record PayrollBookLineDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? Category { get; init; }
    public string? Echelon { get; init; }
    /// <summary>Null si la fiche salarié a été supprimée depuis (le bulletin, lui, reste).</summary>
    public DateTime? HireDate { get; init; }
    /// <summary>Nombre de mois de la plage pour lesquels le salarié a un bulletin.</summary>
    public int MonthsCount { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal ProfessionalExpenses { get; init; }
    public decimal FamilyDeductions { get; init; }
    public decimal NetTaxable { get; init; }
    public decimal Irpp { get; init; }
    /// <summary>Régularisation IRPP annuelle cumulée (signée : + rappel, − restitution).</summary>
    public decimal IrppRegularization { get; init; }
    public decimal Css { get; init; }
    /// <summary>Régularisation CSS annuelle cumulée (même convention de signe).</summary>
    public decimal CssRegularization { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }
    public decimal NetSalary { get; init; }
}

/// <summary>
/// Livre de paie simplifié : registre des salaires par salarié sur une plage de mois d'un exercice.
/// </summary>
public sealed record PayrollBookDto
{
    public int Year { get; init; }
    public int FromMonth { get; init; }
    public int ToMonth { get; init; }
    /// <summary>Libellé lisible de la période, ex. « Janvier à Mars 2026 ».</summary>
    public string PeriodLabel { get; init; } = null!;
    /// <summary>True si les cycles seulement calculés ont été demandés.</summary>
    public bool IncludeCalculated { get; init; }
    /// <summary>Mois de la plage effectivement pris en compte.</summary>
    public IReadOnlyList<int> IncludedMonths { get; init; } = Array.Empty<int>();
    /// <summary>Mois de la plage sans cycle éligible.</summary>
    public IReadOnlyList<int> MissingMonths { get; init; } = Array.Empty<int>();
    /// <summary>Mois inclus alors que leur cycle n'est que calculé (non validé).</summary>
    public IReadOnlyList<int> ProvisionalMonths { get; init; } = Array.Empty<int>();
    /// <summary>True dès qu'un mois provisoire entre dans l'état : l'édition n'est pas définitive.</summary>
    public bool IsProvisional { get; init; }
    public int EmployeeCount { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalProfessionalExpenses { get; init; }
    public decimal TotalFamilyDeductions { get; init; }
    public decimal TotalNetTaxable { get; init; }
    public decimal TotalIrpp { get; init; }
    public decimal TotalIrppRegularization { get; init; }
    public decimal TotalCss { get; init; }
    public decimal TotalCssRegularization { get; init; }
    public decimal TotalOtherDeductions { get; init; }
    public decimal TotalNonTaxableAllowances { get; init; }
    public decimal TotalNetSalary { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalTfp { get; init; }
    public decimal TotalFoprolos { get; init; }
    public decimal TotalCssEmployer { get; init; }
    /// <summary>CNSS patronale + accident de travail + TFP + FOPROLOS + CSS patronale.</summary>
    public decimal TotalEmployerCharges { get; init; }
    /// <summary>Brut + charges patronales (coût employeur de la période).</summary>
    public decimal TotalEmployerCost { get; init; }
    public IReadOnlyList<PayrollBookLineDto> Lines { get; init; } = Array.Empty<PayrollBookLineDto>();
}

/// <summary>Ligne « par salarié » du journal de paie mensuel (toutes rubriques du bulletin gelé).</summary>
public sealed record PayrollJournalEmployeeLineDto
{
    public Guid PayslipId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal ProfessionalExpenses { get; init; }
    public decimal FamilyDeductions { get; init; }
    public decimal MonthlyNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    /// <summary>Régularisation IRPP annuelle du bulletin (signée : + rappel, − restitution).</summary>
    public decimal IrppRegularization { get; init; }
    public decimal Css { get; init; }
    /// <summary>Régularisation CSS annuelle du bulletin (même convention de signe).</summary>
    public decimal CssRegularization { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }
    public decimal NetSalary { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }
    public decimal CssEmployer { get; init; }
    /// <summary>CNSS patronale + accident de travail + TFP + FOPROLOS + CSS patronale.</summary>
    public decimal TotalEmployerCharges { get; init; }
    /// <summary>Brut + charges patronales (coût employeur du salarié sur le mois).</summary>
    public decimal TotalCost { get; init; }
}

/// <summary>Ligne de la ventilation comptable du journal de paie (écriture OD agrégée).</summary>
public sealed record PayrollJournalAccountingLineDto
{
    public string AccountNumber { get; init; } = null!;
    public string AccountLabel { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
}

/// <summary>
/// Journal de paie d'un mois : vue par salarié (rubriques) et ventilation comptable OD,
/// avec contrôle d'équilibre débit / crédit.
/// </summary>
public sealed record PayrollJournalDto
{
    public Guid PayrollRunId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    /// <summary>Libellé lisible de la période, ex. « Mars 2026 ».</summary>
    public string PeriodLabel { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    /// <summary>True si le cycle n'est que calculé : l'édition n'est pas définitive.</summary>
    public bool IsProvisional { get; init; }
    public int EmployeeCount { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalProfessionalExpenses { get; init; }
    public decimal TotalFamilyDeductions { get; init; }
    public decimal TotalNetTaxable { get; init; }
    public decimal TotalIrpp { get; init; }
    public decimal TotalIrppRegularization { get; init; }
    public decimal TotalCss { get; init; }
    public decimal TotalCssRegularization { get; init; }
    public decimal TotalOtherDeductions { get; init; }
    public decimal TotalNonTaxableAllowances { get; init; }
    public decimal TotalNetSalary { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalTfp { get; init; }
    public decimal TotalFoprolos { get; init; }
    public decimal TotalCssEmployer { get; init; }
    public decimal TotalEmployerCharges { get; init; }
    public decimal TotalEmployerCost { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    /// <summary>False signale une incohérence des totaux figés du cycle — anomalie à investiguer.</summary>
    public bool IsBalanced { get; init; }
    /// <summary>
    /// True quand la ventilation reprend l'écriture réellement comptabilisée ; false quand elle
    /// est simulée (cycle pas encore comptabilisé, ou plan comptable absent).
    /// </summary>
    public bool AccountingLinesArePosted { get; init; }
    /// <summary>Numéro de l'écriture comptable de paie si le cycle a déjà été comptabilisé.</summary>
    public int? AccountingEntryNumber { get; init; }
    public DateTime? AccountingEntryDate { get; init; }
    public string? AccountingJournalCode { get; init; }
    public IReadOnlyList<PayrollJournalEmployeeLineDto> Lines { get; init; } = Array.Empty<PayrollJournalEmployeeLineDto>();
    public IReadOnlyList<PayrollJournalAccountingLineDto> AccountingLines { get; init; } = Array.Empty<PayrollJournalAccountingLineDto>();
}

/// <summary>Fichier produit par l'export d'un état de contrôle paie.</summary>
public sealed record PayrollReportFileDto(byte[] Content, string FileName, string ContentType);

// ─────────────────────────────── Termination settlements ───────────────────────────────

public sealed record TerminationSettlementDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime TerminationDate { get; init; }
    public string Reason { get; init; } = null!;
    public string ReasonDisplay { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public int SeniorityMonths { get; init; }
    public decimal GrossMonthlyReference { get; init; }
    public decimal LegalIndemnityAmount { get; init; }
    public decimal NoticeIndemnityAmount { get; init; }
    public decimal UnusedLeaveAmount { get; init; }
    public decimal OtherIndemnityAmount { get; init; }
    public decimal TotalIndemnityAmount { get; init; }
    public string? Notes { get; init; }
}

public sealed record TerminationSettlementPreviewDto
{
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public DateTime TerminationDate { get; init; }
    public string Reason { get; init; } = null!;
    public string ReasonDisplay { get; init; } = null!;
    public int SeniorityMonths { get; init; }
    public int IndemnityDays { get; init; }
    public decimal DailyRate { get; init; }
    public decimal GrossMonthlyReference { get; init; }
    public decimal LegalIndemnityAmount { get; init; }
    public decimal NoticeIndemnityAmount { get; init; }
    public decimal UnusedLeaveAmount { get; init; }
    public decimal OtherIndemnityAmount { get; init; }
    public decimal TotalIndemnityAmount { get; init; }
}

public sealed record UpsertTerminationSettlementDto
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime TerminationDate { get; init; }
    public string Reason { get; init; } = "Dismissal";
    public decimal? LegalIndemnityAmount { get; init; }
    public decimal NoticeIndemnityAmount { get; init; }
    public decimal UnusedLeaveAmount { get; init; }
    public decimal OtherIndemnityAmount { get; init; }
    public string? Notes { get; init; }
    public bool Approve { get; init; }
}

// ─────────────────────────────── Annual bonuses ───────────────────────────────

public sealed record AnnualBonusRuleDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string Kind { get; init; } = null!;
    public string KindDisplay { get; init; } = null!;
    public string Formula { get; init; } = null!;
    public string FormulaDisplay { get; init; } = null!;
    public int PaymentMonth { get; init; }
    public decimal FixedAmount { get; init; }
    public decimal RatePercent { get; init; }
    public decimal MonthsOfBase { get; init; }
    public bool Taxable { get; init; }
    public bool SubjectToCnss { get; init; }
    public bool IsActive { get; init; }
    public int? FiscalYear { get; init; }
}

public sealed record UpsertAnnualBonusRuleDto
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string Kind { get; init; } = "ThirteenthMonth";
    public string Formula { get; init; } = "MonthsOfBase";
    public int PaymentMonth { get; init; } = 12;
    public decimal FixedAmount { get; init; }
    public decimal RatePercent { get; init; }
    public decimal MonthsOfBase { get; init; } = 1m;
    public bool Taxable { get; init; } = true;
    public bool SubjectToCnss { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public int? FiscalYear { get; init; }
}

public sealed record EmployeeAnnualBonusRuleDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public Guid AnnualBonusRuleId { get; init; }
    public string? RuleLabel { get; init; }
    public bool IsActive { get; init; }
    public decimal? OverrideFixedAmount { get; init; }
    public decimal? OverrideRatePercent { get; init; }
    public decimal? OverrideMonthsOfBase { get; init; }
}

public sealed record UpsertEmployeeAnnualBonusRuleDto
{
    public Guid EmployeeId { get; init; }
    public Guid AnnualBonusRuleId { get; init; }
    public bool IsActive { get; init; } = true;
    public decimal? OverrideFixedAmount { get; init; }
    public decimal? OverrideRatePercent { get; init; }
    public decimal? OverrideMonthsOfBase { get; init; }
}

// ─────────────────────────────── CIVP ───────────────────────────────

public sealed record CivpAttestationDto
{
    public string EmployerCompanyName { get; init; } = null!;
    public string? EmployerNif { get; init; }
    public string? EmployerAddressLine { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? JobTitle { get; init; }
    public DateTime CivpStartDate { get; init; }
    public DateTime CivpEndDate { get; init; }
    public decimal CivpStateGrant { get; init; }
    public decimal CivpEmployerAllowance { get; init; }
    public string? AnetiReference { get; init; }
    public string DocumentReference { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
}
