using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Assistance IA à la migration de dossier (N1). Produit UNIQUEMENT des intrants pour le pipeline
/// d'import existant (analyse de format, mapping de colonnes, table de correspondance de comptes,
/// candidats doublons) : aucune méthode n'écrit en base. La validation et le commit restent
/// intégralement dans <c>ReferenceDataImportService</c> / <c>JournalImportService</c>, inchangés.
/// </summary>
public interface IMigrationAssistantService
{
    /// <summary>Analyse un fichier source : progiciel détecté, format, cible probable, en-têtes.</summary>
    Task<Result<MigrationAnalysisDto>> AnalyzeAsync(
        string fileName, byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Propose une correspondance colonnes source → schéma canonique de la cible
    /// (synonymes historiques d'abord, catalogue de formats connus ensuite, LLM borné en dernier).
    /// </summary>
    Task<Result<ColumnMappingSuggestionDto>> SuggestColumnMappingAsync(
        byte[] content, JournalImportFormat format, ReferenceImportTarget target,
        CancellationToken cancellationToken);

    /// <summary>
    /// Propose une correspondance comptes source → plan comptable local, en quatre étages :
    /// identité, préfixe de codification, puis LLM restreint à la liste fermée des comptes locaux.
    /// <paramref name="manualColumnMapping"/> (en-tête source → colonne canonique) permet de chaîner
    /// après validation du mapping de colonnes.
    /// </summary>
    Task<Result<AccountMappingSuggestionDto>> SuggestAccountMappingAsync(
        byte[] content, JournalImportFormat format,
        IReadOnlyDictionary<string, string>? manualColumnMapping,
        CancellationToken cancellationToken);

    /// <summary>
    /// Détecte les doublons de tiers entre le fichier à importer, le référentiel existant
    /// (clients + fournisseurs) et le fichier lui-même. Détection déterministe et explicable.
    /// </summary>
    Task<Result<ThirdPartyDuplicateDetectionDto>> DetectThirdPartyDuplicatesAsync(
        byte[] content, JournalImportFormat format, CancellationToken cancellationToken);

    /// <summary>
    /// Réécrit le fichier source en CSV à en-têtes CANONIQUES, en appliquant le mapping de colonnes
    /// validé par l'utilisateur (en-tête source → canonique). Le résultat est conçu pour être
    /// soumis tel quel à <c>reference-import/preview|commit</c> : aucune logique d'import n'est
    /// dupliquée, la validation reste celle du pipeline existant.
    /// </summary>
    Task<Result<byte[]>> ApplyColumnMappingAsync(
        byte[] content, JournalImportFormat format, ReferenceImportTarget target,
        IReadOnlyDictionary<string, string> columnMapping, CancellationToken cancellationToken);
}
