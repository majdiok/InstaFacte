using FactuTrust.Application.Features.Channels;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Dégradation Markdown → WhatsApp : gras, titres, liens, tableaux monospace, citations/règles
/// retirées, compression des lignes vides ; découpage aux frontières de paragraphes avec suffixe (i/n).
/// </summary>
public sealed class WhatsAppTextFormatterTests
{
    // ── Format ──

    [Fact]
    public void DoubleStarBold_BecomesWhatsAppBold()
    {
        Assert.Equal("Le CA est de *12 500 TND* ce mois.",
            WhatsAppTextFormatter.Format("Le CA est de **12 500 TND** ce mois."));
    }

    [Fact]
    public void DoubleUnderscoreBold_BecomesWhatsAppBold()
    {
        Assert.Equal("Total : *1 000*", WhatsAppTextFormatter.Format("Total : __1 000__"));
    }

    [Theory]
    [InlineData("# Synthèse", "*Synthèse*")]
    [InlineData("### Détail des ventes", "*Détail des ventes*")]
    public void Headings_BecomeBoldLines(string input, string expected)
    {
        Assert.Equal(expected, WhatsAppTextFormatter.Format(input));
    }

    [Fact]
    public void Links_BecomeTextWithUrlInParentheses()
    {
        Assert.Equal("Voir la facture (https://app.local/invoices/42)",
            WhatsAppTextFormatter.Format("Voir [la facture](https://app.local/invoices/42)"));
    }

    [Fact]
    public void HorizontalRules_AreRemoved_AndBlankLinesCompressed()
    {
        var result = WhatsAppTextFormatter.Format("Avant\n\n---\n\n\n\nAprès");

        Assert.Equal("Avant\n\nAprès", result);
    }

    [Fact]
    public void Blockquotes_AreUnquoted_AndStarBullets_BecomeDashes()
    {
        var result = WhatsAppTextFormatter.Format("> Citation\n* premier\n* second");

        Assert.Equal("Citation\n- premier\n- second", result);
    }

    [Fact]
    public void MarkdownTable_BecomesAlignedMonospaceBlock()
    {
        var table = "| Client | CA |\n|---|---|\n| Alpha | 100 |\n| Beta | 2500 |";

        var result = WhatsAppTextFormatter.Format(table);

        Assert.StartsWith("```", result);
        Assert.EndsWith("```", result);
        Assert.Contains("Client  CA", result);
        Assert.Contains("Alpha   100", result);
        Assert.Contains("Beta    2500", result);
        Assert.DoesNotContain("|", result);
        Assert.DoesNotContain("---", result);
    }

    [Fact]
    public void EmptyInput_GivesEmptyString()
    {
        Assert.Equal(string.Empty, WhatsAppTextFormatter.Format(null));
        Assert.Equal(string.Empty, WhatsAppTextFormatter.Format("   "));
    }

    // ── Split ──

    [Fact]
    public void ShortText_IsASingleChunk_WithoutSuffix()
    {
        var chunks = WhatsAppTextFormatter.Split("Bonjour", 100);

        Assert.Single(chunks);
        Assert.Equal("Bonjour", chunks[0]);
    }

    [Fact]
    public void LongText_IsSplitOnParagraphBoundaries_WithIndexSuffixes()
    {
        var p1 = new string('a', 80);
        var p2 = new string('b', 80);
        var p3 = new string('c', 80);

        var chunks = WhatsAppTextFormatter.Split($"{p1}\n\n{p2}\n\n{p3}", 100);

        Assert.Equal(3, chunks.Count);
        Assert.EndsWith("(1/3)", chunks[0]);
        Assert.EndsWith("(3/3)", chunks[2]);
        Assert.StartsWith(p1, chunks[0]);
        Assert.StartsWith(p2, chunks[1]);
        Assert.StartsWith(p3, chunks[2]);
        Assert.All(chunks, c => Assert.True(c.Length <= 100, $"chunk de {c.Length} > 100"));
    }

    [Fact]
    public void OversizedSingleParagraph_IsHardSplit_WithinBudget()
    {
        var chunks = WhatsAppTextFormatter.Split(new string('x', 500), 100);

        Assert.True(chunks.Count >= 5);
        Assert.All(chunks, c => Assert.True(c.Length <= 100, $"chunk de {c.Length} > 100"));
    }

    [Fact]
    public void EmptyText_GivesNoChunk()
    {
        Assert.Empty(WhatsAppTextFormatter.Split("  ", 100));
        Assert.Empty(WhatsAppTextFormatter.Split(null, 100));
    }
}
