using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Pièce justificative attachée à une écriture comptable (GED). Le fichier est stocké sur disque
/// HORS wwwroot (téléchargement uniquement via l'API authentifiée) ; cette entité ne porte que
/// les métadonnées et le chemin de stockage relatif.
/// </summary>
public sealed class JournalEntryAttachment : Entity
{
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;

    /// <summary>Nom de fichier original (affichage + téléchargement).</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Chemin relatif au dossier de stockage des pièces (jamais absolu).</summary>
    public string StoragePath { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }

    private JournalEntryAttachment() { }

    public static Result<JournalEntryAttachment> Create(
        Guid journalEntryId,
        string fileName,
        string storagePath,
        string contentType,
        long sizeBytes)
    {
        fileName = fileName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(fileName))
            return Result.Failure<JournalEntryAttachment>(Error.Validation("FileName", "Le nom du fichier est obligatoire."));
        if (fileName.Length > 260)
            fileName = fileName[..260];

        storagePath = storagePath?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(storagePath))
            return Result.Failure<JournalEntryAttachment>(Error.Validation("StoragePath", "Le chemin de stockage est obligatoire."));

        contentType = contentType?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(contentType))
            return Result.Failure<JournalEntryAttachment>(Error.Validation("ContentType", "Le type de contenu est obligatoire."));

        if (sizeBytes <= 0)
            return Result.Failure<JournalEntryAttachment>(Error.Validation("SizeBytes", "Le fichier est vide."));

        return Result.Success(new JournalEntryAttachment
        {
            Id = Guid.NewGuid(),
            JournalEntryId = journalEntryId,
            FileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes
        });
    }
}
