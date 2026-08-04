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

    [Fact]
    public void Reject_stores_trimmed_reason()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;

        var reject = assignment.Reject(UserId, "  Dossier incomplet  ");

        Assert.True(reject.IsSuccess);
        Assert.Equal("Dossier incomplet", assignment.RejectionReason);
    }

    [Fact]
    public void Reject_without_reason_leaves_reason_null()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;

        var reject = assignment.Reject(UserId, "   ");

        Assert.True(reject.IsSuccess);
        Assert.Null(assignment.RejectionReason);
    }

    [Fact]
    public void Reject_truncates_reason_to_max_length()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        var longReason = new string('x', FirmClientAssignment.RejectionReasonMaxLength + 100);

        assignment.Reject(UserId, longReason);

        Assert.Equal(FirmClientAssignment.RejectionReasonMaxLength, assignment.RejectionReason!.Length);
    }

    [Fact]
    public void CancelByCompany_transitions_from_pending()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;

        var cancel = assignment.CancelByCompany(UserId);

        Assert.True(cancel.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.CancelledByCompany, assignment.Status);
        Assert.Equal(UserId, assignment.RevokedByUserId);
        Assert.NotNull(assignment.RevokedAt);
    }

    [Fact]
    public void CreateByFirm_creates_active_assignment_with_firm_origin()
    {
        var result = FirmClientAssignment.CreateByFirm(CompanyId, FirmId, UserId, "Dossier créé par le cabinet");

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmAssignmentStatus.Active, result.Value.Status);
        Assert.Equal(FirmAssignmentOrigin.FirmCreated, result.Value.Origin);
        Assert.Equal(UserId, result.Value.RequestedByUserId);
        Assert.Equal(UserId, result.Value.RespondedByUserId);
        Assert.NotNull(result.Value.RespondedAt);
        Assert.Equal("Dossier créé par le cabinet", result.Value.Notes);
    }

    [Fact]
    public void CreateByFirm_rejects_same_tenant()
    {
        var result = FirmClientAssignment.CreateByFirm(CompanyId, CompanyId, UserId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateByFirm_rejects_empty_user()
    {
        var result = FirmClientAssignment.CreateByFirm(CompanyId, FirmId, Guid.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Request_creates_assignment_with_company_origin()
    {
        var result = FirmClientAssignment.Request(CompanyId, FirmId, UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmAssignmentOrigin.CompanyRequest, result.Value.Origin);
    }

    [Fact]
    public void CancelByCompany_fails_when_active()
    {
        var assignment = FirmClientAssignment.Request(CompanyId, FirmId, UserId).Value;
        assignment.Accept(UserId);

        var cancel = assignment.CancelByCompany(UserId);

        Assert.True(cancel.IsFailure);
        Assert.Equal(FirmAssignmentStatus.Active, assignment.Status);
    }
}
