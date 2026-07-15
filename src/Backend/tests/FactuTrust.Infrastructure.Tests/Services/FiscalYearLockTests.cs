using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot H : verrouillage définitif d'un exercice — refus si période ouverte ou checklist bloquante,
/// blocage de la création d'écriture et de la réouverture après verrou, idempotence, flag off.
/// </summary>
public sealed class FiscalYearLockTests
{
    private readonly string _dbName = $"YearLockDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public FiscalYearLockTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
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

    private void SeedPeriod(int year, int month, bool closed)
    {
        var period = AccountingPeriod.Create(year, month, new DateTime(year, month, 1), new DateTime(year, month, 28));
        if (closed) period.Close("test");
        period.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
    }

    private FiscalYearLockService BuildService(bool enabled = true, bool preClosingBlocking = false)
    {
        var preClosing = new Mock<IPreClosingControlService>();
        preClosing.Setup(p => p.RunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int fy, CancellationToken _) => Result.Success(new PreClosingChecklistDto
            {
                FiscalYear = fy,
                GeneratedAt = DateTime.UtcNow,
                HasBlocking = preClosingBlocking,
                Checks = preClosingBlocking
                    ? new[] { new PreClosingCheckDto { Code = "drafts", Title = "Écritures en brouillard", Severity = 2, Count = 1, Message = "x" } }
                    : Array.Empty<PreClosingCheckDto>()
            }));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Email).Returns("compta@test.tn");
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var settings = Options.Create(new AccountingSettings { DefinitiveLockEnabled = enabled, PreClosingControlsEnabled = true });
        return new FiscalYearLockService(_factory, preClosing.Object, currentUser.Object, audit.Object, settings);
    }

    private AccountingPeriodService BuildPeriodService(bool lockEnabled = true)
    {
        var repo = new AccountingPeriodRepository(_factory);
        var settings = Options.Create(new AccountingSettings { DefinitiveLockEnabled = lockEnabled });
        return new AccountingPeriodService(repo, _factory, settings);
    }

    private bool YearLocked(int year)
    {
        using var ctx = _factory.CreateContext();
        return ctx.AccountingYearLocks.AsNoTracking().Any(l => l.FiscalYear == year);
    }

    [Fact]
    public async Task Lock_WithOpenPeriod_Fails()
    {
        SeedPeriod(2026, 1, closed: false);

        var result = await BuildService().LockYearAsync(2026);

        Assert.True(result.IsFailure);
        Assert.Contains("ne sont pas clôturées", result.Error.Description);
        Assert.False(YearLocked(2026));
    }

    [Fact]
    public async Task Lock_WithBlockingChecklist_Fails()
    {
        SeedPeriod(2026, 1, closed: true);

        var result = await BuildService(preClosingBlocking: true).LockYearAsync(2026);

        Assert.True(result.IsFailure);
        Assert.Contains("bloquants", result.Error.Description);
        Assert.False(YearLocked(2026));
    }

    [Fact]
    public async Task Lock_CleanClosedYear_Succeeds()
    {
        SeedPeriod(2026, 1, closed: true);
        SeedPeriod(2026, 2, closed: true);

        var result = await BuildService().LockYearAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.True(YearLocked(2026));
    }

    [Fact]
    public async Task Lock_AlreadyLocked_Fails()
    {
        SeedPeriod(2026, 1, closed: true);
        Assert.True((await BuildService().LockYearAsync(2026)).IsSuccess);

        var result = await BuildService().LockYearAsync(2026);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà verrouillé", result.Error.Description);
    }

    [Fact]
    public async Task Lock_WhenDisabled_Fails()
    {
        SeedPeriod(2026, 1, closed: true);

        var result = await BuildService(enabled: false).LockYearAsync(2026);

        Assert.True(result.IsFailure);
        Assert.Contains("activé", result.Error.Description);
    }

    [Fact]
    public async Task AfterLock_EnsureOpenPeriod_RefusesEntry()
    {
        SeedPeriod(2026, 5, closed: true);
        Assert.True((await BuildService().LockYearAsync(2026)).IsSuccess);

        var result = await BuildPeriodService().EnsureOpenPeriodAsync(new DateTime(2026, 8, 15));

        Assert.True(result.IsFailure);
        Assert.Contains("verrouillé définitivement", result.Error.Description);
    }

    [Fact]
    public async Task AfterLock_Reopen_IsRefused()
    {
        SeedPeriod(2026, 5, closed: true);
        Guid periodId;
        using (var ctx = _factory.CreateContext())
            periodId = ctx.AccountingPeriods.AsNoTracking().Single(p => p.FiscalYear == 2026 && p.Month == 5).Id;
        Assert.True((await BuildService().LockYearAsync(2026)).IsSuccess);

        var result = await BuildPeriodService().ReopenPeriodAsync(periodId);

        Assert.True(result.IsFailure);
        Assert.Contains("verrouillé définitivement", result.Error.Description);
    }

    [Fact]
    public async Task LockDisabled_EnsureOpenPeriod_Unaffected()
    {
        // Non-régression : flag off ⇒ aucune garde, même si un verrou existe en base.
        SeedPeriod(2026, 5, closed: true);
        Assert.True((await BuildService().LockYearAsync(2026)).IsSuccess);

        var result = await BuildPeriodService(lockEnabled: false).EnsureOpenPeriodAsync(new DateTime(2026, 8, 15));

        Assert.True(result.IsSuccess);
    }
}
