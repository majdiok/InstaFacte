using FactuTrust.Application.Features.Studio.Ai;

namespace FactuTrust.Application.Features.Studio.Templates;

/// <summary>
/// Modèle de système Studio embarqué (« builtin ») : métadonnées d'affichage de la bibliothèque
/// « Modèles de systèmes » + spec JSON complète, chargée depuis les ressources embarquées de
/// l'assembly et validée par <see cref="StudioAiSystemSpec.TryParse"/> au chargement.
/// </summary>
public sealed record StudioBuiltinTemplate(
    string Key,
    string DisplayName,
    string Description,
    string Category,
    string ModuleTag,
    string SpecJson);

/// <summary>
/// Catalogue statique des modèles de systèmes embarqués (tâches B-P0-06/B-P0-07). Les specs JSON
/// vivent dans <c>Features/Studio/Templates/Builtin/&lt;clé&gt;.json</c> (EmbeddedResource) ; chacune
/// est validée à l'initialisation statique — un modèle manquant ou qui viole les bornes du parseur
/// (≤ 8 entités, ≤ 40 champs/entité, ≤ 200 fiches de seed) fait échouer le chargement immédiatement
/// (fail fast, contrat couvert par <c>StudioTemplateCatalogTests</c>).
/// Les clés optionnelles des phases ultérieures (workflow, automations, isReferenceData, menu) sont
/// ignorées par le parseur actuel et consommées à partir de la phase P2.
/// </summary>
public static class StudioTemplateCatalog
{
    // (clé, catégorie, étiquette de module) — la clé est aussi le nom du fichier Builtin/<clé>.json.
    private static readonly (string Key, string Category, string ModuleTag)[] Manifest =
    {
        ("gestion-conges", "RH", "RH & Paie"),
        ("gestion-contrats", "CRM", "CRM / Ventes"),
        ("suivi-equipements", "Stock", "Stock / Maintenance"),
        ("gestion-interventions", "Services", "Projets / Services"),
        ("gestion-leads", "CRM", "CRM Commercial"),
        ("catalogue-produits", "Achats", "Achats / Stock"),
        ("gestion-formations", "RH", "RH & Compétences"),
        ("suivi-reclamations", "Services", "Services / Support"),
        ("gestion-projets", "Projets", "Projets / Services"),
        ("gestion-evenements", "Événements", "Marketing / Événements"),
    };

    private static readonly IReadOnlyList<StudioBuiltinTemplate> Catalog = Load();

    /// <summary>Tous les modèles embarqués, triés par catégorie puis par nom d'affichage.</summary>
    public static IReadOnlyList<StudioBuiltinTemplate> All => Catalog;

    /// <summary>Recherche un modèle par clé (insensible à la casse) ; <c>null</c> si la clé est inconnue.</summary>
    public static StudioBuiltinTemplate? TryGet(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Catalog.FirstOrDefault(t => string.Equals(t.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<StudioBuiltinTemplate> Load()
    {
        var assembly = typeof(StudioTemplateCatalog).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        var templates = new List<StudioBuiltinTemplate>(Manifest.Length);

        foreach (var (key, category, moduleTag) in Manifest)
        {
            // Recherche par suffixe, robuste aux variations d'espace de noms (même patron que
            // TunisianCalendarService) : FactuTrust.Application.Features.Studio.Templates.Builtin.<clé>.json
            var resourceName = resourceNames.FirstOrDefault(n =>
                n.EndsWith($".Builtin.{key}.json", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Ressource embarquée introuvable pour le modèle Studio « {key} » " +
                    $"(attendu : Features/Studio/Templates/Builtin/{key}.json déclaré en EmbeddedResource).");

            string json;
            using (var stream = assembly.GetManifestResourceStream(resourceName)
                   ?? throw new InvalidOperationException($"Impossible d'ouvrir la ressource embarquée « {resourceName} »."))
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }

            if (!StudioAiSystemSpec.TryParse(json, out var parsed, out var parseError) || parsed is null)
                throw new InvalidOperationException($"Modèle Studio embarqué « {key} » invalide : {parseError}");

            templates.Add(new StudioBuiltinTemplate(
                key,
                parsed.SystemDisplayName,
                parsed.SystemDescription ?? string.Empty,
                category,
                moduleTag,
                json));
        }

        return templates
            .OrderBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
