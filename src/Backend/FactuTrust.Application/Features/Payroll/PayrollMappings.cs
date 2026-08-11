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
    public static EmployeeListDto ToListDto(Employee e, string parentClaimsStatus = "None")
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
            IsActive = e.IsActive,
            ParentClaimsStatus = parentClaimsStatus
        };
    }

    public static EmployeeDetailDto ToDetailDto(
        Employee e,
        IReadOnlyList<EmployeeDependentParent>? dependentParentClaims = null)
    {
        var claims = dependentParentClaims ?? Array.Empty<EmployeeDependentParent>();
        var eligibility = ParentDeductionEligibilityResolver.ResolveForEmployee(
            e.DependentParents,
            claims,
            claims.ToDictionary(c => c.ParentCin, c => e.Id, StringComparer.Ordinal),
            e.Id);

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
            DependentParentClaims = claims.Select(DependentParentClaimsHelper.ToDto).ToList(),
            ParentClaimsStatus = ParentDeductionEligibilityResolver.ResolveStatusLabel(eligibility.Status),
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
            CivpStartDate = c.CivpStartDate,
            CivpEndDate = c.CivpEndDate,
            CivpStateGrant = c.CivpStateGrant,
            CivpEmployerAllowance = c.CivpEmployerAllowance,
            AnetiReference = c.AnetiReference,
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
            TotalIrppSmigExemption = r.TotalIrppSmigExemption,
            TotalNet = r.TotalNet,
            TotalCnssEmployer = r.TotalCnssEmployer,
            TotalTfp = r.TotalTfp,
            TotalFoprolos = r.TotalFoprolos,
            TotalCssEmployer = r.TotalCssEmployer,
            TotalWorkAccident = r.TotalWorkAccident,
            TotalOtherDeductions = r.TotalOtherDeductions,
            CalculatedAt = r.CalculatedAt,
            ValidatedAt = r.ValidatedAt,
            ValidatedBy = r.ValidatedBy,
            ClosedAt = r.ClosedAt,
            TotalPaid = r.TotalPaid,
            RemainingToPay = r.RemainingToPay,
            PaymentStatus = r.PaymentStatus.ToString(),
            PaymentStatusDisplay = r.PaymentStatus.ToDisplayString(),
            HasPayments = r.HasPayments,
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
            IrppSmigExemption = p.IrppSmigExemption,
            NetSalary = p.NetSalary,
            PaidAmount = p.PaidAmount,
            RemainingToPay = p.RemainingToPay,
            PaymentStatus = p.PaymentStatus.ToString(),
            PaymentStatusDisplay = p.PaymentStatus.ToDisplayString(),
            PaidAt = p.PaidAt
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
            IrppBeforeSmigExemption = p.IrppBeforeSmigExemption,
            IrppSmigExemption = p.IrppSmigExemption,
            Css = p.Css,
            OtherDeductions = p.OtherDeductions,
            NonTaxableAllowances = p.NonTaxableAllowances,
            NetSalary = p.NetSalary,
            ProrataWorkedDays = p.ProrataWorkedDays,
            ProrataNonWorkedDays = p.ProrataNonWorkedDays,
            ProrataDeductionAmount = p.ProrataDeductionAmount,
            CnssEmployer = p.CnssEmployer,
            WorkAccidentContribution = p.WorkAccidentContribution,
            Tfp = p.Tfp,
            Foprolos = p.Foprolos,
            CssEmployer = p.CssEmployer,
            PaidAmount = p.PaidAmount,
            RemainingToPay = p.RemainingToPay,
            PaymentStatus = p.PaymentStatus.ToString(),
            PaymentStatusDisplay = p.PaymentStatus.ToDisplayString(),
            PaidAt = p.PaidAt,
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
            EnableIrppRegularization = p.EnableIrppRegularization,
            EnableAutomaticProrata = p.EnableAutomaticProrata,
            CssRate = p.CssRate,
            CssAnnualExemptionThreshold = p.CssAnnualExemptionThreshold,
            CssEmployerRate = p.CssEmployerRate,
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
            SmigIrppExemptionMode = p.SmigIrppExemptionMode.ToString(),
            SmigIrppExemptionModeDisplay = p.SmigIrppExemptionMode.ToDisplayString(),
            SmigIrppExemptionRateOverride = p.SmigIrppExemptionRateOverride,
            IrppBrackets = p.IrppBrackets
                .OrderBy(b => b.LowerBound)
                .Select(b => new IrppBracketDto { LowerBound = b.LowerBound, Rate = b.Rate })
                .ToList(),
            CnssMonthlyCeiling = p.CnssMonthlyCeiling,
            CnssDailyCeiling = p.CnssDailyCeiling,
            CssMonthlyCeiling = p.CssMonthlyCeiling,
            AccidentWorkMonthlyCeiling = p.AccidentWorkMonthlyCeiling,
            SickLeaveWaitingDays = p.SickLeaveWaitingDays,
            SickLeaveIjRatePercent = p.SickLeaveIjRatePercent,
            MaternityLeaveDurationDays = p.MaternityLeaveDurationDays,
            PaternityLeaveDurationDays = p.PaternityLeaveDurationDays,
            MaternityEmployerTopUpDefault = p.MaternityEmployerTopUpDefault
        };
    }

    public static IReadOnlyList<PayrollGarnishmentBracketDto> ToGarnishmentBracketDtos(PayrollYearParameters p) =>
        p.GarnishmentBrackets
            .OrderBy(b => b.LowerBoundMonthlyNet)
            .Select(b => new PayrollGarnishmentBracketDto
            {
                LowerBoundMonthlyNet = b.LowerBoundMonthlyNet,
                SeizableFraction = b.SeizableFraction
            })
            .ToList();

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
            ApprovedAt = l.ApprovedAt,
            MedicalCertificateNumber = l.MedicalCertificateNumber,
            MedicalCertificateDate = l.MedicalCertificateDate,
            SubrogationEnabled = l.SubrogationEnabled,
            EmployerTopUpPercent = l.EmployerTopUpPercent,
            EmployerTopUpDays = l.EmployerTopUpDays,
            ExpectedBirthDate = l.ExpectedBirthDate,
            ActualBirthDate = l.ActualBirthDate,
            ChildBirthCertificateNumber = l.ChildBirthCertificateNumber
        };
    }

    public static CnssIjClaimDto ToDto(CnssIjClaim claim, string? employeeName = null) =>
        new()
        {
            Id = claim.Id,
            EmployeeId = claim.EmployeeId,
            EmployeeName = employeeName,
            LeaveRequestId = claim.LeaveRequestId,
            Year = claim.Year,
            Month = claim.Month,
            Amount = claim.Amount,
            Status = claim.Status.ToString(),
            StatusDisplay = claim.Status.ToDisplayString(),
            PaidAt = claim.PaidAt
        };

    public static EmployeePayrollSuspensionDto ToDto(EmployeePayrollSuspension s) =>
        new()
        {
            Id = s.Id,
            EmployeeId = s.EmployeeId,
            Type = s.Type.ToString(),
            TypeDisplay = s.Type.ToDisplayString(),
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            IsPaid = s.IsPaid,
            Reason = s.Reason,
            IsApproved = s.IsApproved,
            ApprovedAt = s.ApprovedAt
        };

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

    /// <summary>Libellés courts des mois, pour le tableau « Détail du calcul ».</summary>
    private static readonly string[] MonthLabels =
    {
        "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    public static string ToMonthLabel(int month) =>
        month is >= 1 and <= 12 ? MonthLabels[month - 1] : month.ToString();

    public static IrppRegularizationDto ToIrppRegularizationDto(
        PayrollIrppRegularization regularization,
        string? employeeName = null,
        string? employeeNumber = null,
        IReadOnlyList<IrppRegularizationMonthDto>? months = null)
    {
        return new IrppRegularizationDto
        {
            Id = regularization.Id,
            EmployeeId = regularization.EmployeeId,
            EmployeeName = employeeName,
            EmployeeNumber = employeeNumber,
            Year = regularization.Year,
            Month = regularization.Month,
            Reason = (int)regularization.Reason,
            ReasonLabel = regularization.Reason.ToDisplayString(),
            MonthsCounted = regularization.MonthsCounted,
            CumulNetTaxable = regularization.CumulNetTaxable,
            CumulIrppWithheld = regularization.CumulIrppWithheld,
            CumulCssWithheld = regularization.CumulCssWithheld,
            IrppDue = regularization.IrppDue,
            CssDue = regularization.CssDue,
            ComputedIrppDelta = regularization.ComputedIrppDelta,
            ComputedCssDelta = regularization.ComputedCssDelta,
            OverrideIrppDelta = regularization.OverrideIrppDelta,
            OverrideCssDelta = regularization.OverrideCssDelta,
            IsOverridden = regularization.IsOverridden,
            EffectiveIrppDelta = regularization.EffectiveIrppDelta,
            EffectiveCssDelta = regularization.EffectiveCssDelta,
            EffectiveTotalDelta = regularization.EffectiveTotalDelta,
            IsAdditionalWithholding = regularization.EffectiveTotalDelta > 0,
            Notes = regularization.Notes,
            Months = months ?? Array.Empty<IrppRegularizationMonthDto>()
        };
    }

    public static PayrollVariableAllowanceLineDto ToVariableAllowanceDto(PayrollVariableAllowanceLine line, string? employeeName = null)
    {
        return new PayrollVariableAllowanceLineDto
        {
            Id = line.Id,
            EmployeeId = line.EmployeeId,
            EmployeeName = employeeName,
            Year = line.Year,
            Month = line.Month,
            Label = line.Label,
            Amount = line.Amount,
            Taxable = line.Taxable,
            SubjectToCnss = line.SubjectToCnss,
            Source = line.Source.ToString(),
            AnnualBonusRuleId = line.AnnualBonusRuleId
        };
    }

    public static SocialFundSchemeDto ToSocialFundSchemeDto(SocialFundScheme scheme) => new()
    {
        Id = scheme.Id,
        Code = scheme.Code,
        Name = scheme.Name,
        IsActive = scheme.IsActive,
        EmployeeRatePercent = scheme.EmployeeRatePercent,
        EmployerRatePercent = scheme.EmployerRatePercent,
        Base = scheme.Base,
        FixedEmployeeAmount = scheme.FixedEmployeeAmount,
        FixedEmployerAmount = scheme.FixedEmployerAmount,
        MonthlyEmployeeCap = scheme.MonthlyEmployeeCap,
        EmployeeAccountSce = scheme.EmployeeAccountSce,
        EmployerAccountSce = scheme.EmployerAccountSce,
        EffectiveFrom = scheme.EffectiveFrom,
        EffectiveTo = scheme.EffectiveTo
    };

    public static EmployeeSocialFundEnrollmentDto ToSocialFundEnrollmentDto(
        EmployeeSocialFundEnrollment enrollment,
        string? employeeName = null,
        string? schemeName = null) => new()
    {
        Id = enrollment.Id,
        EmployeeId = enrollment.EmployeeId,
        EmployeeName = employeeName,
        SocialFundSchemeId = enrollment.SocialFundSchemeId,
        SchemeName = schemeName,
        StartDate = enrollment.StartDate,
        EndDate = enrollment.EndDate,
        OverrideEmployeeAmount = enrollment.OverrideEmployeeAmount,
        OverrideEmployerAmount = enrollment.OverrideEmployerAmount
    };

    public static PayrollMealVoucherLineDto ToMealVoucherDto(PayrollMealVoucherLine line, string? employeeName = null) => new()
    {
        Id = line.Id,
        EmployeeId = line.EmployeeId,
        EmployeeName = employeeName,
        Year = line.Year,
        Month = line.Month,
        Days = line.Days,
        FaceValue = line.FaceValue,
        EmployerContributionRate = line.EmployerContributionRate,
        TotalValue = line.TotalValue,
        EmployerContribution = line.EmployerContribution,
        EmployeeContribution = line.EmployeeContribution
    };

    public static EmployeeInKindBenefitDto ToInKindBenefitDto(EmployeeInKindBenefit benefit, string? employeeName = null) => new()
    {
        Id = benefit.Id,
        EmployeeId = benefit.EmployeeId,
        EmployeeName = employeeName,
        Type = benefit.Type,
        Label = benefit.Label,
        MonthlyValue = benefit.MonthlyValue,
        StartDate = benefit.StartDate,
        EndDate = benefit.EndDate,
        Description = benefit.Description
    };

    public static EmployeeLoanDto ToEmployeeLoanDto(EmployeeLoan loan, string? employeeName = null) => new()
    {
        Id = loan.Id,
        EmployeeId = loan.EmployeeId,
        EmployeeName = employeeName,
        Reference = loan.Reference,
        Principal = loan.Principal,
        InstallmentCount = loan.InstallmentCount,
        MonthlyInstallmentAmount = loan.MonthlyInstallmentAmount,
        StartYear = loan.StartYear,
        StartMonth = loan.StartMonth,
        Notes = loan.Notes,
        Status = loan.Status,
        RemainingBalance = loan.RemainingBalance,
        Installments = loan.Installments.Select(i => new EmployeeLoanInstallmentDto
        {
            Id = i.Id,
            SequenceNumber = i.SequenceNumber,
            Year = i.Year,
            Month = i.Month,
            Amount = i.Amount,
            IsSettled = i.IsSettled,
            SettledInPayrollRunId = i.SettledInPayrollRunId
        }).ToList()
    };

    public static EmployeeGarnishmentDto ToEmployeeGarnishmentDto(EmployeeGarnishment garnishment, string? employeeName = null) => new()
    {
        Id = garnishment.Id,
        EmployeeId = garnishment.EmployeeId,
        EmployeeName = employeeName,
        Type = garnishment.Type,
        Reference = garnishment.Reference,
        IssuedAt = garnishment.IssuedAt,
        BeneficiaryName = garnishment.BeneficiaryName,
        BeneficiaryRib = garnishment.BeneficiaryRib,
        Priority = garnishment.Priority,
        Kind = garnishment.Kind,
        FixedAmount = garnishment.FixedAmount,
        PercentOfNet = garnishment.PercentOfNet,
        TotalAmountDue = garnishment.TotalAmountDue,
        StartDate = garnishment.StartDate,
        EndDate = garnishment.EndDate,
        Status = garnishment.Status,
        TotalApplied = garnishment.TotalApplied,
        Installments = garnishment.Installments.Select(i => new EmployeeGarnishmentInstallmentDto
        {
            Id = i.Id,
            Year = i.Year,
            Month = i.Month,
            PayrollRunId = i.PayrollRunId,
            RequestedAmount = i.RequestedAmount,
            AppliedAmount = i.AppliedAmount,
            CarriedOverAmount = i.CarriedOverAmount
        }).ToList()
    };

    public static PayrollPublicHolidayDto ToPublicHolidayDto(PayrollPublicHoliday holiday) => new()
    {
        Id = holiday.Id,
        Year = holiday.Year,
        Date = holiday.Date,
        Label = holiday.Label,
        Kind = holiday.Kind,
        KindDisplay = holiday.Kind.ToDisplayString(),
        IsPaid = holiday.IsPaid,
        IsEstimated = holiday.IsEstimated,
        DecreeReference = holiday.DecreeReference
    };
}
