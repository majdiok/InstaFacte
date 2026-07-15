using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class FiscalScheduleAttachment : Entity
{
    public Guid FiscalScheduleEntryId { get; private set; }
    public FiscalScheduleEntry FiscalScheduleEntry { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StoragePath { get; private set; } = null!;

    private FiscalScheduleAttachment() { }

    public static Result<FiscalScheduleAttachment> Create(
        Guid fiscalScheduleEntryId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath)
    {
        if (fiscalScheduleEntryId == Guid.Empty)
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("FiscalScheduleEntryId", "Echeance fiscale invalide."));
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("FileName", "Le nom du fichier est obligatoire."));
        if (fileName.Trim().Length > 260)
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("FileName", "Le nom du fichier ne doit pas depasser 260 caracteres."));
        if (string.IsNullOrWhiteSpace(contentType))
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("ContentType", "Le type de fichier est obligatoire."));
        if (sizeBytes <= 0)
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("SizeBytes", "Le fichier est vide."));
        if (string.IsNullOrWhiteSpace(storagePath))
            return Result.Failure<FiscalScheduleAttachment>(Error.Validation("StoragePath", "Le chemin de stockage est obligatoire."));

        return Result.Success(new FiscalScheduleAttachment
        {
            FiscalScheduleEntryId = fiscalScheduleEntryId,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            StoragePath = storagePath.Trim()
        });
    }
}
