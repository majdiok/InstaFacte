using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmClientAssignmentTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Request_creates_pending_assignment()
    {
        var result = FirmClientAssignment.Request(CompanyId, FirmId, UserId, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.PendingFirmApproval, result.Value.Status);
        Assert.Equal("Notes", result.Value.Notes);
    }

    [Fact]
    public void Request_rejects_same_tenant()
    {
        var result = FirmClientAssignment.Request(CompanyId, CompanyId, UserId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Accept_transitions_from_pending_to_active()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        var accept = assignment.Accept(UserId);

        Assert.True(accept.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.Active, assignment.Status);
        Assert.NotNull(assignment.RespondedAt);
    }

    [Fact]
    public void Accept_fails_when_not_pending()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        assignment.Reject(UserId);

        var accept = assignment.Accept(UserId);

        Assert.True(accept.IsFailure);
    }

    [Fact]
    public void Reject_transitions_from_pending_to_rejected()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        var reject = assignment.Reject(UserId);

        Assert.True(reject.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.Rejected, assignment.Status);
    }

    [Fact]
    public void RevokeByCompany_requires_active()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;

        var revoke = assignment.RevokeByCompany(UserId);
        Assert.True(revoke.IsFailure);

        assignment.Accept(UserId);
        revoke = assignment.RevokeByCompany(UserId);
        Assert.True(revoke.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.RevokedByCompany, assignment.Status);
    }

    [Fact]
    public void RevokeByFirm_requires_active()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        assignment.Accept(UserId);

        var revoke = assignment.RevokeByFirm(UserId);

        Assert.True(revoke.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.RevokedByFirm, assignment.Status);
    }
}
