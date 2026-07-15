using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// GED des écritures : pièces justificatives attachées à une écriture comptable.
/// Stockage disque hors wwwroot ; l'accès aux fichiers passe exclusivement par
/// <see cref="DownloadAsync"/> (endpoint authentifié).
/// </summary>
public interface IJournalEntryAttachmentService
{
    Task<Result<IReadOnlyList<JournalEntryAttachmentDto>>> ListAsync(Guid journalEntryId, CancellationToken cancellationToken = default);

    /// <summary>Attache un fichier (PDF, image, Excel/Word — 10 Mo max) à une écriture existante.</summary>
    Task<Result<JournalEntryAttachmentDto>> UploadAsync(
        Guid journalEntryId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    Task<Result<AttachmentDownload>> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Supprime la pièce (métadonnées + fichier). Refusé si la période de l'écriture est clôturée.</summary>
    Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

/// <summary>Flux de téléchargement d'une pièce (le contrôleur le transmet tel quel).</summary>
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);
