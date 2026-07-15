using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Reprise de dossier : import d'écritures (et balance d'ouverture) depuis un fichier CSV/Excel/FEC.
/// L'aperçu (dry-run) valide sans rien écrire ; le commit crée les écritures EN BROUILLON de façon
/// tout-ou-rien. Piloté par le flag <c>Accounting.DossierImportEnabled</c>.
/// </summary>
public interface IJournalImportService
{
    Task<Result<JournalImportPreviewDto>> PreviewAsync(byte[] content, JournalImportFormat format, CancellationToken cancellationToken = default);
    Task<Result<JournalImportCommitResultDto>> CommitAsync(byte[] content, JournalImportFormat format, CancellationToken cancellationToken = default);
}
