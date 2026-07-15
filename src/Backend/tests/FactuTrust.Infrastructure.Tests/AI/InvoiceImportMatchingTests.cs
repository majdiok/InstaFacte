using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Tests des primitives de rapprochement clients/produits de l'import de facture :
/// normalisation du matricule fiscal (NIF) et similarité de noms, qui décident
/// d'un rapprochement exact, flou ou ambigu.
/// </summary>
public sealed class InvoiceImportMatchingTests
{
    // ── NormalizeNif ───────────────────────────────────────────────────────

    [Fact]
    public void NormalizeNif_StripsSeparatorsAndUppercases()
    {
        Assert.Equal("1234567ABM000", InvoiceImportParsing.NormalizeNif("1234567/a/b/m/000"));
    }

    [Fact]
    public void NormalizeNif_IgnoresFormattingDifferences_SoEquivalentNifsMatch()
    {
        var a = InvoiceImportParsing.NormalizeNif("1234567/A/M/000");
        var b = InvoiceImportParsing.NormalizeNif("  1234567 A M 000  ");
        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalizeNif_DistinctNifs_DoNotMatch()
    {
        var a = InvoiceImportParsing.NormalizeNif("1234567/A/M/000");
        var b = InvoiceImportParsing.NormalizeNif("7654321/B/N/000");
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NormalizeNif_NullOrEmpty_ReturnsEmptyString(string? nif)
    {
        Assert.Equal(string.Empty, InvoiceImportParsing.NormalizeNif(nif));
    }

    // ── Similarity ─────────────────────────────────────────────────────────

    [Fact]
    public void Similarity_IdenticalStrings_ReturnsOne()
    {
        Assert.Equal(1d, InvoiceImportParsing.Similarity("Société Alpha", "Société Alpha"));
    }

    [Fact]
    public void Similarity_IsCaseInsensitive()
    {
        Assert.Equal(1d, InvoiceImportParsing.Similarity("BOULANGERIE ENNOUR", "boulangerie ennour"));
    }

    [Theory]
    [InlineData(null, "Société Alpha")]
    [InlineData("Société Alpha", null)]
    [InlineData("", "Société Alpha")]
    public void Similarity_MissingOperand_ReturnsZero(string? a, string? b)
    {
        Assert.Equal(0d, InvoiceImportParsing.Similarity(a, b));
    }

    [Fact]
    public void Similarity_NearIdenticalNames_ClearTheMatchThreshold()
    {
        // Accents manquants sur un nom long (cas OCR fréquent) : l'écart relatif
        // reste faible, donc le score demeure au-dessus du seuil de rapprochement.
        var score = InvoiceImportParsing.Similarity(
            "Société Tunisienne de Distribution",
            "Societe Tunisienne de Distribution");
        Assert.True(
            score >= InvoiceImportParsing.NameMatchThreshold,
            $"Score attendu >= {InvoiceImportParsing.NameMatchThreshold}, obtenu {score}.");
    }

    [Fact]
    public void Similarity_DifferentNames_StayBelowTheMatchThreshold()
    {
        var score = InvoiceImportParsing.Similarity("Pharmacie Centrale", "Garage du Nord");
        Assert.True(
            score < InvoiceImportParsing.NameMatchThreshold,
            $"Score attendu < {InvoiceImportParsing.NameMatchThreshold}, obtenu {score}.");
    }

    [Fact]
    public void NameMatchThreshold_IsNinetyPercent()
    {
        Assert.Equal(0.9d, InvoiceImportParsing.NameMatchThreshold);
    }
}
