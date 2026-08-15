using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactuTrust.Application.Features.Accounting.Audit.Narrative;

/// <summary>
/// Les faits déterministes d'une anomalie, tels qu'ils entrent dans le dossier de révision.
///
/// <para>C'est la <b>seule</b> source des valeurs chiffrées de la note. Le modèle de langage reçoit
/// une projection de ces faits et rend de la prose ; sévérité, montant, compte et pièce sont
/// réinjectés d'ici, jamais lus de sa réponse.</para>
/// </summary>
public sealed record RevisionAnomalyFacts(
    Guid Id,
    string RuleCode,
    string ModuleCode,
    int Severity,
    string Title,
    string Description,
    decimal Amount,
    string? AccountRef,
    string? PieceRef,
    IReadOnlyList<string> Recommendations);

/// <summary>Réponse attendue du modèle. <b>Aucun champ numérique</b> — c'est le garde-fou n°1.</summary>
public sealed record RevisionDossierLlmResponse
{
    [JsonPropertyName("executiveSummary")]
    public string? ExecutiveSummary { get; init; }

    [JsonPropertyName("items")]
    public List<RevisionDossierLlmItem>? Items { get; init; }
}

/// <summary>Une entrée rédigée. <c>anomalyRef</c> doit correspondre à une référence fournie.</summary>
public sealed record RevisionDossierLlmItem
{
    [JsonPropertyName("anomalyRef")]
    public string? AnomalyRef { get; init; }

    [JsonPropertyName("workingNote")]
    public string? WorkingNote { get; init; }

    [JsonPropertyName("clientQuestion")]
    public string? ClientQuestion { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }
}

/// <summary>
/// Schéma de sortie transmis au décodeur.
///
/// <para><b>Il ne comporte aucun type numérique.</b> Le modèle est structurellement incapable de
/// produire un montant, un taux ou un compte : ces valeurs n'ont pas de place où atterrir. C'est la
/// traduction, dans le schéma lui-même, de la règle « le chiffre est calculé par du code, le mot est
/// écrit par le modèle ».</para>
/// </summary>
public static class RevisionDossierSchema
{
    private const string Json = """
        {
          "type": "object",
          "properties": {
            "executiveSummary": { "type": "string" },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "anomalyRef": { "type": "string" },
                  "workingNote": { "type": "string" },
                  "clientQuestion": { "type": ["string","null"] },
                  "action": {
                    "type": "string",
                    "enum": [
                      "extourner", "demander_la_facture", "reclasser", "lettrer",
                      "joindre_piece", "regulariser_declaration", "verifier_contrat", "aucune"
                    ]
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>Schéma désérialisé une seule fois ; <see cref="JsonElement"/> est immuable.</summary>
    public static JsonElement Instance { get; } = JsonDocument.Parse(Json).RootElement.Clone();
}
