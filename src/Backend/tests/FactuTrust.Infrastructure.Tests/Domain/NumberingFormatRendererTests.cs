using Xunit;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class NumberingFormatRendererTests
{
    private static readonly DateTime RefDate = new(2026, 6, 14);

    [Fact]
    public void Render_LegacyInvoiceFormat_MatchesExpected()
    {
        var blocks = new List<NumberingFormatBlock>
        {
            new(NumberingBlockType.FreeText, "FAC", 0),
            new(NumberingBlockType.Separator, "-", 1),
            new(NumberingBlockType.Year4, null, 2),
            new(NumberingBlockType.Separator, "-", 3),
            new(NumberingBlockType.DocumentNumberPadded6, null, 4)
        };
        var result = NumberingFormatRenderer.Render(blocks, new NumberingFormatRenderer.RenderContext(101, RefDate));
        Assert.True(result.IsSuccess);
        Assert.Equal("FAC-2026-000101", result.Value);
    }

    [Fact]
    public void Render_AxeaneStyleFormat_MatchesExpected()
    {
        var blocks = new List<NumberingFormatBlock>
        {
            new(NumberingBlockType.FreeText, "Fac", 0),
            new(NumberingBlockType.Separator, "-", 1),
            new(NumberingBlockType.DocumentNumberPadded3, null, 2),
            new(NumberingBlockType.Separator, "-", 3),
            new(NumberingBlockType.Year2, null, 4)
        };
        var result = NumberingFormatRenderer.Render(blocks, new NumberingFormatRenderer.RenderContext(101, RefDate));
        Assert.True(result.IsSuccess);
        Assert.Equal("FAC-101-26", result.Value);
    }

    [Fact]
    public void ValidateBlocks_WithoutDocumentNumber_Fails()
    {
        var blocks = new List<NumberingFormatBlock>
        {
            new(NumberingBlockType.FreeText, "FAC", 0),
            new(NumberingBlockType.Separator, "-", 1),
            new(NumberingBlockType.Year4, null, 2)
        };
        Assert.True(NumberingFormatRenderer.ValidateBlocks(blocks).IsFailure);
    }
}