using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

/// <summary>
/// Verifies the lightweight Markdown → OpenXml conversion used to render assistant text without
/// pulling in <c>Markdig</c>. Tests are intentionally structural (paragraph counts, run formatting)
/// rather than full XML comparisons so the converter can evolve internally.
/// </summary>
public sealed class MarkdownToOpenXmlConverterTests
{
    private readonly IPowerPointTheme _theme = new TestTheme();

    [Fact]
    public void Convert_empty_input_returns_no_paragraphs()
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert(string.Empty, _theme);
        Assert.Empty(paragraphs);
    }

    [Fact]
    public void Convert_simple_paragraph_yields_one_paragraph_with_text_run()
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert("Hello world", _theme);
        var list = paragraphs.ToList();
        Assert.Single(list);
        Assert.Equal("Hello world", FirstRunText(list[0]));
    }

    [Fact]
    public void Convert_heading_uses_bigger_font_size()
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert("## Section", _theme);
        var list = paragraphs.ToList();
        Assert.Single(list);

        var run = list[0].Elements<A.Run>().First();
        var rPr = run.RunProperties!;
        Assert.True(rPr.FontSize!.Value > 1400, "Heading font size should be larger than body.");
        Assert.True(rPr.Bold?.Value == true, "Heading runs should be bold.");
    }

    [Fact]
    public void Convert_bullet_list_emits_one_paragraph_per_bullet()
    {
        var markdown = """
        - First item
        - Second item
        - Third item
        """;

        var paragraphs = MarkdownToOpenXmlConverter.Convert(markdown, _theme);
        var list = paragraphs.Where(p => p.Elements<A.Run>().Any()).ToList();
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void Convert_inline_bold_creates_dedicated_bold_run()
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert("Plain **bold** end", _theme);
        var list = paragraphs.ToList();
        Assert.Single(list);

        var runs = list[0].Elements<A.Run>().ToList();
        Assert.True(runs.Count >= 3, $"Expected at least 3 runs, got {runs.Count}.");
        Assert.Contains(runs, r => r.RunProperties?.Bold?.Value == true && r.Text!.Text == "bold");
    }

    [Fact]
    public void Convert_strips_json_fences_silently()
    {
        var markdown = """
        Header text
        ```json
        { "title": "X" }
        ```
        Footer text
        """;

        var paragraphs = MarkdownToOpenXmlConverter.Convert(markdown, _theme).ToList();
        var allText = string.Concat(paragraphs.SelectMany(p => p.Elements<A.Run>()).Select(r => r.Text?.Text));
        Assert.DoesNotContain("\"title\"", allText);
        Assert.Contains("Header text", allText);
        Assert.Contains("Footer text", allText);
    }

    [Fact]
    public void Convert_inline_link_renders_label_only()
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert("See [docs](https://example.com).", _theme);
        var list = paragraphs.ToList();
        var allText = string.Concat(list[0].Elements<A.Run>().Select(r => r.Text?.Text));
        Assert.Contains("docs", allText);
        Assert.DoesNotContain("https://", allText);
    }

    private static string FirstRunText(A.Paragraph paragraph) =>
        paragraph.Elements<A.Run>().FirstOrDefault()?.Text?.Text ?? string.Empty;

    private sealed class TestTheme : IPowerPointTheme
    {
        public PowerPointTemplate Template => PowerPointTemplate.Standard;
        public string Name => "Test";
        public string Description => "Test theme";
        public PowerPointThemeCategory Category => PowerPointThemeCategory.Light;
        public bool IsDark => false;
        public int SortOrder => 0;
        public string? PreviewGradientCss => null;
        public PowerPointThemeEngine Engine => PowerPointThemeEngine.Legacy;
        public string? BaseTemplateKey => null;
        public CoverLayoutStyle CoverStyle => CoverLayoutStyle.ClassicBar;
        public ThemeAttribution? RequiredAttribution => null;
        public string? PreviewThumbnailPath => null;
        public ThemeColors Colors { get; } = new();
        public ThemeFonts Fonts { get; } = new();
        public byte[]? LogoOverlayPng => null;
    }
}
