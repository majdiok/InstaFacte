using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectAttachment : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private ProjectAttachment() { }

    public static Result<ProjectAttachment> Create(
        Guid projectId,
        Guid uploadedByUserId,
        string fileName,
        string storagePath,
        string contentType,
        long sizeBytes,
        Guid? taskId = null)
    {
        fileName = fileName?.Trim() ?? string.Empty;
        storagePath = storagePath?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectAttachment>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (string.IsNullOrEmpty(fileName))
            return Result.Failure<ProjectAttachment>(Error.Validation("FileName", "Le nom du fichier est obligatoire"));
        if (string.IsNullOrEmpty(storagePath))
            return Result.Failure<ProjectAttachment>(Error.Validation("StoragePath", "Le chemin de stockage est obligatoire"));
        if (sizeBytes < 0)
            return Result.Failure<ProjectAttachment>(Error.Validation("SizeBytes", "Taille de fichier invalide"));
        if (fileName.Length > 260)
            fileName = fileName[..260];

        return Result.Success(new ProjectAttachment
        {
            ProjectId = projectId,
            TaskId = taskId,
            FileName = fileName,
            StoragePath = storagePath.Length > 500 ? storagePath[..500] : storagePath,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            SizeBytes = sizeBytes,
            UploadedByUserId = uploadedByUserId
        });
    }
}
