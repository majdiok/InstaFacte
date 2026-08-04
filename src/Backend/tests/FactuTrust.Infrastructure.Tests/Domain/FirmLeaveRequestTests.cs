using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmLeaveRequestTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TypeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ProcessorId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static FirmLeaveRequest CreateDraft() =>
        FirmLeaveRequest.Create(
            FirmId, UserId, TypeId,
            new DateTime(2026, 8, 10), new DateTime(2026, 8, 14),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, 5m, "Vacances").Value;

    [Fact]
    public void Submit_succeeds_from_draft()
    {
        var req = CreateDraft();
        Assert.True(req.Submit().IsSuccess);
        Assert.Equal(FirmLeaveRequestStatus.Submitted, req.Status);
        Assert.NotNull(req.SubmittedAt);
    }

    [Fact]
    public void Approve_succeeds_from_submitted()
    {
        var req = CreateDraft();
        req.Submit();
        Assert.True(req.Approve(ProcessorId, "Manager").IsSuccess);
        Assert.Equal(FirmLeaveRequestStatus.Approved, req.Status);
        Assert.Equal("Manager", req.ProcessedByName);
    }

    [Fact]
    public void Approve_fails_from_draft()
    {
        var req = CreateDraft();
        Assert.True(req.Approve(ProcessorId, "Manager").IsFailure);
    }

    [Fact]
    public void Reject_succeeds_from_submitted()
    {
        var req = CreateDraft();
        req.Submit();
        Assert.True(req.Reject(ProcessorId, "Manager", "Effectif insuffisant").IsSuccess);
        Assert.Equal(FirmLeaveRequestStatus.Rejected, req.Status);
        Assert.Equal("Effectif insuffisant", req.RejectionReason);
    }

    [Fact]
    public void Cancel_succeeds_from_draft_and_submitted()
    {
        var draft = CreateDraft();
        Assert.True(draft.Cancel().IsSuccess);

        var submitted = CreateDraft();
        submitted.Submit();
        Assert.True(submitted.Cancel().IsSuccess);
        Assert.Equal(FirmLeaveRequestStatus.Cancelled, submitted.Status);
    }

    [Fact]
    public void Cancel_fails_when_approved()
    {
        var req = CreateDraft();
        req.Submit();
        req.Approve(ProcessorId, "M");
        Assert.True(req.Cancel().IsFailure);
    }

    [Fact]
    public void Update_allowed_on_rejected_then_resubmit()
    {
        var req = CreateDraft();
        req.Submit();
        req.Reject(ProcessorId, "M", "x");
        Assert.True(req.Update(TypeId, new DateTime(2026, 9, 1), new DateTime(2026, 9, 3),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, 3m, "Replanifié").IsSuccess);
        Assert.True(req.Submit().IsSuccess);
    }

    [Fact]
    public void Update_fails_when_submitted()
    {
        var req = CreateDraft();
        req.Submit();
        Assert.True(req.Update(TypeId, new DateTime(2026, 9, 1), new DateTime(2026, 9, 3),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, 3m, null).IsFailure);
    }

    [Fact]
    public void Create_fails_when_end_before_start()
    {
        var result = FirmLeaveRequest.Create(
            FirmId, UserId, TypeId,
            new DateTime(2026, 8, 14), new DateTime(2026, 8, 10),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, 1m);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Overlaps_detects_intersection()
    {
        var req = CreateDraft();
        Assert.True(req.Overlaps(new DateTime(2026, 8, 12), new DateTime(2026, 8, 20)));
        Assert.False(req.Overlaps(new DateTime(2026, 8, 15), new DateTime(2026, 8, 20)));
    }
}
