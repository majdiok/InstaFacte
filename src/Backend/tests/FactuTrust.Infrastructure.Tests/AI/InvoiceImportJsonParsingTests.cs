using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Tests de <see cref="InvoiceImportParsing.ExtractFirstJsonObject"/> : isolement
/// robuste du premier objet JSON équilibré dans une réponse LLM imparfaite
/// (texte parasite, clôtures markdown, objets multiples, JSON tronqué).
/// </summary>
public sealed class InvoiceImportJsonParsingTests
{
    [Fact]
    public void ExtractFirstJsonObject_PureJson_ReturnsItUnchanged()
    {
        const string json = "{\"documentType\":\"INVOICE\",\"currency\":\"TND\"}";
        Assert.Equal(json, InvoiceImportParsing.ExtractFirstJsonObject(json));
    }

    [Fact]
    public void ExtractFirstJsonObject_WithTextPrefix_ReturnsOnlyTheObject()
    {
        var raw = "Voici les données extraites : {\"a\":1} — j'espère que cela convient.";
        Assert.Equal("{\"a\":1}", InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_WithMarkdownFences_ReturnsTheObject()
    {
        var raw = "```json\n{\"a\":1}\n```";
        Assert.Equal("{\"a\":1}", InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_NestedObjects_ReturnsFullBalancedObject()
    {
        var raw = "{\"client\":{\"name\":\"ACME\"},\"lines\":[{\"q\":1}]}";
        Assert.Equal(raw, InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_BraceInsideStringValue_IsNotFooled()
    {
        // L'accolade fermante à l'intérieur d'une chaîne ne doit pas clore l'objet.
        var raw = "{\"designation\":\"Lot A } reste\"}";
        Assert.Equal(raw, InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_EscapedQuoteInsideString_IsHandled()
    {
        var raw = "{\"note\":\"il a dit \\\"bonjour\\\"\"}";
        Assert.Equal(raw, InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_TrailingTextAfterObject_IsStripped()
    {
        var raw = "{\"a\":1}\nFin de la réponse.";
        Assert.Equal("{\"a\":1}", InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_MultipleObjects_ReturnsTheFirstOne()
    {
        var raw = "{\"first\":1}{\"second\":2}";
        Assert.Equal("{\"first\":1}", InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Fact]
    public void ExtractFirstJsonObject_UnbalancedTruncatedObject_ReturnsNull()
    {
        // Accolade fermante manquante : réponse LLM tronquée.
        var raw = "{\"a\":1,\"b\":{\"c\":2}";
        Assert.Null(InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Aucune donnée structurée n'a pu être produite.")]
    public void ExtractFirstJsonObject_NoJson_ReturnsNull(string? raw)
    {
        Assert.Null(InvoiceImportParsing.ExtractFirstJsonObject(raw));
    }
}
