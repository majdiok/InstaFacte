using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Central catalogue of all PowerPoint theme definitions. Keeps colour tokens in one place
/// instead of scattering 24 nearly-identical theme classes.
/// </summary>
public static class PowerPointThemeLibrary
{
    public static IReadOnlyList<PowerPointThemeDefinition> AllDefinitions { get; } = BuildAll();

    private static IReadOnlyList<PowerPointThemeDefinition> BuildAll() =>
        new[]
        {
            // ── Legacy trio (enum 0–2) — tokens must stay pixel-perfect ─────────────
            Def(PowerPointTemplate.Standard, "Standard",
                "Mise en page polyvalente et neutre, équilibrée entre texte et visuel.",
                PowerPointThemeCategory.Light, false, 0,
                primary: "0F172A", secondary: "475569", accent: "2563EB", accentSoft: "DBEAFE",
                background: "FFFFFF", surface: "F8FAFC", onSurface: "0F172A", muted: "94A3B8"),

            Def(PowerPointTemplate.Analyse, "Analyse",
                "Données denses, graphiques mis en valeur, idéale pour les revues opérationnelles.",
                PowerPointThemeCategory.Vibrant, false, 1,
                primary: "1E3A8A", secondary: "1E40AF", accent: "F59E0B", accentSoft: "FEF3C7",
                background: "FFFFFF", surface: "EFF6FF", onSurface: "0F172A", muted: "64748B",
                success: "16A34A", warning: "F97316",
                chart: new[] { "2563EB", "F59E0B", "16A34A", "DC2626", "7C3AED", "0891B2", "BE185D", "65A30D" }),

            Def(PowerPointTemplate.Executive, "Executive",
                "Palette charbon et doré, généreuse en espaces blancs. Pensée pour les comités de direction.",
                PowerPointThemeCategory.Premium, false, 2,
                primary: "111827", secondary: "374151", accent: "B45309", accentSoft: "FEF3C7",
                background: "FFFFFF", surface: "F9FAFB", onSurface: "111827", muted: "6B7280",
                success: "047857", warning: "B45309", danger: "991B1B",
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "111827", "B45309", "047857", "1E3A8A", "6B21A8", "0E7490", "9F1239", "3F6212" }),

            // ── Light family (3–9) ───────────────────────────────────────────────────
            Def(PowerPointTemplate.Pearl, "Pearl",
                "Ivoire chaleureux et typographie nette — idéal pour les synthèses client.",
                PowerPointThemeCategory.Light, false, 3,
                primary: "1C1917", secondary: "57534E", accent: "C2410C", accentSoft: "FFEDD5",
                background: "FFFBEB", surface: "FEF3C7", onSurface: "292524", muted: "A8A29E"),

            Def(PowerPointTemplate.Daydream, "Daydream",
                "Lavande douce et accents violets pour des présentations créatives.",
                PowerPointThemeCategory.Light, false, 4,
                primary: "4C1D95", secondary: "6D28D9", accent: "8B5CF6", accentSoft: "EDE9FE",
                background: "FAF5FF", surface: "F3E8FF", onSurface: "3B0764", muted: "A78BFA"),

            Def(PowerPointTemplate.Serene, "Serene",
                "Verts apaisants inspirés de la nature — parfait pour le bien-être et l'ESG.",
                PowerPointThemeCategory.Light, false, 5,
                primary: "14532D", secondary: "166534", accent: "059669", accentSoft: "D1FAE5",
                background: "F0FDF4", surface: "ECFDF5", onSurface: "052E16", muted: "6EE7B7"),

            Def(PowerPointTemplate.Breeze, "Breeze",
                "Bleu ciel aéré pour des decks légers et aérés.",
                PowerPointThemeCategory.Light, false, 6,
                primary: "0C4A6E", secondary: "0369A1", accent: "0EA5E9", accentSoft: "E0F2FE",
                background: "F0F9FF", surface: "E0F2FE", onSurface: "082F49", muted: "7DD3FC"),

            Def(PowerPointTemplate.Kraft, "Kraft",
                "Papier kraft et tons terreux — chaleureux et artisanal.",
                PowerPointThemeCategory.Light, false, 7,
                primary: "44403C", secondary: "78716C", accent: "B45309", accentSoft: "FEF3C7",
                background: "FAF7F2", surface: "F5F0E8", onSurface: "292524", muted: "A8A29E"),

            Def(PowerPointTemplate.Ash, "Ash",
                "Gris neutre minimaliste pour mettre le contenu au premier plan.",
                PowerPointThemeCategory.Light, false, 8,
                primary: "18181B", secondary: "52525B", accent: "71717A", accentSoft: "F4F4F5",
                background: "FAFAFA", surface: "F4F4F5", onSurface: "18181B", muted: "A1A1AA"),

            Def(PowerPointTemplate.Howlite, "Howlite",
                "Blanc pur et contrastes nets — la sobriété à l'état pur.",
                PowerPointThemeCategory.Light, false, 9,
                primary: "0A0A0A", secondary: "404040", accent: "171717", accentSoft: "F5F5F5",
                background: "FFFFFF", surface: "FAFAFA", onSurface: "0A0A0A", muted: "A3A3A3"),

            // ── Dark family (10–16) ──────────────────────────────────────────────────
            Dark(PowerPointTemplate.Vortex, "Vortex",
                "Violet profond et accents néon — impact visuel maximal.",
                PowerPointThemeCategory.Dark, 10,
                primary: "F5F3FF", secondary: "C4B5FD", accent: "A78BFA", accentSoft: "4C1D95",
                background: "1E1B4B", surface: "312E81", onSurface: "EDE9FE", muted: "8B5CF6",
                gradient: "linear-gradient(135deg, #1E1B4B 0%, #4C1D95 100%)"),

            Dark(PowerPointTemplate.Indigo, "Indigo",
                "Indigo nocturne pour des analyses data en mode sombre.",
                PowerPointThemeCategory.Dark, 11,
                primary: "E0E7FF", secondary: "A5B4FC", accent: "6366F1", accentSoft: "312E81",
                background: "0F172A", surface: "1E293B", onSurface: "E2E8F0", muted: "64748B"),

            Dark(PowerPointTemplate.Onyx, "Onyx",
                "Noir profond et accents blancs — élégance absolue.",
                PowerPointThemeCategory.Dark, 12,
                primary: "FAFAFA", secondary: "D4D4D4", accent: "FFFFFF", accentSoft: "262626",
                background: "0A0A0A", surface: "171717", onSurface: "F5F5F5", muted: "737373"),

            Dark(PowerPointTemplate.Blueberry, "Blueberry",
                "Bleu nuit et baies vives pour les dashboards nocturnes.",
                PowerPointThemeCategory.Dark, 13,
                primary: "DBEAFE", secondary: "93C5FD", accent: "3B82F6", accentSoft: "1E3A8A",
                background: "0C1929", surface: "172554", onSurface: "EFF6FF", muted: "60A5FA"),

            Dark(PowerPointTemplate.Coal, "Coal",
                "Charbon mat et accents ambre — chaleur dans l'obscurité.",
                PowerPointThemeCategory.Dark, 14,
                primary: "F9FAFB", secondary: "D1D5DB", accent: "F59E0B", accentSoft: "374151",
                background: "111827", surface: "1F2937", onSurface: "F3F4F6", muted: "9CA3AF"),

            Dark(PowerPointTemplate.Electric, "Electric",
                "Fond sombre et accents électriques — énergie et modernité.",
                PowerPointThemeCategory.Dark, 15,
                primary: "ECFEFF", secondary: "67E8F9", accent: "06B6D4", accentSoft: "164E63",
                background: "0C0A09", surface: "1C1917", onSurface: "F0FDFA", muted: "2DD4BF",
                gradient: "linear-gradient(135deg, #0C0A09 0%, #164E63 50%, #06B6D4 100%)"),

            Dark(PowerPointTemplate.Mystique, "Mystique",
                "Prune profond et or rose — premium sombre.",
                PowerPointThemeCategory.Dark, 16,
                primary: "FAE8FF", secondary: "E879F9", accent: "D946EF", accentSoft: "701A75",
                background: "2E1065", surface: "581C87", onSurface: "FDF4FF", muted: "C026D3"),

            // ── Premium family (17–20) ─────────────────────────────────────────────
            Def(PowerPointTemplate.Clementa, "Clementa",
                "Serif raffiné et doré discret — boardroom premium.",
                PowerPointThemeCategory.Premium, false, 17,
                primary: "1C1917", secondary: "44403C", accent: "CA8A04", accentSoft: "FEF9C3",
                background: "FFFBEB", surface: "FEFCE8", onSurface: "292524", muted: "A8A29E",
                titleFont: "Georgia", bodyFont: "Calibri"),

            Def(PowerPointTemplate.Stratos, "Stratos",
                "Bleu ardoise et typographie aérienne — corporate haut de gamme.",
                PowerPointThemeCategory.Premium, false, 18,
                primary: "0F172A", secondary: "334155", accent: "475569", accentSoft: "E2E8F0",
                background: "F8FAFC", surface: "F1F5F9", onSurface: "0F172A", muted: "94A3B8",
                titleFont: "Georgia", bodyFont: "Inter"),

            Def(PowerPointTemplate.Mercury, "Mercury",
                "Argent et gris froid — précision et clarté exécutive.",
                PowerPointThemeCategory.Premium, false, 19,
                primary: "18181B", secondary: "3F3F46", accent: "71717A", accentSoft: "E4E4E7",
                background: "FAFAFA", surface: "F4F4F5", onSurface: "18181B", muted: "A1A1AA",
                titleFont: "Georgia", bodyFont: "Calibri"),

            Def(PowerPointTemplate.Dialogue, "Dialogue",
                "Titres serif et corps aéré — storytelling premium.",
                PowerPointThemeCategory.Premium, false, 20,
                primary: "292524", secondary: "57534E", accent: "78716C", accentSoft: "E7E5E4",
                background: "FAFAF9", surface: "F5F5F4", onSurface: "1C1917", muted: "A8A29E",
                titleFont: "Georgia", bodyFont: "Calibri"),

            // ── Vibrant family (21–23) ───────────────────────────────────────────────
            Def(PowerPointTemplate.Nova, "Nova",
                "Explosion orange et rose — présentations percutantes.",
                PowerPointThemeCategory.Vibrant, false, 21,
                primary: "7C2D12", secondary: "C2410C", accent: "F97316", accentSoft: "FFEDD5",
                background: "FFF7ED", surface: "FFEDD5", onSurface: "431407", muted: "FB923C",
                gradient: "linear-gradient(135deg, #FFF7ED 0%, #FDBA74 50%, #F97316 100%)"),

            Def(PowerPointTemplate.Aurora, "Aurora",
                "Vert émeraude et teal — dynamisme et croissance.",
                PowerPointThemeCategory.Vibrant, false, 22,
                primary: "064E3B", secondary: "047857", accent: "10B981", accentSoft: "D1FAE5",
                background: "ECFDF5", surface: "D1FAE5", onSurface: "022C22", muted: "34D399",
                gradient: "linear-gradient(135deg, #ECFDF5 0%, #6EE7B7 50%, #059669 100%)"),

            Def(PowerPointTemplate.CoralGlow, "Coral Glow",
                "Corail lumineux et rose chaud — marketing et lancement produit.",
                PowerPointThemeCategory.Vibrant, false, 23,
                primary: "881337", secondary: "BE123C", accent: "FB7185", accentSoft: "FFE4E6",
                background: "FFF1F2", surface: "FFE4E6", onSurface: "4C0519", muted: "FDA4AF",
                gradient: "linear-gradient(135deg, #FFF1F2 0%, #FDA4AF 50%, #FB7185 100%)"),

            // ── Hybrid-only extended catalogue (24–35) ───────────────────────────────
            HybridOnly(PowerPointTemplate.TechReport, "Technology Report",
                "Bleu tech et typo bold — rapport industrie.", PowerPointThemeCategory.Light, false, 24,
                primary: "0C4A6E", secondary: "0369A1", accent: "0EA5E9", accentSoft: "E0F2FE",
                background: "F0F9FF", surface: "E0F2FE", onSurface: "082F49", muted: "7DD3FC",
                coverStyle: CoverLayoutStyle.CorporateBlue),

            HybridOnly(PowerPointTemplate.MinimalBusiness, "Minimal Business",
                "Blanc serein, serif élégant — plan d'affaires.", PowerPointThemeCategory.Light, false, 25,
                primary: "1C1917", secondary: "57534E", accent: "78716C", accentSoft: "F5F5F4",
                background: "FAFAF9", surface: "FFFFFF", onSurface: "292524", muted: "A8A29E",
                titleFont: "Georgia", bodyFont: "Calibri", coverStyle: CoverLayoutStyle.MinimalSerif),

            HybridOnly(PowerPointTemplate.DarkCinematic, "Dark Cinematic",
                "Noir profond et accents néon — impact maximal.", PowerPointThemeCategory.Dark, true, 26,
                primary: "F5F3FF", secondary: "C4B5FD", accent: "A78BFA", accentSoft: "4C1D95",
                background: "0A0A0A", surface: "171717", onSurface: "FAFAFA", muted: "737373",
                coverStyle: CoverLayoutStyle.DarkCinematic),

            HybridOnly(PowerPointTemplate.FinanceGold, "Finance Gold",
                "Charbon et or — comité de direction.", PowerPointThemeCategory.Premium, false, 27,
                primary: "111827", secondary: "374151", accent: "B45309", accentSoft: "FEF3C7",
                background: "FFFBEB", surface: "FEFCE8", onSurface: "292524", muted: "A8A29E",
                titleFont: "Georgia", coverStyle: CoverLayoutStyle.ClassicBar),

            HybridOnly(PowerPointTemplate.MedicalClean, "Medical Clean",
                "Vert clinique apaisant — proposition santé.", PowerPointThemeCategory.Light, false, 28,
                primary: "14532D", secondary: "166534", accent: "059669", accentSoft: "D1FAE5",
                background: "F0FDF4", surface: "ECFDF5", onSurface: "052E16", muted: "6EE7B7",
                coverStyle: CoverLayoutStyle.MedicalClean),

            HybridOnly(PowerPointTemplate.MagazinePortfolio, "Magazine Portfolio",
                "Rouge éditorial et portrait — portfolio créatif.", PowerPointThemeCategory.Premium, false, 29,
                primary: "881337", secondary: "BE123C", accent: "DC2626", accentSoft: "FFE4E6",
                background: "FFFFFF", surface: "FFF1F2", onSurface: "4C0519", muted: "FDA4AF",
                titleFont: "Georgia", coverStyle: CoverLayoutStyle.BoldEditorial),

            HybridOnly(PowerPointTemplate.GradientCloud, "Synergy Cloud",
                "Dégradé bleu radial — cloud & tech.", PowerPointThemeCategory.Light, false, 30,
                primary: "1E3A8A", secondary: "2563EB", accent: "3B82F6", accentSoft: "DBEAFE",
                background: "EFF6FF", surface: "DBEAFE", onSurface: "1E3A8A", muted: "93C5FD",
                coverStyle: CoverLayoutStyle.GradientHero,
                gradient: "linear-gradient(135deg, #EFF6FF 0%, #3B82F6 100%)"),

            HybridOnly(PowerPointTemplate.ArtBold, "Art Academy",
                "Rose vif et typo lourde — académie artistique.", PowerPointThemeCategory.Vibrant, false, 31,
                primary: "831843", secondary: "BE185D", accent: "EC4899", accentSoft: "FCE7F3",
                background: "FDF2F8", surface: "FCE7F3", onSurface: "500724", muted: "F9A8D4",
                coverStyle: CoverLayoutStyle.BoldEditorial),

            HybridOnly(PowerPointTemplate.BusinessMgmt, "Business Management",
                "Bleu corporate structuré — management.", PowerPointThemeCategory.Light, false, 32,
                primary: "1E40AF", secondary: "1D4ED8", accent: "2563EB", accentSoft: "DBEAFE",
                background: "F8FAFC", surface: "EFF6FF", onSurface: "0F172A", muted: "64748B",
                coverStyle: CoverLayoutStyle.CorporateBlue),

            HybridOnly(PowerPointTemplate.VibeCoding, "Vibe Coding",
                "Minimal tech clair — production software.", PowerPointThemeCategory.Light, false, 33,
                primary: "18181B", secondary: "3F3F46", accent: "6366F1", accentSoft: "E0E7FF",
                background: "FFFFFF", surface: "FAFAFA", onSurface: "18181B", muted: "A1A1AA",
                coverStyle: CoverLayoutStyle.MinimalSerif),

            HybridOnly(PowerPointTemplate.TeamStrategy, "Team Strategy",
                "Jaune énergique — stratégie d'équipe.", PowerPointThemeCategory.Vibrant, false, 34,
                primary: "713F12", secondary: "A16207", accent: "EAB308", accentSoft: "FEF9C3",
                background: "FEFCE8", surface: "FEF08A", onSurface: "422006", muted: "FACC15",
                coverStyle: CoverLayoutStyle.GradientHero),

            HybridOnly(PowerPointTemplate.EsgSerene, "ESG Serene",
                "Vert nature et courbes — rapport ESG.", PowerPointThemeCategory.Light, false, 35,
                primary: "064E3B", secondary: "047857", accent: "10B981", accentSoft: "D1FAE5",
                background: "ECFDF5", surface: "D1FAE5", onSurface: "022C22", muted: "34D399",
                coverStyle: CoverLayoutStyle.MedicalClean),

            // ── Premium V2 — editorial & executive (36–40) ───────────────────────
            HybridOnly(PowerPointTemplate.EditorialNoir, "Editorial Noir",
                "Sérif déclaratif et noir absolu — édition magazine premium.",
                PowerPointThemeCategory.Premium, false, 36,
                primary: "0A0A0A", secondary: "27272A", accent: "F50057", accentSoft: "FCE4EC",
                background: "FAFAFA", surface: "FFFFFF", onSurface: "0A0A0A", muted: "A1A1AA",
                coverStyle: CoverLayoutStyle.EditorialMagazine,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "F50057", "0A0A0A", "D4AF37", "52525B", "EC407A", "8E24AA", "00897B", "5D4037" }),

            HybridOnly(PowerPointTemplate.BoardroomCharcoal, "Boardroom Charcoal",
                "Charbon, dorure froide et grilles — boardroom international.",
                PowerPointThemeCategory.Premium, false, 37,
                primary: "1B1F23", secondary: "374151", accent: "9C7A4D", accentSoft: "F4ECDB",
                background: "FFFFFF", surface: "F7F5F2", onSurface: "1B1F23", muted: "7B7F86",
                coverStyle: CoverLayoutStyle.ClassicBar,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "1B1F23", "9C7A4D", "4A5568", "2D3748", "A78057", "6B7280", "8B4513", "4F46E5" }),

            HybridOnly(PowerPointTemplate.EmeraldExec, "Emerald Exec",
                "Émeraude profond et accents or — ESG executive.",
                PowerPointThemeCategory.Premium, false, 38,
                primary: "064E3B", secondary: "065F46", accent: "D4AF37", accentSoft: "FEF3C7",
                background: "FFFFFF", surface: "F0FDF4", onSurface: "022C22", muted: "4B5563",
                coverStyle: CoverLayoutStyle.ClassicBar,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "D4AF37", "065F46", "0F766E", "B45309", "1F2937", "78350F", "047857", "1E40AF" }),

            HybridOnly(PowerPointTemplate.LinenScholar, "Linen Scholar",
                "Lin papier crème et encre sépia — recherche académique.",
                PowerPointThemeCategory.Premium, false, 39,
                primary: "1C1917", secondary: "44403C", accent: "78350F", accentSoft: "FEF3C7",
                background: "FAF6F0", surface: "F5EFE5", onSurface: "1C1917", muted: "78716C",
                coverStyle: CoverLayoutStyle.MinimalSerif,
                titleFont: "Georgia", bodyFont: "Calibri",
                chart: new[] { "78350F", "1C1917", "5C2C0C", "7C2D12", "92400E", "B45309", "0C4A6E", "065F46" }),

            HybridOnly(PowerPointTemplate.ForestRetreat, "Forest Retreat",
                "Vert forêt et terre cuite — récit environnemental immersif.",
                PowerPointThemeCategory.Premium, false, 40,
                primary: "1B4332", secondary: "2D6A4F", accent: "D4A373", accentSoft: "FEFAE0",
                background: "FAEDCD", surface: "E9EDC9", onSurface: "081C15", muted: "588157",
                coverStyle: CoverLayoutStyle.GradientWaveHero,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "D4A373", "2D6A4F", "BC6C25", "606C38", "283618", "9C6644", "7F4F24", "CCD5AE" }),

            // ── Premium V2 — dark cinematic (41–44) ─────────────────────────────
            HybridOnly(PowerPointTemplate.NeonBlade, "Neon Blade",
                "Cyan néon sur noir charbon — pitch tech avant-gardiste.",
                PowerPointThemeCategory.Dark, true, 41,
                primary: "E0F7FA", secondary: "80DEEA", accent: "00E5FF", accentSoft: "006064",
                background: "0A0E1A", surface: "111827", onSurface: "F0F9FF", muted: "455A64",
                coverStyle: CoverLayoutStyle.NeonAccent,
                success: "00E676", warning: "FFD600", danger: "FF1744",
                chart: new[] { "00E5FF", "00E676", "FF6E40", "B388FF", "FFD600", "40C4FF", "EA80FC", "1DE9B6" },
                gradient: "linear-gradient(135deg, #0A0E1A 0%, #006064 50%, #00E5FF 100%)"),

            HybridOnly(PowerPointTemplate.MidnightData, "Midnight Data",
                "Bleu nuit étoilé et accent magenta — analytics avancé.",
                PowerPointThemeCategory.Dark, true, 42,
                primary: "E0E7FF", secondary: "A5B4FC", accent: "F472B6", accentSoft: "4338CA",
                background: "030712", surface: "0F172A", onSurface: "EEF2FF", muted: "64748B",
                coverStyle: CoverLayoutStyle.AsymmetricSplit,
                success: "34D399", warning: "FBBF24", danger: "F87171",
                chart: new[] { "F472B6", "60A5FA", "34D399", "FBBF24", "A78BFA", "22D3EE", "FB7185", "FACC15" }),

            HybridOnly(PowerPointTemplate.Obsidian, "Obsidian",
                "Obsidienne mat et accent platine — luxe minimaliste.",
                PowerPointThemeCategory.Dark, true, 43,
                primary: "FAFAFA", secondary: "D4D4D8", accent: "E5E4E2", accentSoft: "27272A",
                background: "09090B", surface: "18181B", onSurface: "FAFAFA", muted: "52525B",
                coverStyle: CoverLayoutStyle.EditorialMagazine,
                success: "34D399", warning: "FBBF24", danger: "F87171",
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "E5E4E2", "D4AF37", "A8A29E", "78716C", "57534E", "44403C", "292524", "0C0A09" }),

            HybridOnly(PowerPointTemplate.AbyssTeal, "Abyss Teal",
                "Abysse bleu sarcelle — corporate sombre raffiné.",
                PowerPointThemeCategory.Dark, true, 44,
                primary: "F0FDFA", secondary: "99F6E4", accent: "5EEAD4", accentSoft: "134E4A",
                background: "042F2E", surface: "134E4A", onSurface: "ECFEFF", muted: "64748B",
                coverStyle: CoverLayoutStyle.DarkCinematic,
                success: "34D399", warning: "FBBF24", danger: "F87171",
                chart: new[] { "5EEAD4", "06B6D4", "2DD4BF", "FBBF24", "A5F3FC", "67E8F9", "FACC15", "FB7185" }),

            // ── Premium V2 — vibrant marketing (45–47) ──────────────────────────
            HybridOnly(PowerPointTemplate.SunsetGradient, "Sunset Gradient",
                "Dégradé corail-violet — lancement produit grand public.",
                PowerPointThemeCategory.Vibrant, false, 45,
                primary: "4A148C", secondary: "7B1FA2", accent: "FF6E40", accentSoft: "FFE0B2",
                background: "FFFBFE", surface: "FCE4EC", onSurface: "311B92", muted: "BA68C8",
                coverStyle: CoverLayoutStyle.GradientWaveHero,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "FF6E40", "E91E63", "7B1FA2", "512DA8", "FFD600", "00BCD4", "4CAF50", "FF5722" },
                gradient: "linear-gradient(135deg, #FFE0B2 0%, #FF6E40 50%, #7B1FA2 100%)"),

            HybridOnly(PowerPointTemplate.CoralLab, "Coral Lab",
                "Corail clinique et menthe — health-tech contemporain.",
                PowerPointThemeCategory.Vibrant, false, 46,
                primary: "881337", secondary: "BE123C", accent: "2DD4BF", accentSoft: "CCFBF1",
                background: "FFFFFF", surface: "FAFAFA", onSurface: "4C0519", muted: "9F1239",
                coverStyle: CoverLayoutStyle.MedicalClean,
                chart: new[] { "2DD4BF", "FB7185", "FACC15", "8B5CF6", "06B6D4", "F472B6", "4ADE80", "F97316" }),

            HybridOnly(PowerPointTemplate.CitrusBurst, "Citrus Burst",
                "Lime électrique et corail — startup énergique.",
                PowerPointThemeCategory.Vibrant, false, 47,
                primary: "365314", secondary: "4D7C0F", accent: "EA580C", accentSoft: "FFEDD5",
                background: "F7FEE7", surface: "ECFCCB", onSurface: "1A2E05", muted: "84CC16",
                coverStyle: CoverLayoutStyle.GradientHero,
                chart: new[] { "EA580C", "84CC16", "F97316", "A3E635", "BEF264", "FB923C", "CA8A04", "22C55E" },
                gradient: "linear-gradient(135deg, #F7FEE7 0%, #84CC16 50%, #EA580C 100%)"),

            // ── Premium V2 — light minimalist (48–51) ───────────────────────────
            HybridOnly(PowerPointTemplate.ScandiCalm, "Scandi Calm",
                "Scandinave épuré et bois clair — sérénité produit.",
                PowerPointThemeCategory.Light, false, 48,
                primary: "292524", secondary: "57534E", accent: "A8A29E", accentSoft: "F5F5F4",
                background: "FAFAF9", surface: "F5F5F4", onSurface: "1C1917", muted: "A8A29E",
                coverStyle: CoverLayoutStyle.MinimalSerif,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "A8A29E", "D6D3D1", "78716C", "44403C", "E7E5E4", "0C4A6E", "78350F", "065F46" }),

            HybridOnly(PowerPointTemplate.SapphireBriefing, "Sapphire Briefing",
                "Cobalt et blanc cassé — briefing institutionnel.",
                PowerPointThemeCategory.Light, false, 49,
                primary: "1E3A8A", secondary: "1E40AF", accent: "2563EB", accentSoft: "DBEAFE",
                background: "F8FAFC", surface: "EFF6FF", onSurface: "0F172A", muted: "94A3B8",
                coverStyle: CoverLayoutStyle.CorporateBlue,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "2563EB", "1E40AF", "3B82F6", "60A5FA", "93C5FD", "1E3A8A", "0EA5E9", "0284C7" }),

            HybridOnly(PowerPointTemplate.RoseQuartz, "Rose Quartz",
                "Rose poudré et taupe — communication interne moderne.",
                PowerPointThemeCategory.Light, false, 50,
                primary: "831843", secondary: "9F1239", accent: "FB7185", accentSoft: "FFE4E6",
                background: "FFF1F2", surface: "FCE7F3", onSurface: "4C0519", muted: "F9A8D4",
                coverStyle: CoverLayoutStyle.MinimalSerif,
                titleFont: "Georgia", bodyFont: "Inter",
                chart: new[] { "FB7185", "EC4899", "F472B6", "BE185D", "831843", "9F1239", "F87171", "FACC15" }),

            HybridOnly(PowerPointTemplate.SlateMinimal, "Slate Minimal",
                "Ardoise et papier — minimalisme exécutif.",
                PowerPointThemeCategory.Light, false, 51,
                primary: "0F172A", secondary: "334155", accent: "64748B", accentSoft: "E2E8F0",
                background: "F8FAFC", surface: "F1F5F9", onSurface: "0F172A", muted: "94A3B8",
                coverStyle: CoverLayoutStyle.MinimalSerif,
                chart: new[] { "64748B", "0F172A", "475569", "94A3B8", "CBD5E1", "E2E8F0", "1E40AF", "047857" })
        }.Select(ApplyHybridMetadata).ToList();

    private static PowerPointThemeDefinition Def(
        PowerPointTemplate template,
        string name,
        string description,
        PowerPointThemeCategory category,
        bool isDark,
        int sortOrder,
        string primary,
        string secondary,
        string accent,
        string accentSoft,
        string background,
        string surface,
        string onSurface,
        string muted,
        string success = "059669",
        string warning = "D97706",
        string danger = "DC2626",
        string titleFont = "Inter",
        string bodyFont = "Inter",
        string[]? chart = null,
        string? gradient = null,
        CoverLayoutStyle coverStyle = CoverLayoutStyle.ClassicBar) =>
        ApplyHybridMetadata(new PowerPointThemeDefinition(
            template,
            name,
            description,
            category,
            isDark,
            sortOrder,
            new ThemeColors
            {
                PrimaryHex = primary,
                SecondaryHex = secondary,
                AccentHex = accent,
                AccentSoftHex = accentSoft,
                BackgroundHex = background,
                SurfaceHex = surface,
                OnSurfaceHex = onSurface,
                MutedHex = muted,
                SuccessHex = success,
                WarningHex = warning,
                DangerHex = danger,
                ChartSeriesHex = chart ?? DefaultChart(primary, accent, success, danger)
            },
            new ThemeFonts { TitleFamily = titleFont, BodyFamily = bodyFont, MonoFamily = "Consolas" },
            gradient,
            CoverStyle: coverStyle));

    private static PowerPointThemeDefinition HybridOnly(
        PowerPointTemplate template,
        string name,
        string description,
        PowerPointThemeCategory category,
        bool isDark,
        int sortOrder,
        string primary,
        string secondary,
        string accent,
        string accentSoft,
        string background,
        string surface,
        string onSurface,
        string muted,
        CoverLayoutStyle coverStyle,
        string success = "059669",
        string warning = "D97706",
        string danger = "DC2626",
        string titleFont = "Inter",
        string bodyFont = "Inter",
        string[]? chart = null,
        string? gradient = null) =>
        Def(template, name, description, category, isDark, sortOrder,
            primary, secondary, accent, accentSoft, background, surface, onSurface, muted,
            success, warning, danger, titleFont, bodyFont, chart, gradient, coverStyle);

    private static PowerPointThemeDefinition ApplyHybridMetadata(PowerPointThemeDefinition definition)
    {
        if ((int)definition.Template < (int)PowerPointTemplate.Pearl)
            return definition with { Engine = PowerPointThemeEngine.Legacy };

        var key = definition.Template.ToString();
        return definition with
        {
            Engine = PowerPointThemeEngine.Hybrid,
            BaseTemplateKey = key,
            PreviewThumbnailPath = $"/assets/powerpoint/themes/{key}/preview-16x9.svg"
        };
    }

    private static PowerPointThemeDefinition Dark(
        PowerPointTemplate template,
        string name,
        string description,
        PowerPointThemeCategory category,
        int sortOrder,
        string primary,
        string secondary,
        string accent,
        string accentSoft,
        string background,
        string surface,
        string onSurface,
        string muted,
        string success = "34D399",
        string warning = "FBBF24",
        string danger = "F87171",
        string titleFont = "Inter",
        string bodyFont = "Inter",
        string[]? chart = null,
        string? gradient = null,
        CoverLayoutStyle coverStyle = CoverLayoutStyle.ClassicBar) =>
        Def(template, name, description, category, isDark: true, sortOrder,
            primary, secondary, accent, accentSoft, background, surface, onSurface, muted,
            success, warning, danger, titleFont, bodyFont, chart, gradient, coverStyle);

    private static string[] DefaultChart(string primary, string accent, string success, string danger) =>
        new[] { accent, success, "D97706", danger, "7C3AED", "0891B2", primary, "65A30D" };
}
