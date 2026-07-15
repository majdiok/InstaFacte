using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Forecasting;

/// <summary>
/// Unit tests for the V2 audit-trail entity <see cref="ReplenishmentDecisionAudit"/>.
/// </summary>
public sealed class ReplenishmentDecisionAuditTests
{
    [Fact]
    public void Create_WithValidInputs_PopulatesAllFields()
    {
        var recId = Guid.NewGuid();

        var audit = ReplenishmentDecisionAudit.Create(
            recommendationId: recId,
            fromStatus: ReplenishmentStatus.Pending,
            toStatus: ReplenishmentStatus.Approved,
            actionType: "Approve",
            actorUserId: "user-42",
            reason: null,
            payloadJson: "{\"foo\":\"bar\"}");

        Assert.Equal(recId, audit.RecommendationId);
        Assert.Equal(ReplenishmentStatus.Pending, audit.FromStatus);
        Assert.Equal(ReplenishmentStatus.Approved, audit.ToStatus);
        Assert.Equal("Approve", audit.ActionType);
        Assert.Equal("user-42", audit.ActorUserId);
        Assert.Null(audit.Reason);
        Assert.Equal("{\"foo\":\"bar\"}", audit.PayloadJson);
        Assert.True((DateTime.UtcNow - audit.ActedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Create_TrimsActionTypeAndReasonAndActor()
    {
        var audit = ReplenishmentDecisionAudit.Create(
            recommendationId: Guid.NewGuid(),
            fromStatus: ReplenishmentStatus.Pending,
            toStatus: ReplenishmentStatus.Dismissed,
            actionType: "  Dismiss  ",
            actorUserId: "  user-42  ",
            reason: "  budget cancelled  ");

        Assert.Equal("Dismiss", audit.ActionType);
        Assert.Equal("user-42", audit.ActorUserId);
        Assert.Equal("budget cancelled", audit.Reason);
    }

    [Fact]
    public void Create_WithEmptyPayloadJson_StoresNull()
    {
        var audit = ReplenishmentDecisionAudit.Create(
            recommendationId: Guid.NewGuid(),
            fromStatus: ReplenishmentStatus.Pending,
            toStatus: ReplenishmentStatus.Pending,
            actionType: "AttachNotes",
            actorUserId: "user-1",
            payloadJson: "   ");

        Assert.Null(audit.PayloadJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankActionType_Throws(string actionType)
    {
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.NewGuid(),
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Approved,
                actionType: actionType,
                actorUserId: "user"));
    }

    [Fact]
    public void Create_WithEmptyRecommendationId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.Empty,
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Approved,
                actionType: "Approve",
                actorUserId: "user"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankActorUserId_Throws(string actor)
    {
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.NewGuid(),
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Approved,
                actionType: "Approve",
                actorUserId: actor));
    }

    [Fact]
    public void Create_WithOversizedActionType_Throws()
    {
        var huge = new string('A', 33);
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.NewGuid(),
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Approved,
                actionType: huge,
                actorUserId: "user"));
    }

    [Fact]
    public void Create_WithOversizedReason_Throws()
    {
        var huge = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.NewGuid(),
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Dismissed,
                actionType: "Dismiss",
                actorUserId: "user",
                reason: huge));
    }

    [Fact]
    public void Create_WithOversizedPayload_Throws()
    {
        var huge = new string('A', 4001);
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentDecisionAudit.Create(
                recommendationId: Guid.NewGuid(),
                fromStatus: ReplenishmentStatus.Pending,
                toStatus: ReplenishmentStatus.Approved,
                actionType: "Approve",
                actorUserId: "user",
                payloadJson: huge));
    }
}
