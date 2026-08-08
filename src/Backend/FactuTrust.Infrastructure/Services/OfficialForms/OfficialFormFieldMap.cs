using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactuTrust.Infrastructure.Services.OfficialForms;

/// <summary>
/// Alignement horizontal d'une valeur tamponnée dans sa case.
/// </summary>
public enum OfficialFormAlign
{
    /// <summary>La valeur démarre à <c>X</c> (cases texte : raison sociale, adresse…).</summary>
    Left = 0,

    /// <summary>La valeur se termine à <c>X</c> — cas normal des colonnes de montants.</summary>
    Right = 1,

    /// <summary>La valeur est centrée sur <c>X</c> (cases à cocher, chiffres isolés).</summary>
    Center = 2
}

/// <summary>
/// Une case du formulaire officiel : où tamponner la valeur portant la clé <see cref="Key"/>.
///
/// Le repère est celui de PDFsharp <c>XGraphics</c> — origine en <b>haut à gauche</b>, Y vers le
/// bas, en <b>points</b> — qui coïncide exactement avec celui de <c>pdfplumber</c> utilisé par
/// l'outil de calibration : aucune inversion d'axe n'est nécessaire (vérifié par le spike Lot 0).
/// </summary>
public sealed record OfficialFormField
{
    /// <summary>Identifiant fonctionnel, ex. <c>Vat.Base19</c>. Contrat entre le binder et la carte.</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Numéro de page <b>1-based</b> (tel qu'imprimé sur le formulaire).</summary>
    [JsonPropertyName("page")]
    public required int Page { get; init; }

    [JsonPropertyName("x")]
    public required double X { get; init; }

    /// <summary>Ordonnée de la <b>ligne de base</b> du texte (et non du haut de la cellule).</summary>
    [JsonPropertyName("y")]
    public required double Y { get; init; }

    [JsonPropertyName("align")]
    public OfficialFormAlign Align { get; init; } = OfficialFormAlign.Right;

    /// <summary>Corps de la police en points. Défaut : 8 pt, lisible dans les cellules du gabarit.</summary>
    [JsonPropertyName("fontSize")]
    public double FontSize { get; init; } = 8d;

    /// <summary>Libellé humain de la case (section/ligne du formulaire), pour la relecture fiscale.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

/// <summary>
/// Carte des coordonnées d'un millésime de formulaire officiel, chargée depuis une ressource
/// embarquée. Séparer la géométrie du code permet à un nouveau millésime DGI de se traiter par
/// simple remplacement du couple (gabarit PDF, carte JSON), <b>sans modification C#</b>, et rend
/// le mapping relisible par un fiscaliste.
/// </summary>
public sealed class OfficialFormFieldMap
{
    private static readonly ConcurrentDictionary<string, OfficialFormFieldMap> Cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [JsonPropertyName("templateVersion")]
    public required string TemplateVersion { get; init; }

    /// <summary>Nom de la ressource embarquée du gabarit vierge (fichier seul, sans namespace).</summary>
    [JsonPropertyName("templateFile")]
    public required string TemplateFile { get; init; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; init; }

    [JsonPropertyName("fields")]
    public required IReadOnlyList<OfficialFormField> Fields { get; init; }

    private Dictionary<string, OfficialFormField>? _byKey;

    /// <summary>Index par clé, construit à la demande puis conservé (la carte est immuable).</summary>
    public IReadOnlyDictionary<string, OfficialFormField> ByKey =>
        _byKey ??= Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    /// <summary>
    /// Charge (et met en cache) la carte du millésime demandé, ex. <c>"mensuelle-2026"</c>.
    /// </summary>
    public static OfficialFormFieldMap Load(string mapName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapName);
        return Cache.GetOrAdd(mapName, static name =>
        {
            var resourceName = $"FactuTrust.Infrastructure.Resources.OfficialForms.{name}.map.json";
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Carte de formulaire officiel introuvable : « {resourceName} ».");

            var map = JsonSerializer.Deserialize<OfficialFormFieldMap>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Carte « {name} » illisible (JSON invalide).");

            var duplicates = map.Fields
                .GroupBy(f => f.Key, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicates.Length > 0)
                throw new InvalidOperationException(
                    $"Carte « {name} » : clés dupliquées ({string.Join(", ", duplicates)}).");

            return map;
        });
    }
}
