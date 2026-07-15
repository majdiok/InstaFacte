using FactuTrust.Application.Features.Channels;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Parseur des commandes de canal : LIER/DELIER/AIDE insensibles à la casse et aux accents,
/// normalisation du code (majuscules, sans espaces/tirets), tout le reste = question libre.
/// </summary>
public sealed class ChannelCommandParserTests
{
    [Theory]
    [InlineData("LIER ABCD2345", "ABCD2345")]
    [InlineData("lier abcd2345", "ABCD2345")]
    [InlineData("  Lier  AB-CD 23 45  ", "ABCD2345")]
    [InlineData("LINK ABCD2345", "ABCD2345")]
    public void Link_IsParsed_WithNormalizedCode(string input, string expectedCode)
    {
        var command = ChannelCommandParser.Parse(input);

        Assert.Equal(ChannelCommandKind.Link, command.Kind);
        Assert.Equal(expectedCode, command.Argument);
    }

    [Fact]
    public void Link_WithoutCode_HasNullArgument()
    {
        var command = ChannelCommandParser.Parse("LIER    ");

        Assert.Equal(ChannelCommandKind.Link, command.Kind);
        Assert.Null(command.Argument);
    }

    [Theory]
    [InlineData("DELIER")]
    [InlineData("délier")]
    [InlineData("Délier")]
    [InlineData("  DELIER  ")]
    public void Unlink_IsParsed_CaseAndAccentInsensitive(string input)
    {
        Assert.Equal(ChannelCommandKind.Unlink, ChannelCommandParser.Parse(input).Kind);
    }

    [Theory]
    [InlineData("AIDE")]
    [InlineData("aide")]
    [InlineData("Aïde")]
    [InlineData("help")]
    [InlineData("?")]
    public void Help_IsParsed(string input)
    {
        Assert.Equal(ChannelCommandKind.Help, ChannelCommandParser.Parse(input).Kind);
    }

    [Theory]
    [InlineData("Quel est mon CA ce mois-ci ?")]
    [InlineData("liermoi un rapport")] // « LIER » sans espace = question
    [InlineData("délier mon compte stp")] // « DELIER » avec suite = question
    [InlineData("")]
    [InlineData(null)]
    public void Everything_Else_IsAQuestion(string? input)
    {
        Assert.Equal(ChannelCommandKind.Question, ChannelCommandParser.Parse(input).Kind);
    }

    [Theory]
    [InlineData("ab-cd 23.45", "ABCD2345")]
    [InlineData("  a b c  ", "ABC")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeLinkCode_KeepsOnlyAlphanumerics_Uppercased(string? raw, string expected)
    {
        Assert.Equal(expected, ChannelCommandParser.NormalizeLinkCode(raw));
    }
}
