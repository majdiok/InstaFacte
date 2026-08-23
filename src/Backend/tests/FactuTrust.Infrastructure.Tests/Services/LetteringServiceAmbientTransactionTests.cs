using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Vérifie que le lettrage manuel rejoint la transaction ambiante (TenantUnitOfWork) et peut
/// lettrer des lignes d'écriture non encore commitées — scénario critique du paiement paie.
/// SQL Server requis (LocalDB éphémère) : InMemory ignore les transactions.
/// </summary>
public sealed class LetteringServiceAmbientTransactionTests : IDisposable
{
    private readonly string? _connectionString;
    private readonly bool _canRun;
    private readonly TenantAmbientTransaction _ambient = new();
    private readonly TenantDbContextFactory? _factory;
    private readonly TenantUnitOfWork? _unitOfWork;
    private readonly LetteringService? _lettering;

    public LetteringServiceAmbientTransactionTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            var dbName = $"FactuTrust_Lettering_{Guid.NewGuid():N}";
            _connectionString = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        _canRun = CanConnectAndCreate(_connectionString);
        if (!_canRun)
            return;

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(c => c.ConnectionString).Returns(_connectionString);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        _factory = new TenantDbContextFactory(
            tenantContext.Object,
            new Mock<IMediator>().Object,
            _ambient,
            NullLogger<TenantDbContext>.Instance,
            NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build(),
            hostEnvironment.Object);
        _unitOfWork = new TenantUnitOfWork(_factory, _ambient, NullLogger<TenantUnitOfWork>.Instance);
        _lettering = new LetteringService(_factory, _ambient);
    }

    [Fact]
    public async Task ManualLetterAsync_InsideUnitOfWork_LettersUncommittedLines()
    {
        if (!_canRun) return;

        var payroll421LineId = Guid.Empty;
        var payment421LineId = Guid.Empty;

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            await using var ctx = _factory!.CreateContext();
            var period = AccountingPeriod.Create(2026, 8, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
            ctx.AccountingPeriods.Add(period);

            var employeeId = Guid.NewGuid();
            var payrollEntry = JournalEntry.Create(
                1, "JOD", new DateTime(2026, 8, 1), "Engagement paie",
                period.Id, false, "PayrollRun", Guid.NewGuid(),
                new[]
                {
                    new JournalLineInput("640", "Charges", 1151.733m, 0m, null, ThirdPartyKind.None),
                    new JournalLineInput("4250001", "Salaire", 0m, 1151.733m, employeeId, ThirdPartyKind.Employee)
                }).Value;
            payrollEntry.SetAuditInfo("test", false);

            var paymentEntry = JournalEntry.Create(
                1, "JB", new DateTime(2026, 8, 5), "Paiement paie",
                period.Id, true, "PayrollPayment", Guid.NewGuid(),
                new[]
                {
                    new JournalLineInput("4250001", "Paiement", 1151.733m, 0m, employeeId, ThirdPartyKind.Employee),
                    new JournalLineInput("5321", "Banque", 0m, 1151.733m, null, ThirdPartyKind.None)
                }).Value;
            paymentEntry.SetAuditInfo("test", false);

            ctx.JournalEntries.AddRange(payrollEntry, paymentEntry);
            await ctx.SaveChangesAsync(ct);

            payroll421LineId = payrollEntry.Lines.Single(l => l.AccountNumber.StartsWith("425")).Id;
            payment421LineId = paymentEntry.Lines.Single(l => l.AccountNumber.StartsWith("425")).Id;

            return await _lettering!.ManualLetterAsync(new[] { payroll421LineId, payment421LineId }, cancellationToken: ct);
        });

        Assert.True(result.IsSuccess);

        await using var verify = _factory!.CreateIsolatedContext();
        var payrollCode = await verify.JournalEntryLines.AsNoTracking()
            .Where(l => l.Id == payroll421LineId)
            .Select(l => l.LetteringCode)
            .SingleAsync();
        var paymentCode = await verify.JournalEntryLines.AsNoTracking()
            .Where(l => l.Id == payment421LineId)
            .Select(l => l.LetteringCode)
            .SingleAsync();

        Assert.NotNull(payrollCode);
        Assert.Equal(payrollCode, paymentCode);
        Assert.True(await verify.LetteringGroups.AsNoTracking().AnyAsync(g => g.Code == payrollCode));
    }

    [Fact]
    public async Task ManualLetterAsync_InsideUnitOfWork_FailureRollsBackLettering()
    {
        if (!_canRun) return;

        var payroll421LineId = Guid.Empty;
        var payment421LineId = Guid.Empty;

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            await using var ctx = _factory!.CreateContext();
            var period = AccountingPeriod.Create(2026, 8, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
            ctx.AccountingPeriods.Add(period);

            var employeeId = Guid.NewGuid();
            var payrollEntry = JournalEntry.Create(
                1, "JOD", new DateTime(2026, 8, 1), "Engagement",
                period.Id, false, "PayrollRun", Guid.NewGuid(),
                new[]
                {
                    new JournalLineInput("640", "Charges", 100m, 0m, null, ThirdPartyKind.None),
                    new JournalLineInput("4250001", "Salaire", 0m, 100m, employeeId, ThirdPartyKind.Employee)
                }).Value;
            payrollEntry.SetAuditInfo("test", false);

            var paymentEntry = JournalEntry.Create(
                1, "JB", new DateTime(2026, 8, 5), "Paiement",
                period.Id, true, "PayrollPayment", Guid.NewGuid(),
                new[]
                {
                    new JournalLineInput("4250001", "Paiement", 100m, 0m, employeeId, ThirdPartyKind.Employee),
                    new JournalLineInput("5321", "Banque", 0m, 100m, null, ThirdPartyKind.None)
                }).Value;
            paymentEntry.SetAuditInfo("test", false);

            ctx.JournalEntries.AddRange(payrollEntry, paymentEntry);
            await ctx.SaveChangesAsync(ct);

            payroll421LineId = payrollEntry.Lines.Single(l => l.AccountNumber.StartsWith("425")).Id;
            payment421LineId = paymentEntry.Lines.Single(l => l.AccountNumber.StartsWith("425")).Id;

            var letterResult = await _lettering!.ManualLetterAsync(new[] { payroll421LineId, payment421LineId }, cancellationToken: ct);
            if (letterResult.IsFailure)
                return letterResult;

            return Result.Failure(Error.Validation("Test", "rollback simulé après lettrage"));
        });

        Assert.True(result.IsFailure);

        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(0, await verify.LetteringGroups.CountAsync());
        Assert.Equal(0, await verify.JournalEntries.CountAsync());
    }

    private bool CanConnectAndCreate(string connectionString)
    {
        try
        {
            using var context = NewRawContext(connectionString);
            context.Database.EnsureCreated();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TenantDbContext NewRawContext(string connectionString)
        => new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    public void Dispose()
    {
        if (!_canRun || string.IsNullOrWhiteSpace(_connectionString))
            return;

        try
        {
            using var context = NewRawContext(_connectionString);
            context.Database.EnsureDeleted();
        }
        catch
        {
            // Nettoyage best-effort.
        }
    }
}
