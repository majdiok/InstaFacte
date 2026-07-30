using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Exchange;

public sealed class ExchangeDocument : Entity
{
    public const int FileNameMaxLength = 260;
    public const int StoragePathMaxLength = 500;
    public const int ContentTypeMaxLength = 120;

    public Guid ThreadId { get; private set; }
    public Guid? MessageId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid? TaskId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public Guid UploadedByTenantId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private ExchangeDocument() { }

    public static Result<ExchangeDocument> Create(
        Guid threadId,
        string fileName,
        string storagePath,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        Guid uploadedByTenantId,
        Guid? messageId = null,
        Guid? requestId = null,
        Guid? taskId = null)
    {
        if (threadId == Guid.Empty)
            return Result.Failure<ExchangeDocument>(Error.Validation("Thread", "Thread invalide"));
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<ExchangeDocument>(Error.Validation("FileName", "Nom de fichier obligatoire"));
        if (string.IsNullOrWhiteSpace(storagePath))
            return Result.Failure<ExchangeDocument>(Error.Validation("StoragePath", "Chemin de stockage obligatoire"));
        if (sizeBytes <= 0)
            return Result.Failure<ExchangeDocument>(Error.Validation("Size", "Taille de fichier invalide"));
        if (uploadedByUserId == Guid.Empty || uploadedByTenantId == Guid.Empty)
            return Result.Failure<ExchangeDocument>(Error.Validation("Uploader", "Utilisateur invalide"));

        return Result.Success(new ExchangeDocument
        {
            ThreadId = threadId,
            MessageId = messageId,
            RequestId = requestId,
            TaskId = taskId,
            FileName = Truncate(fileName.Trim(), FileNameMaxLength)!,
            StoragePath = Truncate(storagePath.Trim(), StoragePathMaxLength)!,
            ContentType = Truncate(contentType?.Trim() ?? "application/octet-stream", ContentTypeMaxLength)!,
            SizeBytes = sizeBytes,
            UploadedByUserId = uploadedByUserId,
            UploadedByTenantId = uploadedByTenantId,
            UploadedAt = DateTime.UtcNow
        });
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
