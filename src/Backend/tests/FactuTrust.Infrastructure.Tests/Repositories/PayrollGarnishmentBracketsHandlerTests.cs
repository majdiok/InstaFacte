using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Queries;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class PayrollGarnishmentBracketsHandlerTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly PayrollParametersRepository _repository;

    public PayrollGarnishmentBracketsHandlerTests()
    {
        _databaseName = $"PayrollGarnishmentHandlerTest_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new PayrollParametersRepository(_contextFactory);
    }

    [Fact]
    public async Task GetPayrollGarnishmentBracketsQuery_returns_seeded_brackets_for_2026()
    {
        var handler = new GetPayrollGarnishmentBracketsQueryHandler(_repository);
        var result = await handler.Handle(new GetPayrollGarnishmentBracketsQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(0m, result.Value[0].LowerBoundMonthlyNet);
        // R-11 : borne indexée sur le SMIG 2026 (554,736, décret n° 2026-67).
        Assert.Equal(554.736m, result.Value[1].LowerBoundMonthlyNet);
        Assert.Equal(0.333m, result.Value[1].SeizableFraction);
    }

    [Fact]
    public async Task UpdatePayrollGarnishmentBracketsCommand_persists_updated_brackets()
    {
        var updateHandler = new UpdatePayrollGarnishmentBracketsCommandHandler(_repository);
        var dto = new UpdatePayrollGarnishmentBracketsDto
        {
            Brackets =
            [
                new PayrollGarnishmentBracketDto { LowerBoundMonthlyNet = 0m, SeizableFraction = 0m },
                new PayrollGarnishmentBracketDto { LowerBoundMonthlyNet = 600m, SeizableFraction = 0.25m }
            ]
        };

        var updateResult = await updateHandler.Handle(
            new UpdatePayrollGarnishmentBracketsCommand(2026, dto),
            CancellationToken.None);
        Assert.True(updateResult.IsSuccess);

        var getHandler = new GetPayrollGarnishmentBracketsQueryHandler(_repository);
        var getResult = await getHandler.Handle(new GetPayrollGarnishmentBracketsQuery(2026), CancellationToken.None);

        Assert.True(getResult.IsSuccess);
        Assert.Equal(2, getResult.Value.Count);
        Assert.Equal(600m, getResult.Value[1].LowerBoundMonthlyNet);
        Assert.Equal(0.25m, getResult.Value[1].SeizableFraction);
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
