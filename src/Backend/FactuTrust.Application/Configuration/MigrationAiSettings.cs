namespace FactuTrust.Application.Configuration;

/// <summary>
/// Réglages de la migration assistée par IA (N1). <see cref="Enabled"/> est <c>false</c> par défaut :
/// tant que le flag n'est pas activé, les endpoints d'assistance répondent 404 et le produit exécute
/// exactement le code historique (import manuel via <c>ReferenceDataImportService</c>, inchangé).
/// L'assistance ne PRODUIT que des intrants (mapping de colonnes, table de correspondance, liste de
/// doublons) consommés ensuite par le pipeline d'import existant : elle n'a aucun chemin d'écriture
/// propre.
/// </summary>
public sealed class MigrationAiSettings
{
    public const string SectionName = "MigrationAi";

    /// <summary>Active les endpoints d'assistance à la migration. Défaut : désactivé.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Modèle explicite (<c>ollama:…</c> / <c>openrouter:…</c>) pour les suggestions.
    /// Null = modèle plateforme d'import (résolution existante, cf. <c>ImportAiModelResolver</c>).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Nombre maximal de comptes du plan local envoyés au modèle pour une suggestion. Au-delà,
    /// seule la classe concernée est envoyée (borne de coût/latence).
    /// </summary>
    public int MaxAccountsSentToModel { get; set; } = 400;

    /// <summary>Score minimal d'une paire de doublons de tiers pour être remontée (0..1).</summary>
    public double DuplicateMinScore { get; set; } = 0.6;

    /// <summary>Nombre maximal de paires de doublons remontées (garde-fou de lisibilité).</summary>
    public int MaxDuplicatePairs { get; set; } = 200;
}
