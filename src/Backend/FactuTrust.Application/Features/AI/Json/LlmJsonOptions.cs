using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactuTrust.Application.Features.AI.Json;

/// <summary>
/// Options de désérialisation PARTAGÉES par les deux chemins d'import IA : la pièce comptable
/// (saisie manuelle d'écritures) et le wizard de facturation.
///
/// <para>C'est la duplication de ces options — un jeu privé dans <c>AccountingDocumentExtractor</c>,
/// un autre dans <c>ImportInvoiceFromFileHandler</c> — qui avait laissé les deux chemins également
/// fragiles. Un seul endroit à faire évoluer désormais.</para>
///
/// <para>Enregistrer les convertisseurs ici plutôt que via des attributs <c>[JsonConverter]</c> sur
/// chaque propriété est délibéré : la tolérance s'applique automatiquement à toute propriété du même
/// type, y compris celles ajoutées demain. Un attribut, on l'oublie.</para>
///
/// <para><b>Portée volontairement étroite</b> : cette instance ne sert qu'aux graphes de DTO de
/// sortie LLM. Elle ne touche ni <c>AddControllers().AddJsonOptions</c>, ni
/// <see cref="JsonSerializerOptions.Default"/>, ni aucune autre sérialisation de la solution.</para>
/// </summary>
public static class LlmJsonOptions
{
    /// <summary>Instance figée, sûre en usage concurrent.</summary>
    public static JsonSerializerOptions Tolerant { get; } = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,

            // Conservé : sans effet sur les types couverts par un convertisseur ci-dessous, mais
            // protège encore « "quantity": "2" » si un type non couvert était ajouté par mégarde.
            NumberHandling = JsonNumberHandling.AllowReadingFromString,

            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        options.Converters.Add(new LenientNullableInt32Converter());
        options.Converters.Add(new LenientNullableDecimalConverter());
        options.Converters.Add(new LenientStringConverter());
        options.Converters.Add(new LenientStringListConverter());

        // Gèle l'instance : toute mutation ultérieure lève au lieu de faire dériver silencieusement
        // le comportement d'un des deux chemins d'import.
        // populateMissingResolver: true installe le résolveur par réflexion par défaut ; sans lui,
        // MakeReadOnly() lève « must specify a TypeInfoResolver ».
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
