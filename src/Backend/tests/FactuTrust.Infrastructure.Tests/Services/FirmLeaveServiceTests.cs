using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmLeaveServiceTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherFirmId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AccountantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static MasterDbContext BuildMaster()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MasterDbContext(options);
    }

    private static FirmLeaveService BuildService(
        MasterDbContext db,
        IFirmLeavePayrollMirrorService? mirror = null)
    {
        var calendar = new Mock<ITunisianCalendarService>();
        calendar.Setup(c => c.IsHoliday(It.IsAny<DateTime>())).Returns(false);

        // Par défaut, un report neutre : ces tests portent sur le circuit RH, pas sur la paie.
        var mirrorService = mirror ?? BuildNoOpMirror();
        return new FirmLeaveService(
            db,
            calendar.Object,
            mirrorService,
            Mock.Of<INotificationService>(),
            NullLogger<FirmLeaveService>.Instance);
    }

    private static IFirmLeavePayrollMirrorService BuildNoOpMirror()
    {
        var mock = new Mock<IFirmLeavePayrollMirrorService>();
        mock.Setup(m => m.MirrorApprovedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmLeaveMirrorResultDto
            {
                State = (int)FirmLeavePayrollMirrorState.NoPayrollEffect,
                StateDisplay = "Sans effet paie"
            });
        mock.Setup(m => m.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirmLeaveMirrorResultDto
            {
                State = (int)FirmLeavePayrollMirrorState.NotMirrored,
                StateDisplay = "Non reporté"
            });
        return mock.Object;
    }

    private static async Task SeedUsersAsync(MasterDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = ManagerId,
            UserName = "mgr@test.com",
            NormalizedUserName = "MGR@TEST.COM",
            Email = "mgr@test.com",
            NormalizedEmail = "MGR@TEST.COM",
            FirstName = "Manager",
            LastName = "Test",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        db.Users.Add(new ApplicationUser
        {
            Id = AccountantId,
            UserName = "acc@test.com",
            NormalizedUserName = "ACC@TEST.COM",
            Email = "acc@test.com",
            NormalizedEmail = "ACC@TEST.COM",
            FirstName = "Collab",
            LastName = "Test",
            TenantId = FirmId,
            IsActive = true,
            EmailConfirmed = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> PaidTypeIdAsync(FirmLeaveService svc, MasterDbContext db)
    {
        await svc.EnsureDefaultsAsync(FirmId, 2026);
        return await db.FirmLeaveTypes.Where(t => t.FirmTenantId == FirmId && t.Code == "PAID")
            .Select(t => t.Id).FirstAsync();
    }

    [Fact]
    public async Task Create_submit_approve_decrements_balance()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, isManager: false, new CreateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 12),
            StartUnit = 0,
            EndUnit = 0,
            Reason = "Vacances",
            SubmitImmediately = true
        });
        Assert.True(create.IsSuccess);
        Assert.Equal((int)FirmLeaveRequestStatus.Submitted, create.Value.Status);
        Assert.Equal(3m, create.Value.Days);

        var process = await svc.ProcessAsync(FirmId, ManagerId, "Manager Test", create.Value.Id, new ProcessFirmLeaveDto
        {
            Approve = true
        });
        Assert.True(process.IsSuccess);
        Assert.Equal((int)FirmLeaveRequestStatus.Approved, process.Value.Status);

        var bal = await svc.GetMyBalanceAsync(FirmId, AccountantId, 2026);
        Assert.NotNull(bal);
        Assert.Equal(3m, bal!.ConsumedDays);
        Assert.Equal(27m, bal.RemainingDays);
    }

    [Fact]
    public async Task Accountant_cannot_see_other_requests_in_list()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        await svc.CreateAsync(FirmId, ManagerId, true, new CreateFirmLeaveRequestDto
        {
            UserId = ManagerId,
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 2),
            SubmitImmediately = true
        });

        var list = await svc.ListRequestsAsync(FirmId, isManager: false, AccountantId, null, null, null, null, null, 2026);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Approve_fails_when_balance_insufficient()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        await svc.SetBalanceAsync(FirmId, AccountantId, 2026, new SetFirmLeaveBalanceDto
        {
            OpeningBalanceDays = 1m,
            AdjustmentDays = 0m
        });

        var create = await svc.CreateAsync(FirmId, AccountantId, false, new CreateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 14),
            SubmitImmediately = true
        });
        Assert.True(create.IsSuccess);

        var process = await svc.ProcessAsync(FirmId, ManagerId, "M", create.Value.Id, new ProcessFirmLeaveDto { Approve = true });
        Assert.True(process.IsFailure);
    }

    [Fact]
    public async Task Cross_tenant_isolation()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, new CreateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 11),
            SubmitImmediately = true
        });

        var other = await svc.GetRequestAsync(OtherFirmId, create.Value.Id, true, ManagerId);
        Assert.Null(other);
    }

    [Fact]
    public async Task Reject_then_resubmit()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        var create = await svc.CreateAsync(FirmId, AccountantId, false, new CreateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 11),
            SubmitImmediately = true
        });

        await svc.ProcessAsync(FirmId, ManagerId, "M", create.Value.Id, new ProcessFirmLeaveDto
        {
            Approve = false,
            RejectionReason = "Pas possible"
        });

        var updated = await svc.UpdateAsync(FirmId, AccountantId, false, create.Value.Id, new UpdateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 9, 7),
            EndDate = new DateTime(2026, 9, 8),
            StartUnit = 0,
            EndUnit = 0,
            Reason = "Reporté"
        });
        Assert.True(updated.IsSuccess);

        var submit = await svc.SubmitAsync(FirmId, AccountantId, false, create.Value.Id);
        Assert.True(submit.IsSuccess);
        Assert.Equal((int)FirmLeaveRequestStatus.Submitted, submit.Value.Status);
    }

    [Fact]
    public async Task Overview_scoped_to_accountant_own_data()
    {
        await using var db = BuildMaster();
        await SeedUsersAsync(db);
        var svc = BuildService(db);
        var typeId = await PaidTypeIdAsync(svc, db);

        await svc.CreateAsync(FirmId, ManagerId, true, new CreateFirmLeaveRequestDto
        {
            UserId = ManagerId,
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 2),
            SubmitImmediately = true
        });

        var accountantRequest = await svc.CreateAsync(FirmId, AccountantId, false, new CreateFirmLeaveRequestDto
        {
            LeaveTypeId = typeId,
            StartDate = new DateTime(2026, 10, 5),
            EndDate = new DateTime(2026, 10, 6),
            SubmitImmediately = true
        });
        Assert.True(accountantRequest.IsSuccess);

        var accountantOverview = await svc.GetOverviewAsync(FirmId, 2026, isManager: false, AccountantId);
        Assert.Single(accountantOverview.PendingRequests);
        Assert.Equal(accountantRequest.Value.Id, accountantOverview.PendingRequests[0].Id);
        Assert.Single(accountantOverview.TopBalances);
        Assert.Equal(AccountantId, accountantOverview.TopBalances[0].UserId);

        var managerOverview = await svc.GetOverviewAsync(FirmId, 2026, isManager: true, ManagerId);
        Assert.Equal(2, managerOverview.PendingRequests.Count);
        Assert.Equal(2, managerOverview.TopBalances.Count);
    }
}
