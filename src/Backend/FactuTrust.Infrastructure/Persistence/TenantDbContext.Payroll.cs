using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the Payroll module (RH &amp; Paie) entities.
/// Kept in a partial file so the module stays self-contained: removing the payroll feature
/// only requires deleting this file, the corresponding DbSets, and the call to ConfigurePayroll.
/// All monetary amounts use decimal(18,3) (millimes, TND).
/// </summary>
public partial class TenantDbContext
{
    private static void ConfigurePayroll(ModelBuilder builder)
    {
        ConfigureEmployee(builder);
        ConfigureEmploymentContract(builder);
        ConfigureContractAllowance(builder);
        ConfigurePayrollRun(builder);
        ConfigurePayslip(builder);
        ConfigurePayslipLine(builder);
        ConfigurePayrollYearParameters(builder);
        ConfigurePayrollIrppBracket(builder);
        ConfigureLeaveRequest(builder);
        ConfigureEmployeePayrollSuspension(builder);
        ConfigureEmployeeAdvance(builder);
        ConfigurePayrollOvertimeLine(builder);
        ConfigurePayrollVariableAllowanceLine(builder);
        ConfigurePayrollIrppRegularization(builder);
        ConfigureLeaveBalanceAccrual(builder);
        ConfigurePayrollPayment(builder);
        ConfigurePayrollPaymentLine(builder);
        ConfigureCnssContributionPayment(builder);
        ConfigureSocialFundScheme(builder);
        ConfigureEmployeeSocialFundEnrollment(builder);
        ConfigurePayrollMealVoucherLine(builder);
        ConfigureEmployeeInKindBenefit(builder);
        ConfigureEmployeeLoan(builder);
        ConfigureEmployeeGarnishment(builder);
        ConfigurePayrollGarnishmentBracket(builder);
        ConfigureEmployeeDependentParent(builder);
    }


    private static void ConfigureEmployee(ModelBuilder builder)
    {
        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Cin).HasMaxLength(20);
            entity.Property(e => e.CnssNumber).HasMaxLength(30);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Echelon).HasMaxLength(50);
            entity.Property(e => e.Rib).HasMaxLength(40);
            entity.Property(e => e.MaritalStatus).IsRequired();
            entity.Property(e => e.IsHeadOfFamily).IsRequired();
            entity.Property(e => e.DependentChildren).IsRequired();
            entity.Property(e => e.StudentChildren).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DisabledChildren).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DependentParents).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.HireDate).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.LeaveOpeningBalanceDays).HasPrecision(8, 3);

            entity.OwnsOne(e => e.Address, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("AddressStreet").HasMaxLength(200);
                addr.Property(a => a.StreetLine2).HasColumnName("AddressStreetLine2").HasMaxLength(200);
                addr.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(100);
                addr.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(10);
                addr.Property(a => a.Governorate).HasColumnName("AddressGovernorate").HasMaxLength(100);
                addr.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(100);
            });

            entity.OwnsOne(e => e.Email, em =>
            {
                em.Property(p => p.Value).HasColumnName("Email").HasMaxLength(256);
            });

            entity.OwnsOne(e => e.Phone, ph =>
            {
                ph.Property(p => p.Value).HasColumnName("Phone").HasMaxLength(20);
                ph.Property(p => p.CountryCode).HasColumnName("PhoneCountryCode").HasMaxLength(5);
                ph.Property(p => p.LocalNumber).HasColumnName("PhoneLocalNumber").HasMaxLength(15);
            });

            entity.HasMany(e => e.Contracts)
                  .WithOne()
                  .HasForeignKey(c => c.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.EmployeeNumber).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CnssNumber);
        });
    }

    private static void ConfigureEmploymentContract(ModelBuilder builder)
    {
        builder.Entity<EmploymentContract>(entity =>
        {
            entity.ToTable("EmploymentContracts");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.EmployeeId).IsRequired();
            entity.Property(c => c.Type).IsRequired();
            entity.Property(c => c.Regime).IsRequired();
            entity.Property(c => c.WeeklyRegime).IsRequired().HasDefaultValue(Domain.Enums.WeeklyWorkRegime.FortyEightHours);
            entity.Property(c => c.StartDate).IsRequired();
            entity.Property(c => c.EndDate);
            entity.Property(c => c.BaseSalary).HasPrecision(18, 3);
            entity.Property(c => c.WorkAccidentRate).HasPrecision(8, 4);
            entity.Property(c => c.JobTitle).HasMaxLength(150);
            entity.Property(c => c.IsActive).IsRequired();

            entity.HasMany(c => c.Allowances)
                  .WithOne()
                  .HasForeignKey(a => a.ContractId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.EmployeeId);
        });
    }

    private static void ConfigureContractAllowance(ModelBuilder builder)
    {
        builder.Entity<ContractAllowance>(entity =>
        {
            entity.ToTable("ContractAllowances");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ContractId).IsRequired();
            entity.Property(a => a.Label).HasMaxLength(150).IsRequired();
            entity.Property(a => a.Amount).HasPrecision(18, 3);
            entity.Property(a => a.Taxable).IsRequired();
            entity.Property(a => a.SubjectToCnss).IsRequired();

            entity.HasIndex(a => a.ContractId);
        });
    }

    private static void ConfigurePayrollRun(ModelBuilder builder)
    {
        builder.Entity<PayrollRun>(entity =>
        {
            entity.ToTable("PayrollRuns");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Year).IsRequired();
            entity.Property(r => r.Month).IsRequired();
            entity.Property(r => r.Label).HasMaxLength(150).IsRequired();
            entity.Property(r => r.Status).IsRequired();
            entity.Property(r => r.ParametersFiscalYear).IsRequired();
            entity.Property(r => r.ValidatedBy).HasMaxLength(450);

            entity.Property(r => r.TotalGross).HasPrecision(18, 3);
            entity.Property(r => r.TotalCnssEmployee).HasPrecision(18, 3);
            entity.Property(r => r.TotalIrpp).HasPrecision(18, 3);
            entity.Property(r => r.TotalCss).HasPrecision(18, 3);
            entity.Property(r => r.TotalNet).HasPrecision(18, 3);
            entity.Property(r => r.TotalCnssEmployer).HasPrecision(18, 3);
            entity.Property(r => r.TotalTfp).HasPrecision(18, 3);
            entity.Property(r => r.TotalFoprolos).HasPrecision(18, 3);
            entity.Property(r => r.TotalCssEmployer).HasPrecision(18, 3);
            entity.Property(r => r.TotalWorkAccident).HasPrecision(18, 3);
            entity.Property(r => r.TotalOtherDeductions).HasPrecision(18, 3);
            entity.Property(r => r.TotalIrppRegularization).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(r => r.TotalCssRegularization).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(r => r.TotalIrppSmigExemption).HasPrecision(18, 3).HasDefaultValue(0m);

            entity.HasMany(r => r.Payslips)
                  .WithOne()
                  .HasForeignKey(p => p.PayrollRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.Year, r.Month }).IsUnique();
            entity.HasIndex(r => r.Status);
        });
    }

    private static void ConfigurePayslip(ModelBuilder builder)
    {
        builder.Entity<Payslip>(entity =>
        {
            entity.ToTable("Payslips");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.PayrollRunId).IsRequired();
            entity.Property(p => p.EmployeeId).IsRequired();
            entity.Property(p => p.EmployeeName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(p => p.CnssNumber).HasMaxLength(30);
            entity.Property(p => p.Year).IsRequired();
            entity.Property(p => p.Month).IsRequired();

            entity.Property(p => p.GrossSalary).HasPrecision(18, 3);
            entity.Property(p => p.CnssableGross).HasPrecision(18, 3);
            entity.Property(p => p.CnssEmployee).HasPrecision(18, 3);
            entity.Property(p => p.TaxableBaseAfterCnss).HasPrecision(18, 3);
            entity.Property(p => p.ProfessionalExpenses).HasPrecision(18, 3);
            entity.Property(p => p.FamilyDeductions).HasPrecision(18, 3);
            entity.Property(p => p.MonthlyNetTaxable).HasPrecision(18, 3);
            entity.Property(p => p.AnnualNetTaxable).HasPrecision(18, 3);
            entity.Property(p => p.Irpp).HasPrecision(18, 3);
            entity.Property(p => p.Css).HasPrecision(18, 3);
            entity.Property(p => p.IrppBeforeSmigExemption).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.IrppSmigExemption).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.OtherDeductions).HasPrecision(18, 3);
            entity.Property(p => p.NonTaxableAllowances).HasPrecision(18, 3);
            // Régularisation annuelle : défaut 0 pour que les bulletins déjà émis conservent
            // exactement leur montant (colonnes ajoutées après coup).
            entity.Property(p => p.IrppRegularization).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.CssRegularization).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.RegularizationDeferred).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.NetSalary).HasPrecision(18, 3);
            entity.Property(p => p.ProrataWorkedDays).HasPrecision(6, 2).HasDefaultValue(0m);
            entity.Property(p => p.ProrataNonWorkedDays).HasPrecision(6, 2).HasDefaultValue(0m);
            entity.Property(p => p.ProrataDeductionAmount).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.CnssEmployer).HasPrecision(18, 3);
            entity.Property(p => p.WorkAccidentContribution).HasPrecision(18, 3);
            entity.Property(p => p.Tfp).HasPrecision(18, 3);
            entity.Property(p => p.Foprolos).HasPrecision(18, 3);
            entity.Property(p => p.CssEmployer).HasPrecision(18, 3);
            entity.Property(p => p.AppliedCnssEmployeeRate).HasPrecision(8, 4);
            entity.Property(p => p.AppliedCnssEmployerRate).HasPrecision(8, 4);
            entity.Property(p => p.PaidAmount).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(p => p.PaidAt);
            entity.Property(p => p.EmployeeAuxiliaryAccount).HasMaxLength(10);

            entity.HasMany(p => p.Lines)
                  .WithOne()
                  .HasForeignKey(l => l.PayslipId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.PayrollRunId);
            entity.HasIndex(p => new { p.EmployeeId, p.Year, p.Month });
        });
    }

    private static void ConfigurePayslipLine(ModelBuilder builder)
    {
        builder.Entity<PayslipLine>(entity =>
        {
            entity.ToTable("PayslipLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.PayslipId).IsRequired();
            entity.Property(l => l.Order).IsRequired();
            entity.Property(l => l.Label).HasMaxLength(200).IsRequired();
            entity.Property(l => l.Kind).IsRequired();
            entity.Property(l => l.Base).HasPrecision(18, 3);
            entity.Property(l => l.Rate).HasPrecision(8, 4);
            entity.Property(l => l.Amount).HasPrecision(18, 3);
            entity.Property(l => l.DeductionKind);

            entity.HasIndex(l => l.PayslipId);
        });
    }

    private static void ConfigurePayrollYearParameters(ModelBuilder builder)
    {
        builder.Entity<PayrollYearParameters>(entity =>
        {
            entity.ToTable("PayrollYearParameters");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.FiscalYear).IsRequired();
            entity.Property(p => p.CnssEmployeeRate).HasPrecision(8, 4);
            entity.Property(p => p.CnssEmployerRate).HasPrecision(8, 4);
            entity.Property(p => p.CnssEmployeeRateRsa).HasPrecision(8, 4);
            entity.Property(p => p.CnssEmployerRateRsa).HasPrecision(8, 4);
            entity.Property(p => p.EnforceSmigOnContracts).IsRequired();
            entity.Property(p => p.EnableExtendedOvertimeRates).IsRequired();
            entity.Property(p => p.EnableAllowanceQuadrantMatrix).IsRequired();
            // Régularisation IRPP annuelle : désactivée par défaut, les exercices existants
            // conservent strictement leur calcul.
            entity.Property(p => p.EnableIrppRegularization).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.EnableAutomaticProrata).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.CssRate).HasPrecision(8, 4);
            entity.Property(p => p.CssAnnualExemptionThreshold).HasPrecision(18, 3);
            entity.Property(p => p.CssEmployerRate).HasPrecision(8, 4);
            entity.Property(p => p.ProfessionalExpensesRate).HasPrecision(8, 4);
            entity.Property(p => p.ProfessionalExpensesAnnualCap).HasPrecision(18, 3);
            entity.Property(p => p.HeadOfFamilyAnnualDeduction).HasPrecision(18, 3);
            entity.Property(p => p.ChildAnnualDeduction).HasPrecision(18, 3);
            entity.Property(p => p.MaxDeductibleChildren).IsRequired();
            // Défauts légaux (art. 40 code IRPP) pour les lignes créées avant l'ajout de ces colonnes.
            entity.Property(p => p.StudentChildAnnualDeduction).HasPrecision(18, 3).HasDefaultValue(1000m);
            entity.Property(p => p.DisabledChildAnnualDeduction).HasPrecision(18, 3).HasDefaultValue(2000m);
            entity.Property(p => p.ParentDeductionRatePercent).HasPrecision(8, 4).HasDefaultValue(5m);
            entity.Property(p => p.ParentAnnualDeductionCap).HasPrecision(18, 3).HasDefaultValue(450m);
            entity.Property(p => p.IsIndustrialSector).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.TfpRateIndustry).HasPrecision(8, 4);
            entity.Property(p => p.TfpRateOther).HasPrecision(8, 4);
            entity.Property(p => p.FoprolosRate).HasPrecision(8, 4);
            entity.Property(p => p.MonthlySmig).HasPrecision(18, 3);
            entity.Property(p => p.SmigIrppExemptionMode).IsRequired().HasDefaultValue(SmigIrppExemptionMode.None);
            entity.Property(p => p.SmigIrppExemptionRateOverride).HasPrecision(5, 2);
            entity.Property(p => p.MealVoucherDailyExemptionCap).HasPrecision(18, 3).HasDefaultValue(3.000m);

            entity.HasMany(p => p.IrppBrackets)
                  .WithOne()
                  .HasForeignKey(b => b.PayrollYearParametersId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.GarnishmentBrackets)
                  .WithOne()
                  .HasForeignKey(b => b.PayrollYearParametersId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.FiscalYear).IsUnique();
        });
    }

    private static void ConfigurePayrollIrppBracket(ModelBuilder builder)
    {
        builder.Entity<PayrollIrppBracket>(entity =>
        {
            entity.ToTable("PayrollIrppBrackets");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.PayrollYearParametersId).IsRequired();
            entity.Property(b => b.LowerBound).HasPrecision(18, 3);
            entity.Property(b => b.Rate).HasPrecision(8, 4);

            entity.HasIndex(b => b.PayrollYearParametersId);
        });
    }

    private static void ConfigureLeaveRequest(ModelBuilder builder)
    {
        builder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.Type).IsRequired();
            entity.Property(l => l.StartDate).IsRequired();
            entity.Property(l => l.EndDate).IsRequired();
            entity.Property(l => l.Days).HasPrecision(6, 2);
            entity.Property(l => l.Reason).HasMaxLength(500);
            entity.Property(l => l.IsApproved).IsRequired();
            entity.Property(l => l.ApprovedBy).HasMaxLength(450);

            entity.HasIndex(l => l.EmployeeId);
            entity.HasIndex(l => new { l.StartDate, l.EndDate });
        });
    }

    private static void ConfigureEmployeePayrollSuspension(ModelBuilder builder)
    {
        builder.Entity<EmployeePayrollSuspension>(entity =>
        {
            entity.ToTable("EmployeePayrollSuspensions");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.EmployeeId).IsRequired();
            entity.Property(s => s.Type).IsRequired();
            entity.Property(s => s.StartDate).IsRequired();
            entity.Property(s => s.IsPaid).IsRequired();
            entity.Property(s => s.Reason).HasMaxLength(500);
            entity.Property(s => s.IsApproved).IsRequired();
            entity.Property(s => s.ApprovedBy).HasMaxLength(450);

            entity.HasIndex(s => s.EmployeeId);
            entity.HasIndex(s => new { s.StartDate, s.EndDate });
        });
    }

    private static void ConfigureEmployeeAdvance(ModelBuilder builder)
    {
        builder.Entity<EmployeeAdvance>(entity =>
        {
            entity.ToTable("EmployeeAdvances");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.EmployeeId).IsRequired();
            entity.Property(a => a.Date).IsRequired();
            entity.Property(a => a.Amount).HasPrecision(18, 3);
            entity.Property(a => a.Reason).HasMaxLength(500);
            entity.Property(a => a.IsSettled).IsRequired();
            entity.Property(a => a.SettledInPayrollRunId);

            entity.HasIndex(a => new { a.EmployeeId, a.IsSettled });
        });
    }

    private static void ConfigurePayrollOvertimeLine(ModelBuilder builder)
    {
        builder.Entity<PayrollOvertimeLine>(entity =>
        {
            entity.ToTable("PayrollOvertimeLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.Year).IsRequired();
            entity.Property(l => l.Month).IsRequired();
            entity.Property(l => l.Hours).HasPrecision(8, 2);
            entity.Property(l => l.RatePercent).HasPrecision(8, 2);
            entity.Property(l => l.ComputedAmount).HasPrecision(18, 3);
            entity.Property(l => l.OverrideAmount).HasPrecision(18, 3);
            entity.Property(l => l.IsOverridden).IsRequired();

            entity.HasIndex(l => new { l.EmployeeId, l.Year, l.Month });
            entity.HasIndex(l => new { l.Year, l.Month });
        });
    }

    private static void ConfigurePayrollVariableAllowanceLine(ModelBuilder builder)
    {
        builder.Entity<PayrollVariableAllowanceLine>(entity =>
        {
            entity.ToTable("PayrollVariableAllowanceLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.Year).IsRequired();
            entity.Property(l => l.Month).IsRequired();
            entity.Property(l => l.Label).HasMaxLength(200).IsRequired();
            entity.Property(l => l.Amount).HasPrecision(18, 3);
            entity.Property(l => l.Taxable).IsRequired();
            entity.Property(l => l.SubjectToCnss).IsRequired();

            entity.HasIndex(l => new { l.EmployeeId, l.Year, l.Month });
            entity.HasIndex(l => new { l.Year, l.Month });
        });
    }

    private static void ConfigurePayrollIrppRegularization(ModelBuilder builder)
    {
        builder.Entity<PayrollIrppRegularization>(entity =>
        {
            entity.ToTable("PayrollIrppRegularizations");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.EmployeeId).IsRequired();
            entity.Property(r => r.Year).IsRequired();
            entity.Property(r => r.Month).IsRequired();
            entity.Property(r => r.Reason).IsRequired();
            entity.Property(r => r.MonthsCounted).IsRequired();

            entity.Property(r => r.CumulNetTaxable).HasPrecision(18, 3);
            entity.Property(r => r.CumulIrppWithheld).HasPrecision(18, 3);
            entity.Property(r => r.CumulCssWithheld).HasPrecision(18, 3);
            entity.Property(r => r.IrppDue).HasPrecision(18, 3);
            entity.Property(r => r.CssDue).HasPrecision(18, 3);
            entity.Property(r => r.ComputedIrppDelta).HasPrecision(18, 3);
            entity.Property(r => r.ComputedCssDelta).HasPrecision(18, 3);
            entity.Property(r => r.OverrideIrppDelta).HasPrecision(18, 3);
            entity.Property(r => r.OverrideCssDelta).HasPrecision(18, 3);

            entity.Property(r => r.Notes).HasMaxLength(1000);
            entity.Property(r => r.DetailJson);

            // Les propriétés calculées ne sont pas persistées : elles se déduisent des colonnes.
            entity.Ignore(r => r.IsOverridden);
            entity.Ignore(r => r.EffectiveIrppDelta);
            entity.Ignore(r => r.EffectiveCssDelta);
            entity.Ignore(r => r.EffectiveTotalDelta);
            entity.Ignore(r => r.IsNeutral);

            // Une seule régularisation par salarié et par mois : la régénération remplace.
            entity.HasIndex(r => new { r.EmployeeId, r.Year, r.Month }).IsUnique();
            entity.HasIndex(r => new { r.Year, r.Month });
        });
    }

    private static void ConfigureLeaveBalanceAccrual(ModelBuilder builder)
    {
        builder.Entity<LeaveBalanceAccrual>(entity =>
        {
            entity.ToTable("LeaveBalanceAccruals");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.EmployeeId).IsRequired();
            entity.Property(a => a.Year).IsRequired();
            entity.Property(a => a.Month).IsRequired();
            entity.Property(a => a.WorkedDays).HasPrecision(6, 2);
            entity.Property(a => a.AccruedDays).HasPrecision(8, 3);
            entity.Property(a => a.PayrollRunId).IsRequired();

            entity.HasIndex(a => new { a.EmployeeId, a.Year, a.Month }).IsUnique();
            entity.HasIndex(a => a.PayrollRunId);
        });
    }

    private static void ConfigurePayrollPayment(ModelBuilder builder)
    {
        builder.Entity<PayrollPayment>(entity =>
        {
            entity.ToTable("PayrollPayments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.PayrollRunId).IsRequired();
            entity.Property(p => p.PaymentDate).IsRequired();
            entity.Property(p => p.Method).IsRequired();
            entity.Property(p => p.Reference).HasMaxLength(100);
            entity.Property(p => p.Notes).HasMaxLength(500);
            entity.Property(p => p.IsCancelled).IsRequired();
            entity.Property(p => p.CancelledBy).HasMaxLength(450);
            entity.Property(p => p.CancellationReason).HasMaxLength(500);

            entity.OwnsOne(p => p.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 3).IsRequired();
                money.Property(m => m.Currency).HasColumnName("AmountCurrency").HasMaxLength(3).IsRequired();
            });

            entity.HasOne(p => p.PayrollRun)
                .WithMany()
                .HasForeignKey(p => p.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Lines)
                .WithOne()
                .HasForeignKey(l => l.PayrollPaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.PayrollRunId, p.IsCancelled });
            entity.HasIndex(p => p.PaymentDate);
        });
    }

    private static void ConfigurePayrollPaymentLine(ModelBuilder builder)
    {
        builder.Entity<PayrollPaymentLine>(entity =>
        {
            entity.ToTable("PayrollPaymentLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.PayrollPaymentId).IsRequired();
            entity.Property(l => l.PayslipId).IsRequired();
            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.EmployeeAuxiliaryAccount).HasMaxLength(10).IsRequired();

            entity.OwnsOne(l => l.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 3).IsRequired();
                money.Property(m => m.Currency).HasColumnName("AmountCurrency").HasMaxLength(3).IsRequired();
            });

            entity.HasIndex(l => l.PayslipId);
            entity.HasIndex(l => l.EmployeeId);
        });
    }

    private static void ConfigureCnssContributionPayment(ModelBuilder builder)
    {
        builder.Entity<CnssContributionPayment>(entity =>
        {
            entity.ToTable("CnssContributionPayments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Year).IsRequired();
            entity.Property(p => p.Month).IsRequired();
            entity.Property(p => p.PayrollRunId).IsRequired();
            entity.Property(p => p.TotalCnssEmployee).HasPrecision(18, 3).IsRequired();
            entity.Property(p => p.TotalCnssEmployer).HasPrecision(18, 3).IsRequired();
            entity.Property(p => p.TotalWorkAccident).HasPrecision(18, 3).IsRequired();
            entity.Property(p => p.TotalDue).HasPrecision(18, 3).IsRequired();
            entity.Property(p => p.PaymentDate).IsRequired();
            entity.Property(p => p.Method).IsRequired();
            entity.Property(p => p.Reference).HasMaxLength(100);
            entity.Property(p => p.Notes).HasMaxLength(500);
            entity.Property(p => p.IsCancelled).IsRequired();
            entity.Property(p => p.CancelledBy).HasMaxLength(450);
            entity.Property(p => p.CancellationReason).HasMaxLength(500);

            entity.OwnsOne(p => p.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 3).IsRequired();
                money.Property(m => m.Currency).HasColumnName("AmountCurrency").HasMaxLength(3).IsRequired();
            });

            entity.HasOne(p => p.PayrollRun)
                .WithMany()
                .HasForeignKey(p => p.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => new { p.Year, p.Month, p.IsCancelled });
            entity.HasIndex(p => p.PayrollRunId);
            entity.HasIndex(p => new { p.Year, p.Month })
                .IsUnique()
                .HasFilter("[IsCancelled] = 0");
        });
    }

    private static void ConfigureSocialFundScheme(ModelBuilder builder)
    {
        builder.Entity<SocialFundScheme>(entity =>
        {
            entity.ToTable("SocialFundSchemes");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Code).HasMaxLength(30).IsRequired();
            entity.Property(s => s.Name).HasMaxLength(150).IsRequired();
            entity.Property(s => s.IsActive).IsRequired();
            entity.Property(s => s.EmployeeRatePercent).HasPrecision(8, 4);
            entity.Property(s => s.EmployerRatePercent).HasPrecision(8, 4);
            entity.Property(s => s.Base).IsRequired();
            entity.Property(s => s.FixedEmployeeAmount).HasPrecision(18, 3);
            entity.Property(s => s.FixedEmployerAmount).HasPrecision(18, 3);
            entity.Property(s => s.MonthlyEmployeeCap).HasPrecision(18, 3);
            entity.Property(s => s.EmployeeAccountSce).HasMaxLength(20).IsRequired();
            entity.Property(s => s.EmployerAccountSce).HasMaxLength(20).IsRequired();

            entity.HasIndex(s => s.Code).IsUnique();
        });
    }

    private static void ConfigureEmployeeSocialFundEnrollment(ModelBuilder builder)
    {
        builder.Entity<EmployeeSocialFundEnrollment>(entity =>
        {
            entity.ToTable("EmployeeSocialFundEnrollments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmployeeId).IsRequired();
            entity.Property(e => e.SocialFundSchemeId).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.OverrideEmployeeAmount).HasPrecision(18, 3);
            entity.Property(e => e.OverrideEmployerAmount).HasPrecision(18, 3);

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.SocialFundSchemeId);
        });
    }

    private static void ConfigurePayrollMealVoucherLine(ModelBuilder builder)
    {
        builder.Entity<PayrollMealVoucherLine>(entity =>
        {
            entity.ToTable("PayrollMealVoucherLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.Year).IsRequired();
            entity.Property(l => l.Month).IsRequired();
            entity.Property(l => l.Days).IsRequired();
            entity.Property(l => l.FaceValue).HasPrecision(18, 3);
            entity.Property(l => l.EmployerContributionRate).HasPrecision(8, 4);

            entity.HasIndex(l => new { l.EmployeeId, l.Year, l.Month });
            entity.HasIndex(l => new { l.Year, l.Month });
        });
    }

    private static void ConfigureEmployeeInKindBenefit(ModelBuilder builder)
    {
        builder.Entity<EmployeeInKindBenefit>(entity =>
        {
            entity.ToTable("EmployeeInKindBenefits");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.EmployeeId).IsRequired();
            entity.Property(b => b.Type).IsRequired();
            entity.Property(b => b.Label).HasMaxLength(200).IsRequired();
            entity.Property(b => b.MonthlyValue).HasPrecision(18, 3);
            entity.Property(b => b.StartDate).IsRequired();
            entity.Property(b => b.Description).HasMaxLength(500);

            entity.HasIndex(b => b.EmployeeId);
        });
    }

    private static void ConfigureEmployeeLoan(ModelBuilder builder)
    {
        builder.Entity<EmployeeLoan>(entity =>
        {
            entity.ToTable("EmployeeLoans");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.EmployeeId).IsRequired();
            entity.Property(l => l.Reference).HasMaxLength(50).IsRequired();
            entity.Property(l => l.Principal).HasPrecision(18, 3);
            entity.Property(l => l.InstallmentCount).IsRequired();
            entity.Property(l => l.MonthlyInstallmentAmount).HasPrecision(18, 3);
            entity.Property(l => l.StartYear).IsRequired();
            entity.Property(l => l.StartMonth).IsRequired();
            entity.Property(l => l.Notes).HasMaxLength(500);
            entity.Property(l => l.Status).IsRequired();

            entity.HasMany(l => l.Installments)
                  .WithOne()
                  .HasForeignKey(i => i.EmployeeLoanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(l => l.Installments).HasField("_installments");

            entity.HasIndex(l => l.EmployeeId);
            entity.HasIndex(l => l.Reference);
        });

        builder.Entity<EmployeeLoanInstallment>(entity =>
        {
            entity.ToTable("EmployeeLoanInstallments");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.EmployeeLoanId).IsRequired();
            entity.Property(i => i.SequenceNumber).IsRequired();
            entity.Property(i => i.Year).IsRequired();
            entity.Property(i => i.Month).IsRequired();
            entity.Property(i => i.Amount).HasPrecision(18, 3);
            entity.Property(i => i.IsSettled).IsRequired();

            entity.HasIndex(i => i.EmployeeLoanId);
            entity.HasIndex(i => new { i.Year, i.Month, i.IsSettled });
        });
    }

    private static void ConfigureEmployeeGarnishment(ModelBuilder builder)
    {
        builder.Entity<EmployeeGarnishment>(entity =>
        {
            entity.ToTable("EmployeeGarnishments");
            entity.HasKey(g => g.Id);

            entity.Property(g => g.EmployeeId).IsRequired();
            entity.Property(g => g.Type).IsRequired();
            entity.Property(g => g.Reference).HasMaxLength(100).IsRequired();
            entity.Property(g => g.IssuedAt).IsRequired();
            entity.Property(g => g.BeneficiaryName).HasMaxLength(200).IsRequired();
            entity.Property(g => g.BeneficiaryRib).HasMaxLength(40);
            entity.Property(g => g.Priority).IsRequired();
            entity.Property(g => g.Kind).IsRequired();
            entity.Property(g => g.FixedAmount).HasPrecision(18, 3);
            entity.Property(g => g.PercentOfNet).HasPrecision(8, 4);
            entity.Property(g => g.TotalAmountDue).HasPrecision(18, 3);
            entity.Property(g => g.StartDate).IsRequired();
            entity.Property(g => g.Status).IsRequired();

            entity.HasMany(g => g.Installments)
                  .WithOne()
                  .HasForeignKey(i => i.EmployeeGarnishmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(g => g.Installments).HasField("_installments");

            entity.HasIndex(g => g.EmployeeId);
        });

        builder.Entity<EmployeeGarnishmentInstallment>(entity =>
        {
            entity.ToTable("EmployeeGarnishmentInstallments");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.EmployeeGarnishmentId).IsRequired();
            entity.Property(i => i.Year).IsRequired();
            entity.Property(i => i.Month).IsRequired();
            entity.Property(i => i.PayrollRunId).IsRequired();
            entity.Property(i => i.RequestedAmount).HasPrecision(18, 3);
            entity.Property(i => i.AppliedAmount).HasPrecision(18, 3);
            entity.Property(i => i.CarriedOverAmount).HasPrecision(18, 3);

            entity.HasIndex(i => i.EmployeeGarnishmentId);
            entity.HasIndex(i => i.PayrollRunId);
        });
    }

    private static void ConfigurePayrollGarnishmentBracket(ModelBuilder builder)
    {
        builder.Entity<PayrollGarnishmentBracket>(entity =>
        {
            entity.ToTable("PayrollGarnishmentBrackets");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.PayrollYearParametersId).IsRequired();
            entity.Property(b => b.LowerBoundMonthlyNet).HasPrecision(18, 3);
            entity.Property(b => b.SeizableFraction).HasPrecision(8, 4);

            entity.HasIndex(b => b.PayrollYearParametersId);
        });
    }

    private static void ConfigureEmployeeDependentParent(ModelBuilder builder)
    {
        builder.Entity<EmployeeDependentParent>(entity =>
        {
            entity.ToTable("EmployeeDependentParents");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.EmployeeId).IsRequired();
            entity.Property(p => p.ParentCin).HasMaxLength(20).IsRequired();
            entity.Property(p => p.Kinship).IsRequired();
            entity.Property(p => p.FirstName).HasMaxLength(100);
            entity.Property(p => p.LastName).HasMaxLength(100);
            entity.Property(p => p.StartDate).IsRequired();

            entity.HasIndex(p => p.EmployeeId);
            entity.HasIndex(p => p.ParentCin)
                .IsUnique()
                .HasFilter("[EndDate] IS NULL")
                .HasDatabaseName("IX_EmployeeDependentParents_ParentCin_Active");
        });
    }
}
