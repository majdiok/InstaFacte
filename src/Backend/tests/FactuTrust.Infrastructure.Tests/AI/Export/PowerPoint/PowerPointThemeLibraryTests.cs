using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

public sealed class PowerPointThemeLibraryTests
{
    [Fact]
    public void AllDefinitions_contains_52_themes_with_unique_ids()
    {
        var defs = PowerPointThemeLibrary.AllDefinitions;

        Assert.Equal(52, defs.Count);
        Assert.Equal(52, defs.Select(d => d.Template).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 52).Select(i => (PowerPointTemplate)i), defs.Select(d => d.Template).OrderBy(t => (int)t));
    }

    [Fact]
    public void Hybrid_themes_from_id_3_have_hybrid_engine_metadata()
    {
        var hybrid = PowerPointThemeLibrary.AllDefinitions.Where(d => (int)d.Template >= 3).ToList();
        Assert.Equal(49, hybrid.Count);
        Assert.All(hybrid, d =>
        {
            Assert.Equal(PowerPointThemeEngine.Hybrid, d.Engine);
            Assert.False(string.IsNullOrWhiteSpace(d.BaseTemplateKey));
            Assert.Contains("preview-16x9.svg", d.PreviewThumbnailPath);
        });
    }

    [Fact]
    public void Legacy_themes_0_1_2_remain_programmatic_engine()
    {
        foreach (var template in new[] { PowerPointTemplate.Standard, PowerPointTemplate.Analyse, PowerPointTemplate.Executive })
        {
            var def = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == template);
            Assert.Equal(PowerPointThemeEngine.Legacy, def.Engine);
        }
    }

    [Fact]
    public void Legacy_themes_0_1_2_preserve_original_color_tokens()
    {
        var standard = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == PowerPointTemplate.Standard);
        Assert.Equal("0F172A", standard.Colors.PrimaryHex);
        Assert.Equal("2563EB", standard.Colors.AccentHex);

        var analyse = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == PowerPointTemplate.Analyse);
        Assert.Equal("1E3A8A", analyse.Colors.PrimaryHex);
        Assert.Equal("F59E0B", analyse.Colors.AccentHex);

        var executive = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == PowerPointTemplate.Executive);
        Assert.Equal("111827", executive.Colors.PrimaryHex);
        Assert.Equal("B45309", executive.Colors.AccentHex);
        Assert.Equal("Georgia", executive.Fonts.TitleFamily);
    }
}

public sealed class PowerPointTemplateCatalogTests
{
    [Fact]
    public void ListTemplates_returns_52_when_extended_enabled()
    {
        var catalog = BuildCatalog(extended: true);
        Assert.Equal(52, catalog.List().Count);
    }

    [Fact]
    public void ListTemplates_returns_3_when_extended_disabled()
    {
        var catalog = BuildCatalog(extended: false);
        var list = catalog.List();
        Assert.Equal(3, list.Count);
        Assert.Equal(PowerPointTemplate.Standard, list[0].Id);
        Assert.Equal(PowerPointTemplate.Analyse, list[1].Id);
        Assert.Equal(PowerPointTemplate.Executive, list[2].Id);
    }

    [Fact]
    public void ListTemplates_includes_preview_thumbnail_for_hybrid_themes()
    {
        var catalog = BuildCatalog(extended: true);
        var hybrid = catalog.List().Where(t => t.Engine == PowerPointThemeEngine.Hybrid).ToList();
        Assert.NotEmpty(hybrid);
        Assert.All(hybrid, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.PreviewThumbnailUrl));
            Assert.Contains("preview-16x9", t.PreviewThumbnailUrl);
        });
    }

    [Fact]
    public void Resolve_unknown_template_falls_back_to_standard()
    {
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        var resolver = new PowerPointThemeResolver(brand.Object);

        var theme = resolver.Resolve((PowerPointTemplate)999);

        Assert.Equal(PowerPointTemplate.Standard, theme.Template);
    }

    private static PowerPointTemplateCatalog BuildCatalog(bool extended)
    {
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        var options = Options.Create(new PowerPointRenderingOptions { ExtendedThemeLibraryEnabled = extended });
        var resolver = new PowerPointThemeResolver(brand.Object, options);
        return new PowerPointTemplateCatalog(resolver);
    }
}
