using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Segment metadata (wizard Step 1 card + public catalog endpoint) — plan §5.
/// </summary>
public sealed record SegmentDefinition
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required string DescriptionFr { get; init; }
    public required string IconKey { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>Recommended modules for this segment alone, before any domain overlay, core excluded.</summary>
    public required IReadOnlyList<AppModule> BaseRecommendedModules { get; init; }

    /// <summary>Warehouse name used when the registration payload leaves <c>warehouseName</c> blank.</summary>
    public string? DefaultWarehouseName { get; init; }
}

/// <summary>
/// Domain metadata (wizard Step 1 radio list + public catalog endpoint) — plan §5.
/// </summary>
public sealed record DomainDefinition
{
    public required string Code { get; init; }
    public required string LabelFr { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>Modules added on top of the segment base when this domain is selected.</summary>
    public required IReadOnlyList<AppModule> OverlayModules { get; init; }
}

/// <summary>
/// Phase 1 declarative sector catalog — pure static data, source of truth for module
/// recommendations and default warehouse naming (plan §3 C8, §5, §6.1 B1).
/// <para>
/// Phase 2 replaces this with a DB-driven resolver behind the same <c>IRegistrationSectorService</c>
/// seam (plan §7 P2.3); nothing here is renamed/removed when that happens — this class becomes the
/// permanent fallback.
/// </para>
/// </summary>
public static class SectorConfigurationCatalog
{
    /// <summary>Always-enabled modules, non-deselectable, never subject to plan restriction bypass.</summary>
    public static readonly IReadOnlyList<AppModule> CoreModules = new[]
    {
        AppModule.Administration,
        AppModule.Clients,
        AppModule.Products,
        AppModule.Sales,
        AppModule.Treasury,
        AppModule.Reports
    };

    private static readonly IReadOnlySet<AppModule> CoreModuleSet = new HashSet<AppModule>(CoreModules);

    public static readonly IReadOnlyList<SegmentDefinition> Segments = new[]
    {
        new SegmentDefinition
        {
            Code = CompanySegments.Entreprise,
            LabelFr = "Entreprise",
            DescriptionFr = "Société commerciale ou industrielle classique.",
            IconKey = "briefcase",
            SortOrder = 0,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Accounting, AppModule.CRM, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal"
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Commerce,
            LabelFr = "Commerce",
            DescriptionFr = "Vente de produits, boutique ou négoce.",
            IconKey = "shopping-cart",
            SortOrder = 1,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Fiscal },
            DefaultWarehouseName = "Magasin principal"
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Services,
            LabelFr = "Prestations de services",
            DescriptionFr = "Conseil, prestations intellectuelles ou techniques.",
            IconKey = "handshake",
            SortOrder = 2,
            BaseRecommendedModules = new[] { AppModule.CRM, AppModule.Projects, AppModule.RecurringContracts, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal"
        },
        new SegmentDefinition
        {
            Code = CompanySegments.BtpConstruction,
            LabelFr = "BTP / Construction",
            DescriptionFr = "Bâtiment, travaux publics et chantiers.",
            IconKey = "hard-hat",
            SortOrder = 3,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Projects, AppModule.Fiscal },
            DefaultWarehouseName = "Dépôt chantier"
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Association,
            LabelFr = "Association",
            DescriptionFr = "Organisation à but non lucratif.",
            IconKey = "heart-handshake",
            SortOrder = 4,
            BaseRecommendedModules = new[] { AppModule.Accounting, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal"
        },
        new SegmentDefinition
        {
            Code = CompanySegments.EtablissementEducatif,
            LabelFr = "Établissement éducatif",
            DescriptionFr = "École, institut de formation ou centre éducatif.",
            IconKey = "graduation-cap",
            SortOrder = 5,
            BaseRecommendedModules = new[] { AppModule.RecurringContracts, AppModule.Accounting, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal"
        }
    };

    public static readonly IReadOnlyList<DomainDefinition> Domains = new[]
    {
        new DomainDefinition
        {
            Code = BusinessDomains.TechnologieInformatique,
            LabelFr = "Technologie / Informatique",
            SortOrder = 0,
            OverlayModules = new[] { AppModule.Projects, AppModule.RecurringContracts }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.AlimentationAgroalimentaire,
            LabelFr = "Alimentation / Agroalimentaire",
            SortOrder = 1,
            OverlayModules = new[] { AppModule.Stock, AppModule.Purchases }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.SanteParamedical,
            LabelFr = "Santé / Paramédical",
            SortOrder = 2,
            OverlayModules = new[] { AppModule.CRM }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.TextileHabillement,
            LabelFr = "Textile / Habillement",
            SortOrder = 3,
            OverlayModules = new[] { AppModule.Stock }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.TransportLogistique,
            LabelFr = "Transport / Logistique",
            SortOrder = 4,
            OverlayModules = new[] { AppModule.Stock }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.Immobilier,
            LabelFr = "Immobilier",
            SortOrder = 5,
            OverlayModules = Array.Empty<AppModule>()
        },
        new DomainDefinition
        {
            Code = BusinessDomains.EnergieEnvironnement,
            LabelFr = "Énergie / Environnement",
            SortOrder = 6,
            OverlayModules = Array.Empty<AppModule>()
        },
        new DomainDefinition
        {
            Code = BusinessDomains.CommunicationMarketing,
            LabelFr = "Communication / Marketing",
            SortOrder = 7,
            OverlayModules = new[] { AppModule.CRM }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.Artisanat,
            LabelFr = "Artisanat",
            SortOrder = 8,
            OverlayModules = Array.Empty<AppModule>()
        },
        new DomainDefinition
        {
            Code = BusinessDomains.Autre,
            LabelFr = "Autre",
            SortOrder = 9,
            OverlayModules = Array.Empty<AppModule>()
        }
    };

    private static readonly IReadOnlyDictionary<string, SegmentDefinition> SegmentsByCode =
        Segments.ToDictionary(s => s.Code, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, DomainDefinition> DomainsByCode =
        Domains.ToDictionary(d => d.Code, StringComparer.Ordinal);

    /// <summary>
    /// Merges segment base ∪ domain overlay − core. Null/unknown segment (after normalization) ⇒
    /// null. Unknown domain (after normalization) resolves as if no domain was supplied
    /// (<c>DomainCode = null</c>) — code-level validation of unknown-but-provided codes is the
    /// caller's (<c>IRegistrationSectorService</c>) responsibility, not this pure data lookup.
    /// </summary>
    public static SectorProfile? Resolve(string? segmentCode, string? domainCode)
    {
        var normalizedSegment = CompanySegments.Normalize(segmentCode);
        if (normalizedSegment is null || !SegmentsByCode.TryGetValue(normalizedSegment, out var segment))
            return null;

        var normalizedDomain = BusinessDomains.Normalize(domainCode);
        DomainDefinition? domain = null;
        if (normalizedDomain is not null)
            DomainsByCode.TryGetValue(normalizedDomain, out domain);

        var recommended = new List<AppModule>();
        var recommendedSet = new HashSet<AppModule>();

        void AddRecommended(IEnumerable<AppModule> modules)
        {
            foreach (var module in modules)
            {
                if (CoreModuleSet.Contains(module))
                    continue;
                if (recommendedSet.Add(module))
                    recommended.Add(module);
            }
        }

        AddRecommended(segment.BaseRecommendedModules);
        if (domain is not null)
            AddRecommended(domain.OverlayModules);

        var optional = AppModuleExtensions.AllValues
            .Where(m => !CoreModuleSet.Contains(m) && !recommendedSet.Contains(m) && m != AppModule.Honoraires)
            .ToList();

        return new SectorProfile
        {
            SegmentCode = segment.Code,
            DomainCode = domain?.Code,
            CoreModules = CoreModules,
            RecommendedModules = recommended,
            OptionalModules = optional,
            DefaultWarehouseName = segment.DefaultWarehouseName
        };
    }
}
