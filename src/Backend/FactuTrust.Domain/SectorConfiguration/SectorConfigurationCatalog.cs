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

    /// <summary>
    /// Business domain codes offered/accepted for this segment (plan §3.1 matrix, Phase 1 dynamic
    /// configuration). <c>autre</c> is always included as the universal safety-net fallback.
    /// Consumed by <see cref="StaticSectorCatalogProvider"/> (projected into
    /// <c>SegmentSnapshot.DomainCodes</c>) and enforced by <c>RegistrationSectorService</c> — this
    /// is the single source of truth for the segment↔domain link, mirrored by the frontend fallback
    /// matrix in <c>registration-catalog.ts</c> (<c>SEGMENT_ALLOWED_DOMAINS</c>).
    /// </summary>
    public required IReadOnlyList<string> AllowedDomainCodes { get; init; }
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
/// Declares that enabling <see cref="Module"/> also requires <see cref="RequiredModule"/> to be
/// enabled (plan §4.2). Consumed by <see cref="SectorModuleSetCalculator"/> (transitive-closure
/// pull) and projected into <c>SectorRuleSnapshot.ModuleDependencies</c>/seeded as
/// <c>SectorModuleDependency</c> rows for DB-driven resolution.
/// </summary>
public sealed record ModuleDependencyEdge
{
    public required AppModule Module { get; init; }
    public required AppModule RequiredModule { get; init; }
}

/// <summary>
/// One data-template item (plan §4.3 additive sector presets). <c>ItemKind</c>/<c>PayloadJson</c>
/// match what <c>SectorDataTemplateApplier.ApplyItemAsync</c> understands (e.g. <c>"chart-account"</c>).
/// </summary>
public sealed record DataTemplateItemDefinition
{
    public required string ItemKind { get; init; }
    public required string PayloadJson { get; init; }
    public required int SortOrder { get; init; }
}

/// <summary>
/// A named, additive sector data template (plan §4.3) — e.g. "add these extra chart-of-accounts
/// sub-accounts for this domain". Never modifies/removes pre-existing tenant rows; items are only
/// inserted if the matching row does not already exist (see <c>SectorDataTemplateApplier</c>).
/// </summary>
public sealed record DataTemplateDefinition
{
    public required string Code { get; init; }
    public string? SegmentCode { get; init; }
    public string? DomainCode { get; init; }
    public required string LabelFr { get; init; }
    public string? DescriptionFr { get; init; }
    public required int Version { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<DataTemplateItemDefinition> Items { get; init; }
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
            DescriptionFr = "Sociétés commerciales et de services aux entreprises.",
            IconKey = "briefcase",
            SortOrder = 0,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Accounting, AppModule.CRM, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.TechnologieInformatique,
                BusinessDomains.AlimentationAgroalimentaire,
                BusinessDomains.SanteParamedical,
                BusinessDomains.TextileHabillement,
                BusinessDomains.TransportLogistique,
                BusinessDomains.Immobilier,
                BusinessDomains.EnergieEnvironnement,
                BusinessDomains.CommunicationMarketing,
                BusinessDomains.Artisanat,
                BusinessDomains.Autre
            }
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Commerce,
            LabelFr = "Commerce",
            DescriptionFr = "Négoce et distribution.",
            IconKey = "shopping-cart",
            SortOrder = 1,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Fiscal },
            DefaultWarehouseName = "Magasin principal",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.AlimentationAgroalimentaire,
                BusinessDomains.TextileHabillement,
                BusinessDomains.TechnologieInformatique,
                BusinessDomains.SanteParamedical,
                BusinessDomains.Artisanat,
                BusinessDomains.Autre
            }
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Services,
            LabelFr = "Prestations de services",
            DescriptionFr = "Services et conseils.",
            IconKey = "handshake",
            SortOrder = 2,
            BaseRecommendedModules = new[] { AppModule.CRM, AppModule.Projects, AppModule.RecurringContracts, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.TechnologieInformatique,
                BusinessDomains.CommunicationMarketing,
                BusinessDomains.SanteParamedical,
                BusinessDomains.TransportLogistique,
                BusinessDomains.Immobilier,
                BusinessDomains.Autre
            }
        },
        new SegmentDefinition
        {
            Code = CompanySegments.BtpConstruction,
            LabelFr = "BTP & Construction",
            DescriptionFr = "Bâtiment et travaux publics.",
            IconKey = "hard-hat",
            SortOrder = 3,
            BaseRecommendedModules = new[] { AppModule.Purchases, AppModule.Stock, AppModule.Projects, AppModule.Fiscal },
            DefaultWarehouseName = "Dépôt chantier",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.Immobilier,
                BusinessDomains.EnergieEnvironnement,
                BusinessDomains.Artisanat,
                BusinessDomains.Autre
            }
        },
        new SegmentDefinition
        {
            Code = CompanySegments.Association,
            LabelFr = "Association",
            DescriptionFr = "Organismes à but non lucratif.",
            IconKey = "heart-handshake",
            SortOrder = 4,
            BaseRecommendedModules = new[] { AppModule.Accounting, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.SanteParamedical,
                BusinessDomains.EnergieEnvironnement,
                BusinessDomains.CommunicationMarketing,
                BusinessDomains.Artisanat,
                BusinessDomains.Autre
            }
        },
        new SegmentDefinition
        {
            Code = CompanySegments.EtablissementEducatif,
            LabelFr = "Établissement éducatif",
            DescriptionFr = "Écoles, universités, centres de formation.",
            IconKey = "graduation-cap",
            SortOrder = 5,
            BaseRecommendedModules = new[] { AppModule.RecurringContracts, AppModule.Accounting, AppModule.Fiscal },
            DefaultWarehouseName = "Entrepôt Principal",
            AllowedDomainCodes = new[]
            {
                BusinessDomains.TechnologieInformatique,
                BusinessDomains.SanteParamedical,
                BusinessDomains.Artisanat,
                BusinessDomains.CommunicationMarketing,
                BusinessDomains.Autre
            }
        }
    };

    public static readonly IReadOnlyList<DomainDefinition> Domains = new[]
    {
        new DomainDefinition
        {
            Code = BusinessDomains.TechnologieInformatique,
            LabelFr = "Technologie & Informatique",
            SortOrder = 0,
            OverlayModules = new[] { AppModule.Projects, AppModule.RecurringContracts }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.AlimentationAgroalimentaire,
            LabelFr = "Alimentation & Agroalimentaire",
            SortOrder = 1,
            OverlayModules = new[] { AppModule.Stock, AppModule.Purchases }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.SanteParamedical,
            LabelFr = "Santé & Paramédical",
            SortOrder = 2,
            OverlayModules = new[] { AppModule.CRM }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.TextileHabillement,
            LabelFr = "Textile & Habillement",
            SortOrder = 3,
            OverlayModules = new[] { AppModule.Stock }
        },
        new DomainDefinition
        {
            Code = BusinessDomains.TransportLogistique,
            LabelFr = "Transport & Logistique",
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
            LabelFr = "Énergie & Environnement",
            SortOrder = 6,
            OverlayModules = Array.Empty<AppModule>()
        },
        new DomainDefinition
        {
            Code = BusinessDomains.CommunicationMarketing,
            LabelFr = "Communication & Marketing",
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
            LabelFr = "Autre domaine",
            SortOrder = 9,
            OverlayModules = Array.Empty<AppModule>()
        }
    };

    /// <summary>
    /// Module dependency edges (plan §4.2, approved matrix). Note: because
    /// <see cref="CoreModules"/> already includes Products, Sales and Treasury, these 4 edges are
    /// currently no-ops in the real registration/reconfiguration flow (the required module is
    /// always already granted) — implemented exactly as approved regardless, for when
    /// <see cref="CoreModules"/> composition changes or a module becomes optional later.
    /// </summary>
    public static readonly IReadOnlyList<ModuleDependencyEdge> ModuleDependencies = new[]
    {
        new ModuleDependencyEdge { Module = AppModule.Stock, RequiredModule = AppModule.Products },
        new ModuleDependencyEdge { Module = AppModule.Purchases, RequiredModule = AppModule.Products },
        new ModuleDependencyEdge { Module = AppModule.Forecasting, RequiredModule = AppModule.Treasury },
        new ModuleDependencyEdge { Module = AppModule.RecurringContracts, RequiredModule = AppModule.Sales }
    };

    /// <summary>
    /// Additive sector data templates (plan §4.3). Domain-scoped chart-of-accounts sub-accounts
    /// verified against the real Tunisian seeded chart (<c>TunisianPostingAccounts</c>): parents
    /// 707 "Ventes de marchandises" and 705 "Prestations de services" both exist, are top-level,
    /// class 7, nature Credit; account numbers 7071/7072/7051 do not already exist. Applied
    /// exclusively through <c>SectorDataTemplateApplier.ApplyChartAccountAsync</c>'s existing
    /// insert-if-not-exists path — never touches accounting seeding/migrations.
    /// </summary>
    public static readonly IReadOnlyList<DataTemplateDefinition> DataTemplates = new[]
    {
        new DataTemplateDefinition
        {
            Code = "chart-account-detail-alimentation-agroalimentaire",
            SegmentCode = null,
            DomainCode = BusinessDomains.AlimentationAgroalimentaire,
            LabelFr = "Sous-compte ventes — Alimentation & agroalimentaire",
            DescriptionFr = "Ajoute un sous-compte 7071 dédié aux ventes de marchandises alimentaires.",
            Version = 1,
            SortOrder = 0,
            Items = new[]
            {
                new DataTemplateItemDefinition
                {
                    ItemKind = "chart-account",
                    PayloadJson = "{\"accountNumber\":\"7071\",\"label\":\"Ventes de marchandises - Alimentation & agroalimentaire\",\"accountClass\":7,\"parentAccountNumber\":\"707\",\"natureType\":\"Credit\",\"isSystem\":false}",
                    SortOrder = 0
                }
            }
        },
        new DataTemplateDefinition
        {
            Code = "chart-account-detail-textile-habillement",
            SegmentCode = null,
            DomainCode = BusinessDomains.TextileHabillement,
            LabelFr = "Sous-compte ventes — Textile & habillement",
            DescriptionFr = "Ajoute un sous-compte 7072 dédié aux ventes de marchandises textiles.",
            Version = 1,
            SortOrder = 1,
            Items = new[]
            {
                new DataTemplateItemDefinition
                {
                    ItemKind = "chart-account",
                    PayloadJson = "{\"accountNumber\":\"7072\",\"label\":\"Ventes de marchandises - Textile & habillement\",\"accountClass\":7,\"parentAccountNumber\":\"707\",\"natureType\":\"Credit\",\"isSystem\":false}",
                    SortOrder = 0
                }
            }
        },
        new DataTemplateDefinition
        {
            Code = "chart-account-detail-technologie-informatique",
            SegmentCode = null,
            DomainCode = BusinessDomains.TechnologieInformatique,
            LabelFr = "Sous-compte prestations — Technologie & informatique",
            DescriptionFr = "Ajoute un sous-compte 7051 dédié aux prestations de services technologiques.",
            Version = 1,
            SortOrder = 2,
            Items = new[]
            {
                new DataTemplateItemDefinition
                {
                    ItemKind = "chart-account",
                    PayloadJson = "{\"accountNumber\":\"7051\",\"label\":\"Prestations de services - Technologie & informatique\",\"accountClass\":7,\"parentAccountNumber\":\"705\",\"natureType\":\"Credit\",\"isSystem\":false}",
                    SortOrder = 0
                }
            }
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
