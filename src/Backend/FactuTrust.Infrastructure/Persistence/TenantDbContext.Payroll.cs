using FactuTrust.Domain.Entities.Payroll;
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
        ConfigureEmployeeAdvance(builder);
        ConfigurePayrollOvertimeLine(builder);
        ConfigureLeaveBalanceAccrual(builder);
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
            entity.Property(r => r.TotalWorkAccident).HasPrecision(18, 3);
            entity.Property(r => r.TotalOtherDeductions).HasPrecision(18, 3);

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
            entity.Property(p => p.OtherDeductions).HasPrecision(18, 3);
            entity.Property(p => p.NonTaxableAllowances).HasPrecision(18, 3);
            entity.Property(p => p.NetSalary).HasPrecision(18, 3);
            entity.Property(p => p.CnssEmployer).HasPrecision(18, 3);
            entity.Property(p => p.WorkAccidentContribution).HasPrecision(18, 3);
            entity.Property(p => p.Tfp).HasPrecision(18, 3);
            entity.Property(p => p.Foprolos).HasPrecision(18, 3);
            entity.Property(p => p.AppliedCnssEmployeeRate).HasPrecision(8, 4);
            entity.Property(p => p.AppliedCnssEmployerRate).HasPrecision(8, 4);

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
            entity.Property(p => p.CssRate).HasPrecision(8, 4);
            entity.Property(p => p.CssAnnualExemptionThreshold).HasPrecision(18, 3);
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

            entity.HasMany(p => p.IrppBrackets)
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
}
