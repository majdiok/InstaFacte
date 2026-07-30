using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll;

/// <summary>
/// Manual entity → DTO mapping for the payroll module (no AutoMapper — project convention).
/// </summary>
public static class PayrollMappings
{
    public static EmployeeListDto ToListDto(Employee e)
    {
        var contract = e.GetActiveContract(DateTime.UtcNow) ?? e.Contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
        return new EmployeeListDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            LastName = e.LastName,
            FullName = e.FullName,
            CnssNumber = e.CnssNumber,
            JobTitle = contract?.JobTitle,
            CurrentBaseSalary = contract?.BaseSalary,
            CurrentWeeklyRegime = contract?.WeeklyRegime.ToString(),
            HireDate = e.HireDate,
            IsActive = e.IsActive
        };
    }

    public static EmployeeDetailDto ToDetailDto(Employee e)
    {
        return new EmployeeDetailDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            LastName = e.LastName,
            FullName = e.FullName,
            Cin = e.Cin,
            CnssNumber = e.CnssNumber,
            Category = e.Category,
            Echelon = e.Echelon,
            DateOfBirth = e.DateOfBirth,
            HireDate = e.HireDate,
            TerminationDate = e.TerminationDate,
            MaritalStatus = e.MaritalStatus.ToString(),
            MaritalStatusDisplay = e.MaritalStatus.ToDisplayString(),
            IsHeadOfFamily = e.IsHeadOfFamily,
            DependentChildren = e.DependentChildren,
            StudentChildren = e.StudentChildren,
            DisabledChildren = e.DisabledChildren,
            DependentParents = e.DependentParents,
            Address = e.Address is null ? null : new AddressDto
            {
                Street = e.Address.Street,
                StreetLine2 = e.Address.StreetLine2,
                City = e.Address.City,
                PostalCode = e.Address.PostalCode,
                Governorate = e.Address.Governorate,
                Country = e.Address.Country,
                FullAddress = e.Address.ToSingleLine()
            },
            Email = e.Email?.Value,
            Phone = e.Phone?.Value,
            Rib = e.Rib,
            IsActive = e.IsActive,
            Contracts = e.Contracts
                .OrderByDescending(c => c.StartDate)
                .Select(ToContractDto)
                .ToList()
        };
    }

    public static EmploymentContractDto ToContractDto(EmploymentContract c)
    {
        return new EmploymentContractDto
        {
            Id = c.Id,
            Type = c.Type.ToString(),
            TypeDisplay = c.Type.ToDisplayString(),
            Regime = c.Regime.ToString(),
            RegimeDisplay = c.Regime.ToDisplayString(),
            WeeklyRegime = c.WeeklyRegime.ToString(),
            WeeklyRegimeDisplay = c.WeeklyRegime.ToDisplayString(),
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            BaseSalary = c.BaseSalary,
            WorkAccidentRate = c.WorkAccidentRate,
            JobTitle = c.JobTitle,
            IsActive = c.IsActive,
            Allowances = c.Allowances.Select(a => new ContractAllowanceDto
            {
                Id = a.Id,
                Label = a.Label,
                Amount = a.Amount,
                Taxable = a.Taxable,
                SubjectToCnss = a.SubjectToCnss
            }).ToList()
        };
    }

    public static PayrollRunListDto ToListDto(PayrollRun r, int payslipCount)
    {
        return new PayrollRunListDto
        {
            Id = r.Id,
            Year = r.Year,
            Month = r.Month,
            Label = r.Label,
            Status = r.Status.ToString(),
            StatusDisplay = r.Status.ToDisplayString(),
            PayslipCount = payslipCount,
            TotalGross = r.TotalGross,
            TotalNet = r.TotalNet,
            ValidatedAt = r.ValidatedAt,
            ClosedAt = r.ClosedAt
        };
    }

    public static PayrollRunDetailDto ToDetailDto(PayrollRun r)
    {
        return new PayrollRunDetailDto
        {
            Id = r.Id,
            Year = r.Year,
            Month = r.Month,
            Label = r.Label,
            Status = r.Status.ToString(),
            StatusDisplay = r.Status.ToDisplayString(),
            ParametersFiscalYear = r.ParametersFiscalYear,
            TotalGross = r.TotalGross,
            TotalCnssEmployee = r.TotalCnssEmployee,
            TotalIrpp = r.TotalIrpp,
            TotalCss = r.TotalCss,
            TotalNet = r.TotalNet,
            TotalCnssEmployer = r.TotalCnssEmployer,
            TotalTfp = r.TotalTfp,
            TotalFoprolos = r.TotalFoprolos,
            TotalWorkAccident = r.TotalWorkAccident,
            TotalOtherDeductions = r.TotalOtherDeductions,
            CalculatedAt = r.CalculatedAt,
            ValidatedAt = r.ValidatedAt,
            ValidatedBy = r.ValidatedBy,
            ClosedAt = r.ClosedAt,
            Payslips = r.Payslips
                .OrderBy(p => p.EmployeeName)
                .Select(ToPayslipListDto)
                .ToList()
        };
    }

    public static PayslipListDto ToPayslipListDto(Payslip p)
    {
        return new PayslipListDto
        {
            Id = p.Id,
            EmployeeId = p.EmployeeId,
            EmployeeName = p.EmployeeName,
            EmployeeNumber = p.EmployeeNumber,
            GrossSalary = p.GrossSalary,
            CnssEmployee = p.CnssEmployee,
            Irpp = p.Irpp,
            Css = p.Css,
            NetSalary = p.NetSalary
        };
    }

    public static PayslipDetailDto ToPayslipDetailDto(Payslip p)
    {
        return new PayslipDetailDto
        {
            Id = p.Id,
            PayrollRunId = p.PayrollRunId,
            EmployeeId = p.EmployeeId,
            EmployeeName = p.EmployeeName,
            EmployeeNumber = p.EmployeeNumber,
            CnssNumber = p.CnssNumber,
            Year = p.Year,
            Month = p.Month,
            GrossSalary = p.GrossSalary,
            CnssableGross = p.CnssableGross,
            CnssEmployee = p.CnssEmployee,
            TaxableBaseAfterCnss = p.TaxableBaseAfterCnss,
            ProfessionalExpenses = p.ProfessionalExpenses,
            FamilyDeductions = p.FamilyDeductions,
            MonthlyNetTaxable = p.MonthlyNetTaxable,
            AnnualNetTaxable = p.AnnualNetTaxable,
            Irpp = p.Irpp,
            Css = p.Css,
            OtherDeductions = p.OtherDeductions,
            NonTaxableAllowances = p.NonTaxableAllowances,
            NetSalary = p.NetSalary,
            CnssEmployer = p.CnssEmployer,
            WorkAccidentContribution = p.WorkAccidentContribution,
            Tfp = p.Tfp,
            Foprolos = p.Foprolos,
            Lines = p.Lines
                .OrderBy(l => l.Order)
                .Select(l => new PayslipLineDto
                {
                    Order = l.Order,
                    Label = l.Label,
                    Kind = l.Kind.ToString(),
                    KindDisplay = l.Kind.ToDisplayString(),
                    Base = l.Base,
                    Rate = l.Rate,
                    Amount = l.Amount
                }).ToList()
        };
    }

    public static PayrollParametersDto ToDto(PayrollYearParameters p)
    {
        return new PayrollParametersDto
        {
            Id = p.Id,
            FiscalYear = p.FiscalYear,
            CnssEmployeeRate = p.CnssEmployeeRate,
            CnssEmployerRate = p.CnssEmployerRate,
            CnssEmployeeRateRsa = p.CnssEmployeeRateRsa,
            CnssEmployerRateRsa = p.CnssEmployerRateRsa,
            EnforceSmigOnContracts = p.EnforceSmigOnContracts,
            EnableExtendedOvertimeRates = p.EnableExtendedOvertimeRates,
            EnableAllowanceQuadrantMatrix = p.EnableAllowanceQuadrantMatrix,
            CssRate = p.CssRate,
            CssAnnualExemptionThreshold = p.CssAnnualExemptionThreshold,
            ProfessionalExpensesRate = p.ProfessionalExpensesRate,
            ProfessionalExpensesAnnualCap = p.ProfessionalExpensesAnnualCap,
            HeadOfFamilyAnnualDeduction = p.HeadOfFamilyAnnualDeduction,
            ChildAnnualDeduction = p.ChildAnnualDeduction,
            MaxDeductibleChildren = p.MaxDeductibleChildren,
            StudentChildAnnualDeduction = p.StudentChildAnnualDeduction,
            DisabledChildAnnualDeduction = p.DisabledChildAnnualDeduction,
            ParentDeductionRatePercent = p.ParentDeductionRatePercent,
            ParentAnnualDeductionCap = p.ParentAnnualDeductionCap,
            IsIndustrialSector = p.IsIndustrialSector,
            TfpRateIndustry = p.TfpRateIndustry,
            TfpRateOther = p.TfpRateOther,
            FoprolosRate = p.FoprolosRate,
            MonthlySmig = p.MonthlySmig,
            IrppBrackets = p.IrppBrackets
                .OrderBy(b => b.LowerBound)
                .Select(b => new IrppBracketDto { LowerBound = b.LowerBound, Rate = b.Rate })
                .ToList()
        };
    }

    public static LeaveRequestDto ToDto(LeaveRequest l, string? employeeName = null)
    {
        return new LeaveRequestDto
        {
            Id = l.Id,
            EmployeeId = l.EmployeeId,
            EmployeeName = employeeName,
            Type = l.Type.ToString(),
            TypeDisplay = l.Type.ToDisplayString(),
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            Days = l.Days,
            Reason = l.Reason,
            IsApproved = l.IsApproved,
            ApprovedAt = l.ApprovedAt
        };
    }

    public static EmployeeAdvanceDto ToDto(EmployeeAdvance a, string? employeeName = null)
    {
        return new EmployeeAdvanceDto
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            EmployeeName = employeeName,
            Date = a.Date,
            Amount = a.Amount,
            Reason = a.Reason,
            IsSettled = a.IsSettled
        };
    }

    public static PayrollOvertimeLineDto ToOvertimeDto(PayrollOvertimeLine line, string? employeeName = null)
    {
        return new PayrollOvertimeLineDto
        {
            Id = line.Id,
            EmployeeId = line.EmployeeId,
            EmployeeName = employeeName,
            Year = line.Year,
            Month = line.Month,
            Hours = line.Hours,
            RatePercent = line.RatePercent,
            RatePercentDisplay = OvertimeRatePercentExtensions.ToDisplayString(line.RatePercent),
            ComputedAmount = line.ComputedAmount,
            OverrideAmount = line.OverrideAmount,
            IsOverridden = line.IsOverridden,
            EffectiveAmount = line.EffectiveAmount
        };
    }
}
