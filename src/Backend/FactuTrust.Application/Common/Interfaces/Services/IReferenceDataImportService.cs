using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Reprise de dossier ÉTENDUE : import du plan comptable, du plan tiers et de la balance d'ouverture
/// (au-delà des écritures, gérées par <see cref="IJournalImportService"/>). Même patron : aperçu
/// (dry-run) puis commit tout-ou-rien. Strictement ADDITIF — un élément déjà présent est ignoré,
/// jamais écrasé. Piloté par <c>AccountingSettings.DossierImportEnabled</c>.
/// </summary>
public interface IReferenceDataImportService
{
    // Signatures historiques — INCHANGÉES (des appelants passent le CancellationToken en position).
    /// <summary><paramref name="fiscalYear"/> n'est requis que pour <see cref="ReferenceImportTarget.OpeningBalance"/>.</summary>
    Task<Result<ReferenceImportPreviewDto>> PreviewAsync(
        byte[] content, ReferenceImportTarget target, JournalImportFormat format,
        int? fiscalYear = null, CancellationToken cancellationToken = default);

    Task<Result<ReferenceImportCommitResultDto>> CommitAsync(
        byte[] content, ReferenceImportTarget target, JournalImportFormat format,
        int? fiscalYear = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Surcharge avec table de correspondance de comptes (« source;cible »), appliquée AVANT
    /// validation — utile pour une balance d'ouverture venue d'un autre progiciel. Passer
    /// <c>null</c> équivaut exactement à la surcharge historique.
    /// </summary>
    Task<Result<ReferenceImportPreviewDto>> PreviewAsync(
        byte[] content, ReferenceImportTarget target, JournalImportFormat format,
        int? fiscalYear, byte[]? accountMappingContent, CancellationToken cancellationToken = default);

    Task<Result<ReferenceImportCommitResultDto>> CommitAsync(
        byte[] content, ReferenceImportTarget target, JournalImportFormat format,
        int? fiscalYear, byte[]? accountMappingContent, CancellationToken cancellationToken = default);
}
