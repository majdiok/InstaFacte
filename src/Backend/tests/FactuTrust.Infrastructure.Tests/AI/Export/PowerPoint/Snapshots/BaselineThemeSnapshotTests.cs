using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint.Snapshots;

/// <summary>
/// Anti-regression baseline that pins the structural fingerprint (primary/accent colours, fonts,
/// cover style, category, isDark flag) of every theme that existed before the premium-V2 expansion.
/// Any drift on a baseline theme fails this test, forcing the change to be deliberate.
/// </summary>
/// <remarks>
/// Captured during the Phase-1 baseline before adding 16 new premium themes (IDs 36-51) and
/// 4 new <see cref="CoverLayoutStyle"/> values (8-11). Touching one of the baseline rows means
/// you are changing an existing theme — only do so with a clear product decision.
/// </remarks>
public sealed class BaselineThemeSnapshotTests
{
    private sealed record ThemeFingerprint(
        string PrimaryHex,
        string AccentHex,
        string BackgroundHex,
        string SurfaceHex,
        string TitleFont,
        string BodyFont,
        PowerPointThemeCategory Category,
        bool IsDark,
        CoverLayoutStyle CoverStyle,
        PowerPointThemeEngine Engine);

    private static readonly IReadOnlyDictionary<PowerPointTemplate, ThemeFingerprint> Baseline =
        new Dictionary<PowerPointTemplate, ThemeFingerprint>
        {
            // ── Legacy (0-2) ─────────────────────────────────────────────────────
            [PowerPointTemplate.Standard] = new("0F172A", "2563EB", "FFFFFF", "F8FAFC",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Legacy),

            [PowerPointTemplate.Analyse] = new("1E3A8A", "F59E0B", "FFFFFF", "EFF6FF",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Legacy),

            [PowerPointTemplate.Executive] = new("111827", "B45309", "FFFFFF", "F9FAFB",
                "Georgia", "Inter", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Legacy),

            // ── Light family (3-9) ───────────────────────────────────────────────
            [PowerPointTemplate.Pearl] = new("1C1917", "C2410C", "FFFBEB", "FEF3C7",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Daydream] = new("4C1D95", "8B5CF6", "FAF5FF", "F3E8FF",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Serene] = new("14532D", "059669", "F0FDF4", "ECFDF5",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Breeze] = new("0C4A6E", "0EA5E9", "F0F9FF", "E0F2FE",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Kraft] = new("44403C", "B45309", "FAF7F2", "F5F0E8",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Ash] = new("18181B", "71717A", "FAFAFA", "F4F4F5",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Howlite] = new("0A0A0A", "171717", "FFFFFF", "FAFAFA",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            // ── Dark family (10-16) ──────────────────────────────────────────────
            [PowerPointTemplate.Vortex] = new("F5F3FF", "A78BFA", "1E1B4B", "312E81",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Indigo] = new("E0E7FF", "6366F1", "0F172A", "1E293B",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Onyx] = new("FAFAFA", "FFFFFF", "0A0A0A", "171717",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Blueberry] = new("DBEAFE", "3B82F6", "0C1929", "172554",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Coal] = new("F9FAFB", "F59E0B", "111827", "1F2937",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Electric] = new("ECFEFF", "06B6D4", "0C0A09", "1C1917",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Mystique] = new("FAE8FF", "D946EF", "2E1065", "581C87",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            // ── Premium family (17-20) ──────────────────────────────────────────
            [PowerPointTemplate.Clementa] = new("1C1917", "CA8A04", "FFFBEB", "FEFCE8",
                "Georgia", "Calibri", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Stratos] = new("0F172A", "475569", "F8FAFC", "F1F5F9",
                "Georgia", "Inter", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Mercury] = new("18181B", "71717A", "FAFAFA", "F4F4F5",
                "Georgia", "Calibri", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Dialogue] = new("292524", "78716C", "FAFAF9", "F5F5F4",
                "Georgia", "Calibri", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            // ── Vibrant family (21-23) ──────────────────────────────────────────
            [PowerPointTemplate.Nova] = new("7C2D12", "F97316", "FFF7ED", "FFEDD5",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.Aurora] = new("064E3B", "10B981", "ECFDF5", "D1FAE5",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.CoralGlow] = new("881337", "FB7185", "FFF1F2", "FFE4E6",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            // ── Hybrid-only extended catalogue (24-35) ──────────────────────────
            [PowerPointTemplate.TechReport] = new("0C4A6E", "0EA5E9", "F0F9FF", "E0F2FE",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.CorporateBlue, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.MinimalBusiness] = new("1C1917", "78716C", "FAFAF9", "FFFFFF",
                "Georgia", "Calibri", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.MinimalSerif, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.DarkCinematic] = new("F5F3FF", "A78BFA", "0A0A0A", "171717",
                "Inter", "Inter", PowerPointThemeCategory.Dark, true,
                CoverLayoutStyle.DarkCinematic, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.FinanceGold] = new("111827", "B45309", "FFFBEB", "FEFCE8",
                "Georgia", "Inter", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.ClassicBar, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.MedicalClean] = new("14532D", "059669", "F0FDF4", "ECFDF5",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.MedicalClean, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.MagazinePortfolio] = new("881337", "DC2626", "FFFFFF", "FFF1F2",
                "Georgia", "Inter", PowerPointThemeCategory.Premium, false,
                CoverLayoutStyle.BoldEditorial, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.GradientCloud] = new("1E3A8A", "3B82F6", "EFF6FF", "DBEAFE",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.GradientHero, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.ArtBold] = new("831843", "EC4899", "FDF2F8", "FCE7F3",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.BoldEditorial, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.BusinessMgmt] = new("1E40AF", "2563EB", "F8FAFC", "EFF6FF",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.CorporateBlue, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.VibeCoding] = new("18181B", "6366F1", "FFFFFF", "FAFAFA",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.MinimalSerif, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.TeamStrategy] = new("713F12", "EAB308", "FEFCE8", "FEF08A",
                "Inter", "Inter", PowerPointThemeCategory.Vibrant, false,
                CoverLayoutStyle.GradientHero, PowerPointThemeEngine.Hybrid),

            [PowerPointTemplate.EsgSerene] = new("064E3B", "10B981", "ECFDF5", "D1FAE5",
                "Inter", "Inter", PowerPointThemeCategory.Light, false,
                CoverLayoutStyle.MedicalClean, PowerPointThemeEngine.Hybrid),
        };

    public static IEnumerable<object[]> BaselineTemplates =>
        Baseline.Keys.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(BaselineTemplates))]
    public void Baseline_theme_fingerprint_unchanged(PowerPointTemplate template)
    {
        var definition = PowerPointThemeLibrary.AllDefinitions.Single(d => d.Template == template);
        var expected = Baseline[template];

        Assert.Equal(expected.PrimaryHex, definition.Colors.PrimaryHex);
        Assert.Equal(expected.AccentHex, definition.Colors.AccentHex);
        Assert.Equal(expected.BackgroundHex, definition.Colors.BackgroundHex);
        Assert.Equal(expected.SurfaceHex, definition.Colors.SurfaceHex);
        Assert.Equal(expected.TitleFont, definition.Fonts.TitleFamily);
        Assert.Equal(expected.BodyFont, definition.Fonts.BodyFamily);
        Assert.Equal(expected.Category, definition.Category);
        Assert.Equal(expected.IsDark, definition.IsDark);
        Assert.Equal(expected.CoverStyle, definition.CoverStyle);
        Assert.Equal(expected.Engine, definition.Engine);
    }

    [Fact]
    public void Baseline_covers_all_36_pre_premium_v2_templates()
    {
        Assert.Equal(36, Baseline.Count);
        // Every baseline entry must still exist in the live catalogue.
        foreach (var template in Baseline.Keys)
        {
            Assert.Contains(PowerPointThemeLibrary.AllDefinitions, d => d.Template == template);
        }
    }
}
