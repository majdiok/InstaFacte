using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Honoraires;

public sealed class HonorairesAttachment : Entity
{
    public HonorairesAttachmentDocumentKind DocumentKind { get; private set; }
    public Guid DocumentId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageRelativePath { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }
    public string? UploadedBy { get; private set; }

    private HonorairesAttachment() { }

    public static Result<HonorairesAttachment> Create(
        HonorairesAttachmentDocumentKind documentKind,
        Guid documentId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageRelativePath,
        string? uploadedBy)
    {
        if (documentId == Guid.Empty)
            return Result.Failure<HonorairesAttachment>(Error.Validation("DocumentId", "Document invalide"));
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<HonorairesAttachment>(Error.Validation("FileName", "Le nom de fichier est obligatoire"));
        if (sizeBytes <= 0)
            return Result.Failure<HonorairesAttachment>(Error.Validation("SizeBytes", "Fichier vide"));

        return Result.Success(new HonorairesAttachment
        {
            DocumentKind = documentKind,
            DocumentId = documentId,
            FileName = fileName.Trim(),
            ContentType = contentType?.Trim() ?? "application/octet-stream",
            SizeBytes = sizeBytes,
            StorageRelativePath = storageRelativePath,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
        });
    }
}
