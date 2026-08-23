using System;
using System.Linq;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class NumberingSchemeDefaultsTests
{
    // Exact JSON shape written by the BootstrapDocumentNumberingSchemesFromLegacy migration
    // (PascalCase keys). Existing databases still hold rows in this form.
    private const string PascalCaseInvoiceFormat =
        "[{\"Type\":0,\"Value\":\"FAC\",\"Order\":0}," +
        "{\"Type\":1,\"Value\":\"-\",\"Order\":1}," +
        "{\"Type\":9,\"Value\":null,\"Order\":2}," +
        "{\"Type\":1,\"Value\":\"-\",\"Order\":3}," +
        "{\"Type\":5,\"Value\":null,\"Order\":4}]";

    [Fact]
    public void DeserializeBlocks_ReadsPascalCaseFormat_PreservingBlockTypes()
    {
        // Regression guard for the root cause of the POS/wizard "Erreur lors de la création de la facture":
        // case-sensitive deserialization dropped every Type to FreeText, leaving no document-number block.
        var blocks = NumberingSchemeDefaults.DeserializeBlocks(PascalCaseInvoiceFormat);

        Assert.Equal(5, blocks.Count);
        Assert.Equal(NumberingBlockType.FreeText, blocks[0].Type);
        Assert.Equal("FAC", blocks[0].Value);
        Assert.Equal(NumberingBlockType.Year4, blocks[2].Type);
        Assert.Equal(NumberingBlockType.DocumentNumberPadded5, blocks[4].Type);
        Assert.Contains(blocks, b => b.Type.IsDocumentNumberBlock());
    }

    [Fact]
    public void Render_FromPascalCaseFormat_ProducesValidNumber()
    {
        var blocks = NumberingSchemeDefaults.DeserializeBlocks(PascalCaseInvoiceFormat);

        var render = NumberingFormatRenderer.Render(
            blocks,
            new NumberingFormatRenderer.RenderContext(42, new DateTime(2026, 6, 21), "FAC"));

        Assert.True(render.IsSuccess);
        Assert.Equal("FAC-2026-00042", render.Value);
    }

    [Theory]
    [InlineData(NumberingDocumentType.StockEntry, "BE")]
    [InlineData(NumberingDocumentType.StockIssue, "BS")]
    public void GetDefaultBlocks_StockVouchers_UseDedicatedPrefixes(
        NumberingDocumentType documentType,
        string prefix)
    {
        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(documentType);
        Assert.Equal(prefix, blocks[0].Value);
        Assert.NotEqual("BR", prefix);
        Assert.NotEqual("BL", prefix);
        Assert.NotEqual("TR", prefix);
    }

    [Fact]
    public void DeserializeBlocks_ReadsCamelCaseFormat_RoundTripsWithSerialize()
    {
        // The app's own SerializeBlocks writes camelCase (via [JsonPropertyName]); it must keep working.
        var original = NumberingSchemeDefaults.GetDefaultBlocks(NumberingDocumentType.Invoice);
        var json = NumberingSchemeDefaults.SerializeBlocks(original);

        var roundTripped = NumberingSchemeDefaults.DeserializeBlocks(json);

        Assert.Equal(original.Count, roundTripped.Count);
        Assert.True(NumberingFormatRenderer.ValidateBlocks(roundTripped).IsSuccess);
        Assert.Contains(roundTripped, b => b.Type.IsDocumentNumberBlock());
    }
}
