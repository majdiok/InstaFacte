using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// File attachment on a purchase receipt (PDF, JPG, PNG — max 10 Mo).
/// </summary>
public sealed class PurchaseReceiptAttachment : Entity
{
    public Guid PurchaseReceiptId { get; private set; }
    public PurchaseReceipt PurchaseReceipt { get; private set; } = null!;

    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageRelativePath { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }
    public string? UploadedBy { get; private set; }

    private PurchaseReceiptAttachment() { }

    public static Result<PurchaseReceiptAttachment> Create(
        PurchaseReceipt receipt,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageRelativePath,
        string? uploadedBy)
    {
        if (receipt is null)
            return Result.Failure<PurchaseReceiptAttachment>(Error.Validation("PurchaseReceipt", "Bon de réception invalide"));

        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<PurchaseReceiptAttachment>(Error.Validation("FileName", "Le nom de fichier est obligatoire"));

        if (sizeBytes <= 0)
            return Result.Failure<PurchaseReceiptAttachment>(Error.Validation("SizeBytes", "Fichier vide"));

        if (sizeBytes > 10 * 1024 * 1024)
            return Result.Failure<PurchaseReceiptAttachment>(Error.Validation("SizeBytes", "La taille maximale est de 10 Mo"));

        var normalizedType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = normalizedType is "application/pdf" or "image/jpeg" or "image/jpg" or "image/png";
        if (!allowed && !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PurchaseReceiptAttachment>(
                Error.Validation("ContentType", "Formats autorisés : PDF, JPG, PNG"));
        }

        return Result.Success(new PurchaseReceiptAttachment
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceipt = receipt,
            FileName = fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            SizeBytes = sizeBytes,
            StorageRelativePath = storageRelativePath,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
        });
    }
}
