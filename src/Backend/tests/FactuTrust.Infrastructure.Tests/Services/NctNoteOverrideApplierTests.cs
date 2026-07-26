using FactuTrust.Application.Accounting;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Superposition des personnalisations d'annexes NCT. L'exigence structurante est le TEST DE GEL :
/// sans override, la liste de notes est renvoyée à l'IDENTIQUE — la liasse ne peut donc pas bouger
/// pour un dossier qui n'a rien personnalisé.
/// </summary>
public sealed class NctNoteOverrideApplierTests
{
    private static NctDetailedNoteDto Note(int number, string title) => new()
    {
        Number = number,
        Title = title,
        Family = NctAnnexFamily.Actif,
        Lines = Array.Empty<NctDetailedNoteLineDto>(),
        Total = 100m,
        PreviousTotal = 80m
    };

    private static NctNoteOverride Override(int number, string? title = null, string? description = null, bool hidden = false)
        => NctNoteOverride.Create(2026, number, title, description, hidden).Value;

    private static IReadOnlyList<NctDetailedNoteDto> Catalog() => new[]
    {
        Note(1, "Immobilisations incorporelles"),
        Note(3, "Immobilisations corporelles"),
        Note(4, "Amortissements")
    };

    // ── Test de gel ────────────────────────────────────────────────────────────

    [Fact]
    public void WithoutOverrides_ReturnsSameInstance()
    {
        var notes = Catalog();

        var result = NctNoteOverrideApplier.Apply(notes, Array.Empty<NctNoteOverride>());

        // Même référence : neutralité absolue, aucune allocation ni recopie.
        Assert.Same(notes, result);
    }

    [Fact]
    public void WithNullOverrides_ReturnsSameInstance()
    {
        var notes = Catalog();
        Assert.Same(notes, NctNoteOverrideApplier.Apply(notes, null!));
    }

    [Fact]
    public void OverrideOnAbsentNote_LeavesEverythingUnchanged()
    {
        var notes = Catalog();

        // La note 99 n'existe pas au catalogue : l'override est sans effet.
        var result = NctNoteOverrideApplier.Apply(notes, new[] { Override(99, "Fantôme") });

        Assert.Equal(3, result.Count);
        Assert.Equal("Immobilisations incorporelles", result[0].Title);
    }

    // ── Titre ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomTitle_ReplacesCatalogTitle()
    {
        var result = NctNoteOverrideApplier.Apply(Catalog(), new[] { Override(3, "Immobilisations — détail société") });

        var note = result.Single(n => n.Number == 3);
        Assert.Equal("Immobilisations — détail société", note.Title);
        // Les autres notes et les montants sont intacts.
        Assert.Equal("Amortissements", result.Single(n => n.Number == 4).Title);
        Assert.Equal(100m, note.Total);
    }

    [Fact]
    public void BlankCustomTitle_KeepsCatalogTitle()
    {
        // Un titre vide rend la main au catalogue au lieu d'effacer le libellé.
        var result = NctNoteOverrideApplier.Apply(Catalog(), new[] { Override(3, "   ") });

        Assert.Equal("Immobilisations corporelles", result.Single(n => n.Number == 3).Title);
    }

    // ── Description ────────────────────────────────────────────────────────────

    [Fact]
    public void CustomDescription_IsAttached()
    {
        var result = NctNoteOverrideApplier.Apply(
            Catalog(), new[] { Override(1, description: "Méthode linéaire, durée 5 ans.") });

        Assert.Equal("Méthode linéaire, durée 5 ans.", result.Single(n => n.Number == 1).Description);
        Assert.Null(result.Single(n => n.Number == 3).Description);
    }

    // ── Masquage ───────────────────────────────────────────────────────────────

    [Fact]
    public void HiddenNote_IsRemovedFromLiasse()
    {
        var result = NctNoteOverrideApplier.Apply(Catalog(), new[] { Override(4, hidden: true) });

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, n => n.Number == 4);
    }

    [Fact]
    public void HiddenTakesPrecedenceOverTitle()
    {
        var result = NctNoteOverrideApplier.Apply(
            Catalog(), new[] { Override(4, "Titre ignoré", "Texte ignoré", hidden: true) });

        Assert.DoesNotContain(result, n => n.Number == 4);
    }

    [Fact]
    public void MultipleOverrides_AreAllApplied()
    {
        var result = NctNoteOverrideApplier.Apply(Catalog(), new[]
        {
            Override(1, "Incorporelles"),
            Override(3, description: "Détail par catégorie"),
            Override(4, hidden: true)
        });

        Assert.Equal(2, result.Count);
        Assert.Equal("Incorporelles", result.Single(n => n.Number == 1).Title);
        Assert.Equal("Détail par catégorie", result.Single(n => n.Number == 3).Description);
    }

    // ── Validation du domaine ──────────────────────────────────────────────────

    [Fact]
    public void Create_RejectsInvalidYearAndNumber()
    {
        Assert.True(NctNoteOverride.Create(1900, 1, null, null, false).IsFailure);
        Assert.True(NctNoteOverride.Create(2026, 0, null, null, false).IsFailure);
    }

    [Fact]
    public void Create_RejectsOversizedText()
    {
        Assert.True(NctNoteOverride.Create(2026, 1, new string('x', 301), null, false).IsFailure);
        Assert.True(NctNoteOverride.Create(2026, 1, null, new string('x', 2001), false).IsFailure);
    }
}
