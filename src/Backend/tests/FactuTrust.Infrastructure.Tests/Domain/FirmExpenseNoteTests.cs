using FactuTrust.Domain.Entities.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmExpenseNoteTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AssignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static FirmExpenseNote CreateNote()
    {
        return FirmExpenseNote.Create(FirmId, AssignmentId, "Ste Test", 2026, 7).Value;
    }

    [Fact]
    public void Submit_succeeds_from_draft()
    {
        var note = CreateNote();
        var result = note.Submit();

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmExpenseNoteStatus.Submitted, note.Status);
    }

    [Fact]
    public void Submit_succeeds_from_rejected()
    {
        var note = CreateNote();
        note.Submit();
        note.Reject();

        var result = note.Submit();

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmExpenseNoteStatus.Submitted, note.Status);
    }

    [Fact]
    public void Submit_fails_when_already_submitted()
    {
        var note = CreateNote();
        note.Submit();

        var result = note.Submit();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Approve_succeeds_from_submitted()
    {
        var note = CreateNote();
        note.Submit();

        var result = note.Approve();

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmExpenseNoteStatus.Approved, note.Status);
    }

    [Fact]
    public void Approve_fails_from_draft()
    {
        var note = CreateNote();

        var result = note.Approve();

        Assert.True(result.IsFailure);
        Assert.Equal(FirmExpenseNoteStatus.Draft, note.Status);
    }

    [Fact]
    public void Reject_succeeds_from_submitted()
    {
        var note = CreateNote();
        note.Submit();

        var result = note.Reject();

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmExpenseNoteStatus.Rejected, note.Status);
    }

    [Fact]
    public void MarkReimbursed_succeeds_from_approved()
    {
        var note = CreateNote();
        note.Submit();
        note.Approve();

        var result = note.MarkReimbursed();

        Assert.True(result.IsSuccess);
        Assert.Equal(FirmExpenseNoteStatus.Reimbursed, note.Status);
    }

    [Fact]
    public void MarkReimbursed_fails_from_submitted()
    {
        var note = CreateNote();
        note.Submit();

        var result = note.MarkReimbursed();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void UpdateAmounts_fails_when_approved()
    {
        var note = CreateNote();
        note.Submit();
        note.Approve();

        var result = note.UpdateAmounts(100, 0, 0, 0, 0, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void UpdateAmounts_succeeds_when_rejected()
    {
        var note = CreateNote();
        note.Submit();
        note.Reject();

        var result = note.UpdateAmounts(250, 10, 20, 5, 1000, "Corrigé");

        Assert.True(result.IsSuccess);
        Assert.Equal(250, note.TotalToReimburse);
        Assert.Equal("Corrigé", note.Notes);
    }
}
