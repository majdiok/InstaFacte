namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

/// <summary>
/// Premium PowerPoint templates exposed by the assistant export pipeline.
/// </summary>
/// <remarks>
/// Adding a value requires a matching <c>IPowerPointTheme</c> implementation under
/// <c>FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes</c>. Removing a value is
/// a breaking change for existing audit rows — prefer marking obsolete and keeping the enum value.
/// </remarks>
public enum PowerPointTemplate
{
    /// <summary>Polyvalent, neutral-light branding. Balanced text/visual layouts.</summary>
    Standard = 0,

    /// <summary>Data-dense, FactuTrust brand colours. Prominent charts, compact tables.</summary>
    Analyse = 1,

    /// <summary>Premium minimalist palette for executive committees. Generous whitespace.</summary>
    Executive = 2,

    /// <summary>Warm ivory light theme.</summary>
    Pearl = 3,

    /// <summary>Soft lavender creative theme.</summary>
    Daydream = 4,

    /// <summary>Calm sage-green theme.</summary>
    Serene = 5,

    /// <summary>Sky-blue airy theme.</summary>
    Breeze = 6,

    /// <summary>Kraft paper earthy theme.</summary>
    Kraft = 7,

    /// <summary>Neutral ash-gray theme.</summary>
    Ash = 8,

    /// <summary>Pure white minimalist theme.</summary>
    Howlite = 9,

    /// <summary>Deep purple dark theme.</summary>
    Vortex = 10,

    /// <summary>Indigo night data theme.</summary>
    Indigo = 11,

    /// <summary>Deep black elegant theme.</summary>
    Onyx = 12,

    /// <summary>Midnight blue dashboard theme.</summary>
    Blueberry = 13,

    /// <summary>Charcoal and amber dark theme.</summary>
    Coal = 14,

    /// <summary>Electric cyan accent dark theme.</summary>
    Electric = 15,

    /// <summary>Deep plum premium dark theme.</summary>
    Mystique = 16,

    /// <summary>Refined serif gold premium theme.</summary>
    Clementa = 17,

    /// <summary>Slate blue corporate premium theme.</summary>
    Stratos = 18,

    /// <summary>Silver executive premium theme.</summary>
    Mercury = 19,

    /// <summary>Serif storytelling premium theme.</summary>
    Dialogue = 20,

    /// <summary>Orange-pink vibrant theme.</summary>
    Nova = 21,

    /// <summary>Emerald-teal vibrant theme.</summary>
    Aurora = 22,

    /// <summary>Coral glow vibrant theme.</summary>
    CoralGlow = 23,

    /// <summary>Technology industry report — hybrid master.</summary>
    TechReport = 24,

    /// <summary>Minimal business serif — hybrid master.</summary>
    MinimalBusiness = 25,

    /// <summary>Dark cinematic neon — hybrid master.</summary>
    DarkCinematic = 26,

    /// <summary>Finance gold executive — hybrid master.</summary>
    FinanceGold = 27,

    /// <summary>Medical clean proposal — hybrid master.</summary>
    MedicalClean = 28,

    /// <summary>Magazine portfolio editorial — hybrid master.</summary>
    MagazinePortfolio = 29,

    /// <summary>Synergy cloud gradient — hybrid master.</summary>
    GradientCloud = 30,

    /// <summary>Art academy bold — hybrid master.</summary>
    ArtBold = 31,

    /// <summary>Business management corporate — hybrid master.</summary>
    BusinessMgmt = 32,

    /// <summary>Clean tech vibe — hybrid master.</summary>
    VibeCoding = 33,

    /// <summary>Team strategy energetic — hybrid master.</summary>
    TeamStrategy = 34,

    /// <summary>ESG serene nature — hybrid master.</summary>
    EsgSerene = 35,

    // ── Premium V2 — editorial & executive ─────────────────────────────────
    /// <summary>Bold serif on absolute black — editorial magazine premium.</summary>
    EditorialNoir = 36,

    /// <summary>Charcoal and cool gold — international boardroom premium.</summary>
    BoardroomCharcoal = 37,

    /// <summary>Deep emerald with gold accents — ESG executive premium.</summary>
    EmeraldExec = 38,

    /// <summary>Cream linen and sepia ink — academic research premium.</summary>
    LinenScholar = 39,

    /// <summary>Forest green and terracotta — immersive ESG storytelling.</summary>
    ForestRetreat = 40,

    // ── Premium V2 — dark cinematic ────────────────────────────────────────
    /// <summary>Neon cyan on charcoal — avant-garde tech pitch.</summary>
    NeonBlade = 41,

    /// <summary>Midnight blue with magenta accent — advanced analytics.</summary>
    MidnightData = 42,

    /// <summary>Matte obsidian with platinum accent — minimalist luxury.</summary>
    Obsidian = 43,

    /// <summary>Deep teal abyss — refined dark corporate.</summary>
    AbyssTeal = 44,

    // ── Premium V2 — vibrant marketing ─────────────────────────────────────
    /// <summary>Coral-to-violet gradient — consumer product launch.</summary>
    SunsetGradient = 45,

    /// <summary>Clinical coral and mint — contemporary health-tech.</summary>
    CoralLab = 46,

    /// <summary>Electric lime and coral — energetic startup.</summary>
    CitrusBurst = 47,

    // ── Premium V2 — light minimalist ──────────────────────────────────────
    /// <summary>Scandinavian wood and bone — product serenity.</summary>
    ScandiCalm = 48,

    /// <summary>Cobalt and off-white — institutional briefing.</summary>
    SapphireBriefing = 49,

    /// <summary>Powder rose and taupe — modern internal communication.</summary>
    RoseQuartz = 50,

    /// <summary>Slate and paper — executive minimalism.</summary>
    SlateMinimal = 51
}
