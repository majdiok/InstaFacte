using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Mutateurs de brouillon (édition de masse) : changement de journal, de date et de libellé.
/// Exigence de non-régression : refus strict hors statut Brouillon (une écriture validée ou
/// clôturée reste immuable), normalisation cohérente avec Create/UpdateDraftLines.
/// </summary>
public sealed class JournalEntryDraftMutationTests
{
    private static IReadOnlyList<JournalLineInput> BalancedLines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Vente", 0, 100m, null, ThirdPartyKind.None)
    };

    private static JournalEntry Make(JournalEntryStatus status)
        => JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(), initialStatus: status).Value;

    // ── Chemin nominal (brouillon) ─────────────────────────────────────────────

    [Fact]
    public void ChangeDraftJournal_OnDraft_NormalizesAndApplies()
    {
        var entry = Make(JournalEntryStatus.Brouillon);

        var result = entry.ChangeDraftJournal(" jod ");

        Assert.True(result.IsSuccess);
        Assert.Equal("JOD", entry.JournalCode);
    }

    [Fact]
    public void ChangeDraftDate_OnDraft_ReassignsDateAndPeriod()
    {
        var entry = Make(JournalEntryStatus.Brouillon);
        var newPeriod = Guid.NewGuid();

        var result = entry.ChangeDraftDate(new DateTime(2026, 7, 3, 9, 0, 0), newPeriod);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 7, 3), entry.EntryDate);
        Assert.Equal(newPeriod, entry.AccountingPeriodId);
    }

    [Fact]
    public void ChangeDraftLabel_OnDraft_TrimsAndApplies()
    {
        var entry = Make(JournalEntryStatus.Brouillon);

        var result = entry.ChangeDraftLabel("  Nouveau libellé  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Nouveau libellé", entry.Label);
    }

    // ── Gardes : refus hors brouillon (non-régression) ─────────────────────────

    [Theory]
    [InlineData(JournalEntryStatus.Validee)]
    [InlineData(JournalEntryStatus.Cloturee)]
    public void ChangeDraftJournal_OnNonDraft_IsRefusedAndUnchanged(JournalEntryStatus status)
    {
        var entry = Make(status);

        var result = entry.ChangeDraftJournal("JOD");

        Assert.True(result.IsFailure);
        Assert.Equal("JV", entry.JournalCode);   // inchangé
    }

    [Theory]
    [InlineData(JournalEntryStatus.Validee)]
    [InlineData(JournalEntryStatus.Cloturee)]
    public void ChangeDraftDate_OnNonDraft_IsRefusedAndUnchanged(JournalEntryStatus status)
    {
        var entry = Make(status);
        var original = entry.EntryDate;

        var result = entry.ChangeDraftDate(new DateTime(2026, 7, 3), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(original, entry.EntryDate);
    }

    [Theory]
    [InlineData(JournalEntryStatus.Validee)]
    [InlineData(JournalEntryStatus.Cloturee)]
    public void ChangeDraftLabel_OnNonDraft_IsRefusedAndUnchanged(JournalEntryStatus status)
    {
        var entry = Make(status);

        var result = entry.ChangeDraftLabel("Autre");

        Assert.True(result.IsFailure);
        Assert.Equal("Vente", entry.Label);      // inchangé
    }

    // ── Validation des entrées ─────────────────────────────────────────────────

    [Fact]
    public void ChangeDraftJournal_Empty_IsRefused()
    {
        var entry = Make(JournalEntryStatus.Brouillon);
        Assert.True(entry.ChangeDraftJournal("  ").IsFailure);
    }

    [Fact]
    public void ChangeDraftLabel_Empty_IsRefused()
    {
        var entry = Make(JournalEntryStatus.Brouillon);
        Assert.True(entry.ChangeDraftLabel("  ").IsFailure);
    }

    [Fact]
    public void ChangeDraftDate_EmptyPeriod_IsRefused()
    {
        var entry = Make(JournalEntryStatus.Brouillon);
        Assert.True(entry.ChangeDraftDate(new DateTime(2026, 7, 3), Guid.Empty).IsFailure);
    }
}
