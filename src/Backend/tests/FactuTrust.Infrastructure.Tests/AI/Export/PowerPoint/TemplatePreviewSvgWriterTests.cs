using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

public sealed class TemplatePreviewSvgWriterTests
{
    [Theory]
    [InlineData("FFFFFF", "#FFFFFF")]
    [InlineData("#1C1917", "#1C1917")]
    [InlineData("1E1B4B", "#1E1B4B")]
    public void NormalizeSvgHex_strips_duplicate_hash_prefix(string input, string expected)
    {
        Assert.Equal(expected, TemplatePreviewSvgWriter.NormalizeSvgHex(input));
    }

    [Fact]
    public void EnsurePreview_dark_theme_does_not_contain_double_hash()
    {
        var definition = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == PowerPointTemplate.Vortex);
        var contentRoot = Path.Combine(Path.GetTempPath(), "ft-ppt-preview-" + Guid.NewGuid().ToString("N"));

        try
        {
            TemplatePreviewSvgWriter.EnsurePreview(contentRoot, definition);

            var svg16 = Path.Combine(
                contentRoot,
                "wwwroot",
                "assets",
                "powerpoint",
                "themes",
                "Vortex",
                "preview-16x9.svg");
            var svg = File.ReadAllText(svg16);

            Assert.DoesNotContain("##", svg);
            Assert.Contains("fill=\"#FFFFFF\"", svg);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void EnsurePreview_light_theme_has_valid_fill_colors()
    {
        var definition = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == PowerPointTemplate.Pearl);
        var contentRoot = Path.Combine(Path.GetTempPath(), "ft-ppt-preview-" + Guid.NewGuid().ToString("N"));

        try
        {
            TemplatePreviewSvgWriter.EnsurePreview(contentRoot, definition);

            var svg16 = Path.Combine(
                contentRoot,
                "wwwroot",
                "assets",
                "powerpoint",
                "themes",
                "Pearl",
                "preview-16x9.svg");
            var svg = File.ReadAllText(svg16);

            Assert.DoesNotContain("##", svg);
            Assert.Contains("fill=\"#1C1917\"", svg);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
        }
    }
}
