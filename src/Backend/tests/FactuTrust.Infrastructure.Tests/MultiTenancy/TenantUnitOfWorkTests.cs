using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

/// <summary>
/// Tests d'intégration de l'unité de travail ambiante (transaction partagée entre
/// repositories). SQL Server requis (LocalDB éphémère) : InMemory ignore les
/// transactions. No-op si SQL indisponible — même convention que
/// FixedAssetRepositorySqlRetryTests.
/// </summary>
public sealed class TenantUnitOfWorkTests : IDisposable
{
    private readonly string? _connectionString;
    private readonly bool _canRun;
    private readonly TenantAmbientTransaction _ambient = new();
    private readonly TenantDbContextFactory? _factory;
    private readonly TenantUnitOfWork? _unitOfWork;

    public TenantUnitOfWorkTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            var dbName = $"FactuTrust_UoW_{Guid.NewGuid():N}";
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
    }

    [Fact]
    public async Task ExecuteAsync_Success_CommitsWritesFromMultipleRepositories()
    {
        if (!_canRun) return;

        var advances = new EmployeeAdvanceRepository(_factory!);
        var accruals = new LeaveBalanceAccrualRepository(_factory!);
        var employeeId = Guid.NewGuid();

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            await advances.AddAsync(EmployeeAdvance.Create(employeeId, new DateTime(2026, 7, 1), 100m).Value, ct);
            await accruals.AddAsync(LeaveBalanceAccrual.Create(employeeId, 2026, 7, 26m, Guid.NewGuid()).Value, ct);
            return Result.Success();
        });

        Assert.True(result.IsSuccess);
        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(1, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == employeeId));
        Assert.Equal(1, await verify.LeaveBalanceAccruals.CountAsync(a => a.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ExecuteAsync_FailureResult_RollsBackWritesFromAllRepositories()
    {
        if (!_canRun) return;

        var advances = new EmployeeAdvanceRepository(_factory!);
        var accruals = new LeaveBalanceAccrualRepository(_factory!);
        var employeeId = Guid.NewGuid();

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            await advances.AddAsync(EmployeeAdvance.Create(employeeId, new DateTime(2026, 7, 1), 100m).Value, ct);
            await accruals.AddAsync(LeaveBalanceAccrual.Create(employeeId, 2026, 7, 26m, Guid.NewGuid()).Value, ct);
            // Échec APRÈS deux écritures déjà « SaveChanges-ées » par leurs repositories :
            // tout doit être annulé (avant le correctif, chaque appel était commité séparément).
            return Result.Failure(Error.Validation("Test", "échec simulé au milieu du flux"));
        });

        Assert.True(result.IsFailure);
        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(0, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == employeeId));
        Assert.Equal(0, await verify.LeaveBalanceAccruals.CountAsync(a => a.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ExecuteAsync_Exception_RollsBackAndRethrows()
    {
        if (!_canRun) return;

        var advances = new EmployeeAdvanceRepository(_factory!);
        var employeeId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork!.ExecuteAsync<Result>(async ct =>
        {
            await advances.AddAsync(EmployeeAdvance.Create(employeeId, new DateTime(2026, 7, 1), 50m).Value, ct);
            throw new InvalidOperationException("boom");
        }));

        Assert.False(_ambient.IsActive); // l'état ambiant est nettoyé même sur exception
        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(0, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == employeeId));
    }

    [Fact]
    public async Task IsolatedContext_InsideUnitOfWork_KeepsItsOwnTransaction()
    {
        if (!_canRun) return;

        var isolatedEmployeeId = Guid.NewGuid();
        var enlistedEmployeeId = Guid.NewGuid();
        var advances = new EmployeeAdvanceRepository(_factory!);

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            // Écriture enrôlée (repository standard) — sera annulée.
            await advances.AddAsync(EmployeeAdvance.Create(enlistedEmployeeId, new DateTime(2026, 7, 1), 10m).Value, ct);

            // Écriture isolée avec SA propre transaction (motif AuditService/DocumentNumberService) —
            // doit survivre au rollback extérieur, sans exception « transaction en cours ».
            await using var isolated = _factory!.CreateIsolatedContext();
            var strategy = isolated.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await isolated.Database.BeginTransactionAsync(ct);
                isolated.EmployeeAdvances.Add(EmployeeAdvance.Create(isolatedEmployeeId, new DateTime(2026, 7, 1), 20m).Value);
                await isolated.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });

            return Result.Failure(Error.Validation("Test", "rollback extérieur"));
        });

        Assert.True(result.IsFailure);
        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(0, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == enlistedEmployeeId));
        Assert.Equal(1, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == isolatedEmployeeId));
    }

    [Fact]
    public async Task ExecuteAsync_Nested_JoinsAmbientTransaction()
    {
        if (!_canRun) return;

        var advances = new EmployeeAdvanceRepository(_factory!);
        var employeeId = Guid.NewGuid();

        var result = await _unitOfWork!.ExecuteAsync(async ct =>
        {
            await advances.AddAsync(EmployeeAdvance.Create(employeeId, new DateTime(2026, 7, 1), 30m).Value, ct);

            // Appel imbriqué : rejoint la transaction en cours (pas de nouvelle transaction).
            return await _unitOfWork.ExecuteAsync(async innerCt =>
            {
                await advances.AddAsync(EmployeeAdvance.Create(employeeId, new DateTime(2026, 7, 2), 40m).Value, innerCt);
                return Result.Failure(Error.Validation("Test", "échec imbriqué → tout annulé"));
            }, ct);
        });

        Assert.True(result.IsFailure);
        await using var verify = _factory!.CreateIsolatedContext();
        Assert.Equal(0, await verify.EmployeeAdvances.CountAsync(a => a.EmployeeId == employeeId));
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
            // Nettoyage best-effort (base éphémère de test).
        }
    }
}
