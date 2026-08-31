using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class PayrollParametersRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly PayrollParametersRepository _repository;

    public PayrollParametersRepositoryTests()
    {
        _databaseName = $"PayrollParamsTest_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new PayrollParametersRepository(_contextFactory);
    }

    [Fact]
    public async Task UpdateAsync_replaces_irpp_brackets_with_valid_parent_fk()
    {
        var parameters = await _repository.GetOrCreateForYearAsync(2026);
        var parentId = parameters.Id;
        Assert.NotEqual(Guid.Empty, parentId);

        var newBrackets = new[]
        {
            PayrollIrppBracket.Create(0m, 0m),
            PayrollIrppBracket.Create(5000m, 16m),
            PayrollIrppBracket.Create(10000m, 26m)
        };

        var replaceResult = parameters.ReplaceIrppBrackets(newBrackets);
        Assert.True(replaceResult.IsSuccess);
        foreach (var bracket in parameters.IrppBrackets)
            Assert.Equal(parentId, bracket.PayrollYearParametersId);

        await _repository.UpdateAsync(parameters);

        await using var verifyContext = _contextFactory.CreateContext();
        var storedBrackets = await verifyContext.PayrollIrppBrackets
            .Where(b => b.PayrollYearParametersId == parentId)
            .OrderBy(b => b.LowerBound)
            .ToListAsync();

        Assert.Equal(3, storedBrackets.Count);
        Assert.All(storedBrackets, b => Assert.Equal(parentId, b.PayrollYearParametersId));
        Assert.Equal(16m, storedBrackets[1].Rate);
    }

    [Fact]
    public async Task UpdateAsync_scalar_rates_persisted_and_barème_unchanged()
    {
        var parameters = await _repository.GetOrCreateForYearAsync(2027);
        var originalBracketCount = parameters.IrppBrackets.Count;
        var originalFirstRate = parameters.IrppBrackets.OrderBy(b => b.LowerBound).First().Rate;

        var ratesResult = parameters.UpdateRates(
            cnssEmployeeRate: 9.50m,
            cnssEmployerRate: parameters.CnssEmployerRate,
            cssRate: parameters.CssRate,
            cssAnnualExemptionThreshold: parameters.CssAnnualExemptionThreshold,
            professionalExpensesRate: parameters.ProfessionalExpensesRate,
            professionalExpensesAnnualCap: parameters.ProfessionalExpensesAnnualCap,
            headOfFamilyAnnualDeduction: parameters.HeadOfFamilyAnnualDeduction,
            childAnnualDeduction: parameters.ChildAnnualDeduction,
            maxDeductibleChildren: parameters.MaxDeductibleChildren,
            tfpRateIndustry: parameters.TfpRateIndustry,
            tfpRateOther: parameters.TfpRateOther,
            foprolosRate: parameters.FoprolosRate,
            monthlySmig: parameters.MonthlySmig,
            cnssEmployeeRateRsa: parameters.CnssEmployeeRateRsa,
            cnssEmployerRateRsa: parameters.CnssEmployerRateRsa,
            enforceSmigOnContracts: parameters.EnforceSmigOnContracts,
            enableExtendedOvertimeRates: parameters.EnableExtendedOvertimeRates,
            enableAllowanceQuadrantMatrix: parameters.EnableAllowanceQuadrantMatrix,
            studentChildAnnualDeduction: parameters.StudentChildAnnualDeduction,
            disabledChildAnnualDeduction: parameters.DisabledChildAnnualDeduction,
            parentDeductionRatePercent: parameters.ParentDeductionRatePercent,
            parentAnnualDeductionCap: parameters.ParentAnnualDeductionCap,
            isIndustrialSector: parameters.IsIndustrialSector,
            cssEmployerRate: 0.5m);
        Assert.True(ratesResult.IsSuccess);

        await _repository.UpdateAsync(parameters);

        var reloaded = await _repository.GetByFiscalYearAsync(2027);
        Assert.NotNull(reloaded);
        Assert.Equal(9.50m, reloaded.CnssEmployeeRate);
        Assert.Equal(0.5m, reloaded.CssEmployerRate);
        Assert.Equal(originalBracketCount, reloaded.IrppBrackets.Count);
        Assert.Equal(originalFirstRate, reloaded.IrppBrackets.OrderBy(b => b.LowerBound).First().Rate);
    }

    [Fact]
    public void ReplaceIrppBrackets_assigns_parent_id()
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var parentId = parameters.Id;
        Assert.NotEqual(Guid.Empty, parentId);

        var brackets = new[]
        {
            PayrollIrppBracket.Create(0m, 0m),
            PayrollIrppBracket.Create(5000m, 15m)
        };

        var result = parameters.ReplaceIrppBrackets(brackets);

        Assert.True(result.IsSuccess);
        Assert.All(parameters.IrppBrackets, b => Assert.Equal(parentId, b.PayrollYearParametersId));
    }

    [Fact]
    public async Task GetOrCreateForYearAsync_seeds_garnishment_brackets_for_2026()
    {
        var parameters = await _repository.GetOrCreateForYearAsync(2026);

        Assert.NotEmpty(parameters.GarnishmentBrackets);
        var ordered = parameters.GarnishmentBrackets.OrderBy(b => b.LowerBoundMonthlyNet).ToList();
        Assert.Equal(0m, ordered[0].LowerBoundMonthlyNet);
        Assert.Equal(0m, ordered[0].SeizableFraction);
        // R-11 : bornes indexées sur le SMIG de l'exercice (554,736 en 2026, décret n° 2026-67).
        Assert.Equal(554.736m, ordered[1].LowerBoundMonthlyNet);
        Assert.Equal(0.333m, ordered[1].SeizableFraction);
    }

    [Fact]
    public async Task UpdateAsync_replaces_garnishment_brackets_with_valid_parent_fk()
    {
        var parameters = await _repository.GetOrCreateForYearAsync(2026);
        var parentId = parameters.Id;

        var newBrackets = new[]
        {
            PayrollGarnishmentBracket.Create(0m, 0m),
            PayrollGarnishmentBracket.Create(500m, 0.10m),
            PayrollGarnishmentBracket.Create(1000m, 0.33m)
        };

        var replaceResult = parameters.ReplaceGarnishmentBrackets(newBrackets);
        Assert.True(replaceResult.IsSuccess);
        Assert.All(parameters.GarnishmentBrackets, b => Assert.Equal(parentId, b.PayrollYearParametersId));

        await _repository.UpdateAsync(parameters);

        var reloaded = await _repository.GetByFiscalYearAsync(2026);
        Assert.NotNull(reloaded);
        var stored = reloaded.GarnishmentBrackets.OrderBy(b => b.LowerBoundMonthlyNet).ToList();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, b => Assert.Equal(parentId, b.PayrollYearParametersId));
        Assert.Equal(0.10m, stored[1].SeizableFraction);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            return new TenantDbContext(options);
        }
    }
}
