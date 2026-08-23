using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1, lot 1 — la commande client s'inscrit dans la numérotation documentaire existante
/// plutôt que d'en inventer une seconde : même schéma configurable, même verrouillage du
/// format au premier document émis, même réservation atomique.
/// </summary>
public sealed class SalesOrderNumberingTests
{
    [Fact]
    public void SalesOrder_IsAppendedAtTheEndOfTheEnum()
    {
        // Les valeurs sont persistées en entier dans DocumentNumberingSchemes.DocumentType :
        // réindexer réaffecterait les schémas de numérotation déjà en base.
        Assert.Equal(10, (int)NumberingDocumentType.SalesOrder);
        Assert.Equal(9, (int)NumberingDocumentType.BankDeposit);
    }

    [Fact]
    public void DefaultPrefix_IsCde()
    {
        Assert.Equal("CDE", NumberingDocumentType.SalesOrder.DefaultFreeText());
        Assert.Equal("Commande client", NumberingDocumentType.SalesOrder.ToDisplayString());
    }

    [Fact]
    public void LegacyPrefix_MapsBackToSalesOrder()
    {
        Assert.Equal(NumberingDocumentType.SalesOrder, DocumentNumberMapper.ToNumberingType("CDE"));
        Assert.Equal(NumberingDocumentType.SalesOrder, DocumentNumberMapper.ToNumberingType("cde"));
        Assert.Equal(NumberingDocumentType.SalesReturnNote, DocumentNumberMapper.ToNumberingType("BRT"));
    }

    [Fact]
    public void DefaultScheme_ProducesTheExpectedFormat()
    {
        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(NumberingDocumentType.SalesOrder);

        // FreeText - Year4 - DocumentNumberPadded6, comme devis, facture et bon de commande.
        Assert.Equal(5, blocks.Count);
        Assert.Equal(NumberingBlockType.FreeText, blocks[0].Type);
        Assert.Equal("CDE", blocks[0].Value);
        Assert.Equal(NumberingBlockType.Year4, blocks[2].Type);
        Assert.Equal(NumberingBlockType.DocumentNumberPadded6, blocks[4].Type);
    }

    [Fact]
    public void Number_IsRenderedAsCdeYearSequence()
    {
        var number = SalesOrderNumber.Create("CDE", 2026, 1);

        Assert.Equal("CDE-2026-000001", number.Value);
        Assert.Equal("CDE", number.Prefix);
        Assert.Equal(2026, number.Year);
        Assert.Equal(1, number.Sequence);
    }

    [Fact]
    public void Number_RoundTripsThroughParse()
    {
        var parsed = SalesOrderNumber.Parse("CDE-2026-000042");

        Assert.True(parsed.IsSuccess, parsed.Error?.Description);
        Assert.Equal("CDE-2026-000042", parsed.Value.Value);
        Assert.Equal(42, parsed.Value.Sequence);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CDE-2026")]
    [InlineData("CDE-XXXX-000001")]
    public void Number_RejectsMalformedValues(string value)
    {
        Assert.True(SalesOrderNumber.Parse(value).IsFailure);
    }

    [Fact]
    public void Next_IncrementsTheSequenceOnly()
    {
        var next = SalesOrderNumber.Create("CDE", 2026, 7).Next();

        Assert.Equal("CDE-2026-000008", next.Value);
        Assert.Equal(2026, next.Year);
    }
}
