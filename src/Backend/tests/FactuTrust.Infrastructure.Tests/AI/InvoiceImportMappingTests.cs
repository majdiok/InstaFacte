using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Tests des fonctions de normalisation et de mapping de l'import de facture :
/// taux de TVA, dates, devise, type de document, niveau de confiance, nettoyage
/// de chaînes et conversion des lignes d'articles.
/// </summary>
public sealed class InvoiceImportMappingTests
{
    // ── ClampVatRate ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(13, 13)]
    [InlineData(19, 19)]
    [InlineData(3, 0)]     // arrondi au taux tunisien légal le plus proche
    [InlineData(8, 7)]
    [InlineData(15, 13)]
    [InlineData(20, 19)]
    [InlineData(25, 19)]
    [InlineData(-5, 0)]
    public void ClampVatRate_SnapsToNearestTunisianRate(int input, int expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.ClampVatRate(input));
    }

    [Fact]
    public void ClampVatRate_Null_DefaultsToStandardRate()
    {
        Assert.Equal(19, InvoiceImportParsing.ClampVatRate(null));
    }

    // ── ParseDate ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2024-03-15", 2024, 3, 15)]   // ISO AAAA-MM-JJ
    [InlineData("15/03/2024", 2024, 3, 15)]   // JJ/MM/AAAA
    [InlineData("31/12/2025", 2025, 12, 31)]
    [InlineData("25-12-2026", 2026, 12, 25)]  // JJ-MM-AAAA
    public void ParseDate_RecognizedFormats_AreParsed(string raw, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), InvoiceImportParsing.ParseDate(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("date inconnue")]
    [InlineData("2024-13-45")]
    public void ParseDate_MissingOrInvalid_ReturnsNull(string? raw)
    {
        Assert.Null(InvoiceImportParsing.ParseDate(raw));
    }

    // ── NormalizeCurrency ──────────────────────────────────────────────────

    [Theory]
    [InlineData("TND", "TND")]
    [InlineData("eur", "EUR")]
    [InlineData(" usd ", "USD")]
    [InlineData("GBP", "TND")]    // devise non supportée => TND
    [InlineData("", "TND")]
    [InlineData(null, "TND")]
    public void NormalizeCurrency_NormalizesOrFallsBackToTnd(string? raw, string expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.NormalizeCurrency(raw));
    }

    // ── NormalizeDocumentType / IsUnknownDocumentType ──────────────────────

    [Theory]
    [InlineData("INVOICE", "INVOICE")]
    [InlineData("invoice", "INVOICE")]
    [InlineData("CREDIT_NOTE", "CREDIT_NOTE")]
    [InlineData("credit_note", "CREDIT_NOTE")]
    [InlineData("DELIVERY_NOTE", "DELIVERY_NOTE")]
    [InlineData("PROFORMA", "PROFORMA")]
    [InlineData("UNKNOWN", "INVOICE")]          // type inconnu => INVOICE par défaut
    [InlineData("n'importe quoi", "INVOICE")]
    [InlineData(null, "INVOICE")]
    public void NormalizeDocumentType_NormalizesOrFallsBackToInvoice(string? raw, string expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.NormalizeDocumentType(raw));
    }

    [Theory]
    [InlineData("UNKNOWN", true)]
    [InlineData("unknown", true)]
    [InlineData(" Unknown ", true)]
    [InlineData("INVOICE", false)]
    [InlineData("CREDIT_NOTE", false)]
    [InlineData(null, false)]
    public void IsUnknownDocumentType_DetectsNonInvoiceFlag(string? raw, bool expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.IsUnknownDocumentType(raw));
    }

    [Theory]
    [InlineData("DELIVERY_NOTE", true)]
    [InlineData("delivery_note", true)]
    [InlineData("INVOICE", false)]
    public void IsDeliveryNoteDocumentType_DetectsDeliveryNote(string? raw, bool expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.IsDeliveryNoteDocumentType(raw));
    }

    // ── NormalizeConfidence ────────────────────────────────────────────────

    [Theory]
    [InlineData("high", "high")]
    [InlineData("MEDIUM", "medium")]
    [InlineData("Low", "low")]
    [InlineData("très sûr", "medium")]   // valeur inattendue => medium
    [InlineData(null, "medium")]
    public void NormalizeConfidence_NormalizesOrFallsBackToMedium(string? raw, string expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.NormalizeConfidence(raw));
    }

    // ── CleanOrNull ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("    ", null)]
    [InlineData("  Société ACME  ", "Société ACME")]
    public void CleanOrNull_TrimsOrReturnsNull(string? raw, string? expected)
    {
        Assert.Equal(expected, InvoiceImportParsing.CleanOrNull(raw));
    }

    // ── MapLine ────────────────────────────────────────────────────────────

    [Fact]
    public void MapLine_Null_ReturnsNull()
    {
        Assert.Null(InvoiceImportParsing.MapLine(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapLine_MissingDesignation_ReturnsNullSoLineIsSkipped(string? designation)
    {
        var line = new LlmInvoiceLine { Designation = designation, Quantity = 2m, UnitPriceHT = 10m };
        Assert.Null(InvoiceImportParsing.MapLine(line));
    }

    [Fact]
    public void MapLine_TrimsDesignationAndKeepsValidValues()
    {
        var line = new LlmInvoiceLine
        {
            Designation = "  Prestation de conseil  ",
            Description = "  détaillée  ",
            Quantity = 3m,
            Unit = " Heure ",
            UnitPriceHT = 150.5m,
            DiscountPercent = 10m,
            VatRatePercent = 19
        };

        var mapped = InvoiceImportParsing.MapLine(line);

        Assert.NotNull(mapped);
        Assert.Equal("Prestation de conseil", mapped!.Designation);
        Assert.Equal("détaillée", mapped.Description);
        Assert.Equal(3m, mapped.Quantity);
        Assert.Equal("Heure", mapped.Unit);
        Assert.Equal(150.5m, mapped.UnitPriceHT);
        Assert.Equal(10m, mapped.DiscountPercent);
        Assert.Equal(19, mapped.VatRatePercent);
    }

    [Theory]
    [InlineData(null, 1.0)]    // quantité absente => 1
    [InlineData(0.0, 1.0)]     // quantité nulle => 1
    [InlineData(-4.0, 1.0)]    // quantité négative => 1
    [InlineData(5.0, 5.0)]
    public void MapLine_NormalizesQuantity(double? rawQuantity, double expected)
    {
        var line = new LlmInvoiceLine { Designation = "Article", Quantity = (decimal?)rawQuantity };

        var mapped = InvoiceImportParsing.MapLine(line);

        Assert.NotNull(mapped);
        Assert.Equal((decimal)expected, mapped!.Quantity);
    }

    [Theory]
    [InlineData(null, 0.0)]    // prix absent => 0
    [InlineData(-12.5, 0.0)]   // prix négatif => 0
    [InlineData(99.999, 99.999)]
    public void MapLine_NormalizesUnitPrice(double? rawPrice, double expected)
    {
        var line = new LlmInvoiceLine { Designation = "Article", UnitPriceHT = (decimal?)rawPrice };

        var mapped = InvoiceImportParsing.MapLine(line);

        Assert.NotNull(mapped);
        Assert.Equal((decimal)expected, mapped!.UnitPriceHT);
    }

    [Theory]
    [InlineData(null, null)]   // remise absente => null
    [InlineData(20.0, 20.0)]
    [InlineData(150.0, 100.0)] // remise > 100 % bornée à 100
    [InlineData(-10.0, 0.0)]   // remise négative bornée à 0
    public void MapLine_ClampsDiscountPercent(double? rawDiscount, double? expected)
    {
        var line = new LlmInvoiceLine { Designation = "Article", DiscountPercent = (decimal?)rawDiscount };

        var mapped = InvoiceImportParsing.MapLine(line);

        Assert.NotNull(mapped);
        Assert.Equal((decimal?)expected, mapped!.DiscountPercent);
    }

    [Fact]
    public void MapLine_AppliesVatRateClamping()
    {
        var line = new LlmInvoiceLine { Designation = "Article", VatRatePercent = 8 };

        var mapped = InvoiceImportParsing.MapLine(line);

        Assert.NotNull(mapped);
        Assert.Equal(7, mapped!.VatRatePercent);   // 8 => taux réduit 7 %
    }

    // ── ResolveImportNumCtx ────────────────────────────────────────────────

    [Fact]
    public void ResolveImportNumCtx_NotConfigured_ReturnsNull()
    {
        // num_ctx <= 0 => on laisse Ollama choisir.
        Assert.Null(InvoiceImportParsing.ResolveImportNumCtx(0, 5000, 2048, false));
    }

    [Fact]
    public void ResolveImportNumCtx_WithImages_ReturnsFullConfiguredWindow()
    {
        // Mode vision : on conserve la pleine fenêtre configurée (aucune régression).
        Assert.Equal(16384, InvoiceImportParsing.ResolveImportNumCtx(16384, 5000, 2048, true));
    }

    [Fact]
    public void ResolveImportNumCtx_SmallInvoice_ReturnsMinimalWindow()
    {
        // ~3000 caractères => ~1000 tokens + 2048 + 512 => arrondi au plancher 4096.
        Assert.Equal(4096, InvoiceImportParsing.ResolveImportNumCtx(16384, 3000, 2048, false));
    }

    [Fact]
    public void ResolveImportNumCtx_MediumInvoice_ReturnsIntermediateWindow()
    {
        // ~15000 caractères => ~5000 tokens + 2048 + 512 => arrondi 8192.
        Assert.Equal(8192, InvoiceImportParsing.ResolveImportNumCtx(16384, 15000, 2048, false));
    }

    [Fact]
    public void ResolveImportNumCtx_HugeDocument_IsCappedAtConfiguredMaximum()
    {
        // Document énorme => borné à la valeur configurée : identique au comportement actuel.
        Assert.Equal(16384, InvoiceImportParsing.ResolveImportNumCtx(16384, 60000, 3072, false));
    }

    [Fact]
    public void ResolveImportNumCtx_NeverExceedsConfiguredMaximumNorFallsBelowFloor()
    {
        var result = InvoiceImportParsing.ResolveImportNumCtx(8192, 40000, 3072, false);
        Assert.True(result <= 8192, $"num_ctx {result} ne doit jamais dépasser le maximum configuré.");
        Assert.True(result >= 4096, $"num_ctx {result} doit rester au moins égal au plancher.");
    }

    [Fact]
    public void ResolveImportNumCtx_ConfiguredBelowFloor_StaysWithinBoundsWithoutThrowing()
    {
        // Garde-fou : un num_ctx configuré sous le plancher de 4096 ne doit pas faire échouer Math.Clamp.
        Assert.Equal(2048, InvoiceImportParsing.ResolveImportNumCtx(2048, 3000, 2048, false));
    }
}
