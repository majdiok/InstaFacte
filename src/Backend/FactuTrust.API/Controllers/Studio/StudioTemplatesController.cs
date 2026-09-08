using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Bibliothèque « Modèles de systèmes » (tâche B-P0-08). P0 : lecture du seul catalogue
/// EMBARQUÉ (<see cref="StudioTemplateCatalog"/>, source « builtin »). Les modèles tenant
/// (table <c>CustomSystemTemplates</c>, partage Privé/Équipe) arrivent en P3 — la fusion
/// builtin + tenant se fera ici, sans changement de contrat. Lecture gardée par
/// <c>EnableStudioAiWorkbench</c> OU <c>EnableStudioTemplates</c> (le catalogue sert aussi à
/// l'endpoint <c>from-template</c> des plans).
/// </summary>
[ApiController]
[Route("api/studio/templates")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioTemplatesController : ControllerBase
{
    // Nombre de tables de chaque modèle, calculé UNE FOIS via le résumé canonique
    // (StudioAiPlanSummary.ForSystem) puis mis en cache — les specs embarquées sont immuables.
    private static readonly Lazy<IReadOnlyDictionary<string, int>> EntityCounts =
        new(ComputeEntityCounts);

    private readonly OllamaSettings _ollamaSettings;

    public StudioTemplatesController(IOptions<OllamaSettings> ollamaSettings) =>
        _ollamaSettings = ollamaSettings.Value;

    /// <summary>Liste fusionnée des modèles (P0 : builtin uniquement), filtrable par catégorie.</summary>
    [HttpGet]
    public IActionResult List([FromQuery] string? category)
    {
        if (TemplatesUnavailableOrNull() is { } unavailable)
            return unavailable;

        var items = StudioTemplateCatalog.All
            .Where(t => string.IsNullOrWhiteSpace(category)
                || string.Equals(t.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(ToListItem);

        // TODO(P3) : fusionner ici les modèles TENANT (dépôt CustomSystemTemplates, source
        // « tenant », id/visibility/updatedAt réels) quand la phase P3 sera livrée.
        return Ok(ApiResponse<IReadOnlyList<StudioTemplateListItemDto>>.Ok(items.ToList()));
    }

    /// <summary>Détail d'un modèle embarqué, spec canonique incluse (telle quelle, sans re-formatage).</summary>
    [HttpGet("{key}")]
    public IActionResult GetByKey(string key)
    {
        if (TemplatesUnavailableOrNull() is { } unavailable)
            return unavailable;

        var template = StudioTemplateCatalog.TryGet(key);
        if (template is null)
            return NotFound(ApiResponse<object>.Fail($"Modèle de système inconnu : « {key} »."));

        var item = ToListItem(template);
        return Ok(ApiResponse<StudioTemplateDetailDto>.Ok(new StudioTemplateDetailDto(
            item.Key, item.Id, item.DisplayName, item.Description, item.Category, item.ModuleTag,
            item.Source, item.Visibility, item.EntityCount, item.UpdatedAt, template.SpecJson)));
    }

    /// <summary>Garde de lecture : 404 si NI le workbench NI la bibliothèque tenant ne sont actifs.</summary>
    private IActionResult? TemplatesUnavailableOrNull() =>
        _ollamaSettings.EnableStudioAiWorkbench || _ollamaSettings.EnableStudioTemplates
            ? null
            : NotFound(ApiResponse<object>.Fail("La bibliothèque de modèles Studio n'est pas activée."));

    private static StudioTemplateListItemDto ToListItem(StudioBuiltinTemplate template) => new(
        template.Key,
        Id: null,              // les modèles embarqués n'ont pas d'identifiant tenant
        template.DisplayName,
        template.Description,
        template.Category,
        template.ModuleTag,
        Source: "builtin",
        Visibility: null,      // le partage Privé/Équipe n'existe que pour les modèles tenant (P3)
        EntityCounts.Value.TryGetValue(template.Key, out var count) ? count : 0,
        UpdatedAt: null);

    private static IReadOnlyDictionary<string, int> ComputeEntityCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in StudioTemplateCatalog.All)
        {
            var count = 0;
            if (StudioAiSystemSpec.TryParse(template.SpecJson, out var spec, out _) && spec is not null)
            {
                try
                {
                    count = (JsonNode.Parse(StudioAiPlanSummary.ForSystem(spec)) as JsonObject)?["entities"]
                        is JsonArray entities ? entities.Count : 0;
                }
                catch (JsonException)
                {
                    // Résumé illisible : la ligne reste affichée avec 0 table (les specs embarquées
                    // sont déjà validées au chargement du catalogue — garde purement défensive).
                }
            }
            counts[template.Key] = count;
        }
        return counts;
    }
}

/// <summary>Ligne de la bibliothèque de modèles (contrat stable, complété côté tenant en P3).</summary>
public sealed record StudioTemplateListItemDto(
    string Key,
    Guid? Id,
    string DisplayName,
    string Description,
    string Category,
    string ModuleTag,
    string Source,
    string? Visibility,
    int EntityCount,
    DateTime? UpdatedAt);

/// <summary>Détail d'un modèle : la ligne + la spec canonique prête pour « Utiliser ce modèle ».</summary>
public sealed record StudioTemplateDetailDto(
    string Key,
    Guid? Id,
    string DisplayName,
    string Description,
    string Category,
    string ModuleTag,
    string Source,
    string? Visibility,
    int EntityCount,
    DateTime? UpdatedAt,
    string SpecJson);
