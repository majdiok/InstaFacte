using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Le congé du cabinet est la source unique : son approbation doit produire l'effet paie
/// correspondant, sans jamais toucher un bulletin déjà arrêté ni bloquer l'acte RH.
/// </summary>
public sealed class FirmLeavePayrollMirrorServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PayrollEmployeeId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class InMemoryTenantFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public InMemoryTenantFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext() => CreateIsolatedContext();

        public TenantDbContext CreateIsolatedContext() =>
            new(new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options);

        public TenantDbContext CreateIsolatedContext(string connectionString) => CreateIsolatedContext();
    }

    private static FirmLeavePayrollMirrorService BuildService(
        MasterDbContext master,
        ITenantDbContextFactory tenantFactory,
        string? connectionString = "InMemory")
    {
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(t => t.GetConnectionStringAsync(FirmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionString);

        return new FirmLeavePayrollMirrorService(
            master,
            new FirmTenantPayrollAccessor(tenantService.Object, tenantFactory),
            NullLogger<FirmLeavePayrollMirrorService>.Instance);
    }

    /// <summary>Crée un type, un profil lié et une demande approuvée sur la période demandée.</summary>
    private static async Task<FirmLeaveRequest> SeedApprovedLeaveAsync(
        MasterDbContext master,
        LeaveType? payrollLeaveType,
        DateTime start,
        DateTime end,
        decimal days,
        bool linkPayroll = true)
    {
        var type = FirmLeaveType.Create(
            FirmId, "UNPAID", "Congé sans solde", "#64748b",
            deductsBalance: false, requiresApproval: true, isSystem: true, sortOrder: 0,
            payrollLeaveType: payrollLeaveType, countsAsAbsence: true).Value;
        master.FirmLeaveTypes.Add(type);

        if (linkPayroll)
        {
            master.FirmCollaboratorProfiles.Add(new FirmCollaboratorProfile
            {
                UserId = CollaboratorId,
                Qualification = string.Empty,
                PayrollEmployeeId = PayrollEmployeeId,
                PayrollLinkSource = FirmPayrollLinkSource.Manual
            });
        }

        var request = FirmLeaveRequest.Create(
            FirmId, CollaboratorId, type.Id, start, end,
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, days, "Convenance").Value;
        request.Submit();
        request.Approve(Guid.NewGuid(), "Ahmed Boudaya");
        master.FirmLeaveRequests.Add(request);

        await master.SaveChangesAsync();
        return request;
    }

    /// <summary>
    /// Cycle de paie arrêté sur le mois donné.
    /// </summary>
    /// <remarks>
    /// <c>Validate</c> exige des bulletins réels ; seul le statut nous intéresse ici, on le pose
    /// directement — même procédé que les autres tests de ce projet pour figer un état.
    /// </remarks>
    private static async Task SeedFrozenRunAsync(ITenantDbContextFactory factory, int year, int month)
    {
        await using var tenant = factory.CreateContext();
        var run = PayrollRun.Create(year, month, year).Value;
        typeof(PayrollRun)
            .GetProperty(nameof(PayrollRun.Status))!
            .SetValue(run, PayrollRunStatus.Validated);
        tenant.PayrollRuns.Add(run);
        await tenant.SaveChangesAsync();
    }

    [Fact]
    public async Task Approved_unpaid_leave_creates_an_approved_payroll_leave()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        var result = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.True(result.IsApplied);
        Assert.Equal((int)FirmLeavePayrollMirrorState.Mirrored, result.State);

        await using var tenant = factory.CreateContext();
        var mirror = await tenant.LeaveRequests.SingleAsync();
        Assert.Equal(PayrollEmployeeId, mirror.EmployeeId);
        Assert.Equal(LeaveType.Unpaid, mirror.Type);
        Assert.Equal(3m, mirror.Days);
        Assert.True(mirror.IsApproved);
        // C'est ce type qui produit la retenue sur le brut du mois.
        Assert.True(mirror.Type.ReducesGross());
    }

    [Fact]
    public async Task Half_days_survive_the_transfer()
    {
        // Le cabinet compte en demi-journées, la paie stocke un décimal : rien ne doit être perdu.
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Paid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 11), 4.5m);

        var service = BuildService(master, factory);
        await service.MirrorApprovedAsync(FirmId, request.Id);

        await using var tenant = factory.CreateContext();
        var mirror = await tenant.LeaveRequests.SingleAsync();
        Assert.Equal(4.5m, mirror.Days);
    }

    [Fact]
    public async Task Mirroring_twice_updates_instead_of_duplicating()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Paid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        var first = await service.MirrorApprovedAsync(FirmId, request.Id);
        var second = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.Equal(first.PayrollLeaveRequestId, second.PayrollLeaveRequestId);

        await using var tenant = factory.CreateContext();
        Assert.Equal(1, await tenant.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task A_frozen_month_is_never_written_to()
    {
        // Invariant cardinal du module paie : un bulletin validé ne se recalcule jamais.
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        await SeedFrozenRunAsync(factory, 2026, 9);
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        var result = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.False(result.IsApplied);
        Assert.Equal((int)FirmLeavePayrollMirrorState.BlockedFrozenPayroll, result.State);
        Assert.Contains("09/2026", result.Message!, StringComparison.Ordinal);
        Assert.Contains("régularisation", result.Message!, StringComparison.OrdinalIgnoreCase);

        await using var tenant = factory.CreateContext();
        Assert.False(await tenant.LeaveRequests.AnyAsync());
    }

    [Fact]
    public async Task An_unmapped_leave_type_has_no_payroll_effect_and_is_not_an_error()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, payrollLeaveType: null, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        var result = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.Equal((int)FirmLeavePayrollMirrorState.NoPayrollEffect, result.State);
        Assert.False(result.IsApplied);

        await using var tenant = factory.CreateContext();
        Assert.False(await tenant.LeaveRequests.AnyAsync());
    }

    [Fact]
    public async Task An_unlinked_collaborator_records_a_failure_without_throwing()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m,
            linkPayroll: false);

        var service = BuildService(master, factory);
        var result = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.Equal((int)FirmLeavePayrollMirrorState.Failed, result.State);
        Assert.Contains("lié", result.Message!, StringComparison.OrdinalIgnoreCase);

        // L'approbation RH reste acquise : seul le report a échoué.
        var reloaded = await master.FirmLeaveRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(FirmLeaveRequestStatus.Approved, reloaded.Status);
        Assert.Equal(FirmLeavePayrollMirrorState.Failed, reloaded.PayrollMirrorState);
    }

    [Fact]
    public async Task An_unreachable_payroll_database_never_cancels_the_approval()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        // Aucune base rattachée au cabinet.
        var service = BuildService(master, factory, connectionString: null);
        var result = await service.MirrorApprovedAsync(FirmId, request.Id);

        Assert.Equal((int)FirmLeavePayrollMirrorState.Failed, result.State);

        var reloaded = await master.FirmLeaveRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(FirmLeaveRequestStatus.Approved, reloaded.Status);
        Assert.NotNull(reloaded.PayrollMirrorMessage);
    }

    [Fact]
    public async Task Revoking_removes_the_payroll_leave_when_the_month_is_still_open()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        await service.MirrorApprovedAsync(FirmId, request.Id);

        var revoked = await service.RevokeAsync(FirmId, request.Id);

        Assert.Equal((int)FirmLeavePayrollMirrorState.Revoked, revoked.State);

        await using var tenant = factory.CreateContext();
        Assert.False(await tenant.LeaveRequests.AnyAsync());

        var reloaded = await master.FirmLeaveRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Null(reloaded.PayrollLeaveRequestId);
    }

    [Fact]
    public async Task Revoking_refuses_to_touch_a_frozen_month()
    {
        await using var master = BuildMaster();
        var factory = new InMemoryTenantFactory(Guid.NewGuid().ToString());
        var request = await SeedApprovedLeaveAsync(
            master, LeaveType.Unpaid, new DateTime(2026, 9, 7), new DateTime(2026, 9, 9), 3m);

        var service = BuildService(master, factory);
        await service.MirrorApprovedAsync(FirmId, request.Id);

        // La paie est arrêtée après coup : le congé est déjà pris en compte sur le bulletin.
        await SeedFrozenRunAsync(factory, 2026, 9);

        var revoked = await service.RevokeAsync(FirmId, request.Id);

        Assert.Equal((int)FirmLeavePayrollMirrorState.BlockedFrozenPayroll, revoked.State);

        await using var tenant = factory.CreateContext();
        Assert.Equal(1, await tenant.LeaveRequests.CountAsync());
    }
}
