using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Lot B : référence et date de pièce externes sur les écritures (facultatives, la référence
/// est normalisée et limitée à 50 caractères).
/// </summary>
public sealed class JournalEntryPieceInfoTests
{
    private static IReadOnlyList<JournalLineInput> BalancedLines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Vente", 0, 100m, null, ThirdPartyKind.None)
    };

    private static JournalEntry Make(string? pieceRef = null, DateTime? pieceDate = null, JournalEntryStatus status = JournalEntryStatus.Validee)
        => JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(), initialStatus: status, pieceRef: pieceRef, pieceDate: pieceDate).Value;

    [Fact]
    public void Create_WithoutPiece_LeavesFieldsNull()
    {
        var entry = Make();

        Assert.Null(entry.PieceRef);
        Assert.Null(entry.PieceDate);
    }

    [Fact]
    public void Create_WithPiece_TrimsAndStoresDateOnly()
    {
        var entry = Make("  FACT-2026-042  ", new DateTime(2026, 5, 8, 14, 30, 0));

        Assert.Equal("FACT-2026-042", entry.PieceRef);
        Assert.Equal(new DateTime(2026, 5, 8), entry.PieceDate);
    }

    [Fact]
    public void Create_WithBlankPiece_NormalizesToNull()
    {
        var entry = Make("   ");

        Assert.Null(entry.PieceRef);
    }

    [Fact]
    public void Create_WithTooLongPiece_Fails()
    {
        var result = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(), pieceRef: new string('X', 51));

        Assert.True(result.IsFailure);
        Assert.Contains("50", result.Error.Description);
    }

    [Fact]
    public void UpdateDraftLines_UpdatesPieceInfo()
    {
        var entry = Make("OLD-REF", new DateTime(2026, 5, 1), JournalEntryStatus.Brouillon);

        var result = entry.UpdateDraftLines("Vente modifiée", BalancedLines(),
            pieceRef: "NEW-REF", pieceDate: new DateTime(2026, 5, 9));

        Assert.True(result.IsSuccess);
        Assert.Equal("NEW-REF", entry.PieceRef);
        Assert.Equal(new DateTime(2026, 5, 9), entry.PieceDate);
    }

    [Fact]
    public void UpdateDraftLines_WithoutPiece_ClearsPieceInfo()
    {
        // La mise à jour d'un brouillon remplace l'en-tête : omettre la pièce l'efface
        // (même sémantique que le formulaire de saisie).
        var entry = Make("OLD-REF", new DateTime(2026, 5, 1), JournalEntryStatus.Brouillon);

        var result = entry.UpdateDraftLines("Vente modifiée", BalancedLines());

        Assert.True(result.IsSuccess);
        Assert.Null(entry.PieceRef);
        Assert.Null(entry.PieceDate);
    }
}
