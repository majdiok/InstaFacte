namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Options du prompt StudioBuilder pour UNE requête : quel modèle sert le tour (le digest de schéma
/// dispose d'un budget plus large sur le modèle avancé), l'intention déclarée par l'atelier, et le
/// couple (tenant, utilisateur) dont on résume le schéma Studio et le dernier plan.
/// Transmis par <c>SendChatMessageHandler</c> à <c>IAiContextBuilder.BuildSystemPromptAsync</c> ;
/// <c>null</c> hors mode Studio (comportement historique).
/// </summary>
/// <param name="UseAdvancedModel">Vrai si la requête est effectivement servie par le modèle avancé (après repli).</param>
/// <param name="StudioIntent">
/// Intention déclarée : <c>system | table | relations | form | reference_data | report | workflow | page</c>.
/// Toute autre valeur (ou <c>null</c>) est ignorée.
/// </param>
/// <param name="TenantId">Tenant dont le schéma Studio est résumé — jamais celui d'un autre client.</param>
/// <param name="UserId">Utilisateur dont le dernier plan est résumé (les plans sont privés par utilisateur).</param>
public sealed record StudioPromptOptions(bool UseAdvancedModel, string? StudioIntent, Guid TenantId, string UserId)
{
    /// <summary>Intentions reconnues par l'atelier (valeurs de <c>StudioAiIntent</c> côté frontend).</summary>
    public static readonly IReadOnlyList<string> KnownIntents =
        ["system", "table", "relations", "form", "reference_data", "report", "workflow", "page"];

    /// <summary>Intention normalisée (minuscules, sans espaces) si elle est connue, sinon <c>null</c>.</summary>
    public string? NormalizedIntent
    {
        get
        {
            var value = StudioIntent?.Trim().ToLowerInvariant();
            return value is not null && KnownIntents.Contains(value) ? value : null;
        }
    }
}
