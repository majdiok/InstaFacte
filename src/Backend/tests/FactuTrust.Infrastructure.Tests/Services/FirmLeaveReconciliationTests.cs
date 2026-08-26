using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Contrepartie du fail-open : un report peut échouer sans annuler l'approbation. Le rapprochement
/// est l'endroit où l'écart se voit et se rattrape.
/// </summary>
public sealed class FirmLeaveReconciliationTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CollaboratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const int Year = 2026;

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FirmLeaveService BuildService(
        MasterDbContext db,
        IFirmLeavePayrollMirrorService? mirror = null)
    {
        var calendar = new Mock<ITunisianCalendarService>();
        calendar.Setup(c => c.IsHoliday(It.IsAny<DateTime>())).Returns(false);
        return new FirmLeaveService(
            db,
            calendar.Object,
            mirror ?? Mock.Of<IFirmLeavePayrollMirrorService>(),
            Mock.Of<INotificationService>(),
            NullLogger<FirmLeaveService>.Instance);
    }

    private static async Task<FirmLeaveRequest> SeedApprovedAsync(
        MasterDbContext db,
        LeaveType? payrollLeaveType,
        FirmLeavePayrollMirrorState mirrorState,
        string code = "UNPAID")
    {
        if (!await db.Users.AnyAsync(u => u.Id == CollaboratorId))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = CollaboratorId,
                TenantId = FirmId,
                IsActive = true,
                FirstName = "Karim",
                LastName = "Ferchiou",
                Email = "karim@cabinet.tn",
                UserName = "karim@cabinet.tn"
            });
        }

        var type = await db.FirmLeaveTypes.FirstOrDefaultAsync(t => t.FirmTenantId == FirmId && t.Code == code);
        if (type is null)
        {
            type = FirmLeaveType.Create(
                FirmId, code, code, "#64748b",
                deductsBalance: false, requiresApproval: true, isSystem: true, sortOrder: 0,
                payrollLeaveType: payrollLeaveType, countsAsAbsence: true).Value;
            db.FirmLeaveTypes.Add(type);
        }

        var request = FirmLeaveRequest.Create(
            FirmId, CollaboratorId, type.Id,
            new DateTime(Year, 5, 4), new DateTime(Year, 5, 6),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, 3m).Value;
        request.Submit();
        request.Approve(Guid.NewGuid(), "Ahmed");
        request.MarkPayrollMirror(mirrorState, null, mirrorState == FirmLeavePayrollMirrorState.Failed ? "Base injoignable." : null);
        db.FirmLeaveRequests.Add(request);

        await db.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task A_failed_mirror_is_listed_as_pending_and_replayable()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.Failed);
        var service = BuildService(db);

        var report = await service.GetReconciliationAsync(FirmId, Year);

        var row = Assert.Single(report.Pending);
        Assert.Equal((int)FirmLeavePayrollMirrorState.Failed, row.MirrorState);
        Assert.True(row.CanReplay);
        Assert.Equal("Karim Ferchiou", row.CollaboratorName);
        Assert.Equal(0, report.MirroredCount);
    }

    [Fact]
    public async Task A_never_attempted_mirror_is_an_écart_too()
    {
        // Demandes approuvées avant la mise en place du report : elles n'ont jamais été tentées.
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.NotMirrored);
        var service = BuildService(db);

        var report = await service.GetReconciliationAsync(FirmId, Year);

        Assert.Single(report.Pending);
        Assert.Equal((int)FirmLeavePayrollMirrorState.NotMirrored, report.Pending[0].MirrorState);
    }

    [Fact]
    public async Task A_frozen_month_is_listed_but_not_replayable()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.BlockedFrozenPayroll);
        var service = BuildService(db);

        var report = await service.GetReconciliationAsync(FirmId, Year);

        // Rejouer ne changerait rien : seule une régularisation le peut.
        Assert.False(Assert.Single(report.Pending).CanReplay);
    }

    [Fact]
    public async Task An_unmapped_type_is_counted_apart_and_never_flagged()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, payrollLeaveType: null, FirmLeavePayrollMirrorState.NoPayrollEffect, "REMOTE");
        var service = BuildService(db);

        var report = await service.GetReconciliationAsync(FirmId, Year);

        Assert.Empty(report.Pending);
        Assert.Equal(1, report.NoPayrollEffectCount);
    }

    [Fact]
    public async Task A_mirrored_leave_is_counted_and_not_pending()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.Mirrored);
        var service = BuildService(db);

        var report = await service.GetReconciliationAsync(FirmId, Year);

        Assert.Empty(report.Pending);
        Assert.Equal(1, report.MirroredCount);
    }

    [Fact]
    public async Task Replaying_reruns_the_mirror_and_reports_what_succeeded()
    {
        await using var db = BuildMaster();
        var request = await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.Failed);

        var mirror = new Mock<IFirmLeavePayrollMirrorService>();
        mirror.Setup(m => m.MirrorApprovedAsync(FirmId, request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmLeaveMirrorResultDto
            {
                State = (int)FirmLeavePayrollMirrorState.Mirrored,
                StateDisplay = "Reporté en paie",
                IsApplied = true
            });

        var service = BuildService(db, mirror.Object);
        var result = await service.ReplayPayrollMirrorAsync(FirmId, Year, leaveRequestId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Replayed);
        Assert.Equal(1, result.Value.Succeeded);
        mirror.Verify(m => m.MirrorApprovedAsync(FirmId, request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Replaying_does_not_touch_leaves_already_mirrored()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.Mirrored);

        var mirror = new Mock<IFirmLeavePayrollMirrorService>();
        var service = BuildService(db, mirror.Object);

        var result = await service.ReplayPayrollMirrorAsync(FirmId, Year, leaveRequestId: null);

        Assert.Equal(0, result.Value.Replayed);
        mirror.Verify(
            m => m.MirrorApprovedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Repeated_failure_messages_are_reported_once()
    {
        await using var db = BuildMaster();
        await SeedApprovedAsync(db, LeaveType.Unpaid, FirmLeavePayrollMirrorState.Failed);
        await SeedApprovedAsync(db, LeaveType.Paid, FirmLeavePayrollMirrorState.Failed, "PAID");

        var mirror = new Mock<IFirmLeavePayrollMirrorService>();
        mirror.Setup(m => m.MirrorApprovedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmLeaveMirrorResultDto
            {
                State = (int)FirmLeavePayrollMirrorState.Failed,
                StateDisplay = "Report en échec",
                Message = "Base de paie injoignable."
            });

        var service = BuildService(db, mirror.Object);
        var result = await service.ReplayPayrollMirrorAsync(FirmId, Year, leaveRequestId: null);

        Assert.Equal(2, result.Value.Replayed);
        Assert.Equal(0, result.Value.Succeeded);
        // Le même motif ne doit pas être répété autant de fois qu'il y a de demandes.
        Assert.Single(result.Value.Messages);
    }
}
