using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Loans;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Registre des emprunts : création (avec génération de l'échéancier persisté), unicité du numéro,
/// et restitution du tableau d'amortissement. Le contrôle structurant est l'articulation
/// <c>IsSettled</c> : Σ capital == capital emprunté et solde final nul, après aller-retour en base.
/// </summary>
public sealed class LoanFeatureTests
{
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

    private readonly TestTenantDbContextFactory _factory = new($"LoanDb_{Guid.NewGuid()}");

    private LoanRepository Repo() => new(_factory);

    private CreateLoanCommandHandler CreateHandler() =>
        new(Repo(), new Mock<IAuditService>().Object);

    private static CreateLoanRequest Request(
        string? number = null,
        decimal principal = 120_000m,
        int count = 12,
        int periodicity = (int)LoanPeriodicity.Monthly,
        int method = (int)LoanAmortizationMethod.ConstantAnnuity)
        => new()
        {
            LoanNumber = number,
            Label = "Crédit d'investissement",
            LenderName = "Banque de Tunisie",
            Principal = principal,
            AnnualRatePercent = 7.2m,
            StartDate = new DateTime(2026, 1, 31),
            InstallmentCount = count,
            Periodicity = periodicity,
            Method = method,
            LoanAccountNumber = "164",
            InterestAccountNumber = "651",
            BankAccountNumber = "532"
        };

    [Fact]
    public async Task Create_PersistsLoanAndItsSchedule()
    {
        var result = await CreateHandler().Handle(new CreateLoanCommand(Request()), CancellationToken.None);

        Assert.True(result.IsSuccess);

        using var ctx = _factory.CreateContext();
        var loan = await ctx.Loans.Include(l => l.ScheduleLines).SingleAsync();
        Assert.Equal(12, loan.ScheduleLines.Count);
        Assert.Equal(120_000m, loan.Principal);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public async Task Create_GeneratesSequentialNumber_WhenNotSupplied()
    {
        var handler = CreateHandler();
        await handler.Handle(new CreateLoanCommand(Request()), CancellationToken.None);
        await handler.Handle(new CreateLoanCommand(Request()), CancellationToken.None);

        using var ctx = _factory.CreateContext();
        var numbers = await ctx.Loans.Select(l => l.LoanNumber).OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { "EMP-2026-001", "EMP-2026-002" }, numbers);
    }

    [Fact]
    public async Task Create_DuplicateNumber_IsRejected()
    {
        var handler = CreateHandler();
        var first = await handler.Handle(new CreateLoanCommand(Request(number: "EMP-X")), CancellationToken.None);
        var second = await handler.Handle(new CreateLoanCommand(Request(number: "EMP-X")), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task Create_InvalidPrincipal_IsRejected()
    {
        var result = await CreateHandler().Handle(
            new CreateLoanCommand(Request(principal: 0m)), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Create_UnknownPeriodicity_IsRejected()
    {
        var result = await CreateHandler().Handle(
            new CreateLoanCommand(Request(periodicity: 99)), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    // ── Articulation : l'échéancier persisté se solde exactement ────────────────

    [Theory]
    [InlineData((int)LoanAmortizationMethod.ConstantAnnuity, (int)LoanPeriodicity.Monthly, 24)]
    [InlineData((int)LoanAmortizationMethod.ConstantPrincipal, (int)LoanPeriodicity.Quarterly, 8)]
    public async Task Schedule_RoundTripsThroughDatabase_AndIsSettled(int method, int periodicity, int count)
    {
        var created = await CreateHandler().Handle(
            new CreateLoanCommand(Request(principal: 87_654.321m, count: count, periodicity: periodicity, method: method)),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var schedule = await new GetLoanScheduleQueryHandler(Repo())
            .Handle(new GetLoanScheduleQuery(created.Value), CancellationToken.None);

        Assert.True(schedule.IsSuccess);
        Assert.Equal(count, schedule.Value.Lines.Count);
        Assert.Equal(87_654.321m, schedule.Value.TotalPrincipal);        // égalité EXACTE après aller-retour
        Assert.Equal(0m, schedule.Value.Lines[^1].ClosingBalance);
        Assert.True(schedule.Value.IsSettled);
        Assert.Equal(schedule.Value.TotalPrincipal + schedule.Value.TotalInterest, schedule.Value.TotalInstallments);
    }

    [Fact]
    public async Task Schedule_UnknownLoan_IsNotFound()
    {
        var result = await new GetLoanScheduleQueryHandler(Repo())
            .Handle(new GetLoanScheduleQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    // ── Registre paginé ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_PaginatesAndFilters()
    {
        var handler = CreateHandler();
        await handler.Handle(new CreateLoanCommand(Request(number: "EMP-A")), CancellationToken.None);
        await handler.Handle(new CreateLoanCommand(Request(number: "EMP-B")), CancellationToken.None);

        var all = await new GetLoansQueryHandler(Repo())
            .Handle(new GetLoansQuery(1, 10, null, null), CancellationToken.None);
        var filtered = await new GetLoansQueryHandler(Repo())
            .Handle(new GetLoansQuery(1, 10, "EMP-A", null), CancellationToken.None);

        Assert.Equal(2, all.Value.TotalCount);
        Assert.Equal(1, filtered.Value.TotalCount);
        Assert.Equal("EMP-A", filtered.Value.Items[0].LoanNumber);
    }
}
