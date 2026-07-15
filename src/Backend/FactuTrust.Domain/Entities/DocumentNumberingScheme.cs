using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class DocumentNumberingScheme
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public NumberingDocumentType DocumentType { get; private set; }
    public int FiscalYear { get; private set; }
    public int StartNumber { get; private set; }
    public int CurrentSequence { get; private set; }
    public string FormatBlocksJson { get; private set; } = null!;
    public bool IsFormatLocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private DocumentNumberingScheme() { }

    public static DocumentNumberingScheme CreateDefault(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        int currentSequence = 0)
    {
        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(documentType);
        var startNumber = currentSequence + 1;

        return new DocumentNumberingScheme
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = documentType,
            FiscalYear = fiscalYear,
            StartNumber = startNumber,
            CurrentSequence = currentSequence,
            FormatBlocksJson = NumberingSchemeDefaults.SerializeBlocks(blocks),
            IsFormatLocked = currentSequence > 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public IReadOnlyList<NumberingFormatBlock> GetBlocks() =>
        NumberingSchemeDefaults.DeserializeBlocks(FormatBlocksJson);

    public Result UpdateFormat(IReadOnlyList<NumberingFormatBlock> blocks)
    {
        if (IsFormatLocked)
            return Result.Failure(Error.Validation("Format",
                "Le format ne peut pas etre modifie car des documents ont deja ete emis."));

        var validation = NumberingFormatRenderer.ValidateBlocks(blocks);
        if (validation.IsFailure)
            return validation;

        FormatBlocksJson = NumberingSchemeDefaults.SerializeBlocks(blocks);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result UpdateStartNumber(int startNumber, bool allowBelowCurrent = false)
    {
        if (startNumber < 1)
            return Result.Failure(Error.Validation("StartNumber",
                "Le debut de numerotation doit etre superieur ou egal a 1."));

        if (!allowBelowCurrent && startNumber <= CurrentSequence)
            return Result.Failure(Error.Validation("StartNumber",
                $"Le numero de depart doit etre superieur a {CurrentSequence} (dernier numero emis). Valeur minimale : {CurrentSequence + 1}."));

        StartNumber = startNumber;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void ReconcileCurrentSequence(int effectiveSequence)
    {
        if (effectiveSequence <= CurrentSequence)
            return;

        CurrentSequence = effectiveSequence;
        if (effectiveSequence > 0)
            IsFormatLocked = true;

        UpdatedAt = DateTime.UtcNow;
    }

    public int ReserveNextSequence()
    {
        var next = Math.Max(CurrentSequence + 1, StartNumber);
        CurrentSequence = next;
        UpdatedAt = DateTime.UtcNow;

        if (!IsFormatLocked)
            IsFormatLocked = true;

        return next;
    }

    public int PreviewNextSequence() =>
        Math.Max(CurrentSequence + 1, StartNumber);

    public void ResetToDefault()
    {
        if (IsFormatLocked)
            throw new InvalidOperationException("Cannot reset locked scheme format.");

        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(DocumentType);
        FormatBlocksJson = NumberingSchemeDefaults.SerializeBlocks(blocks);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Repairs a corrupt/unrenderable stored format by replacing it with the default blocks for
    /// this document type. Unlike <see cref="ResetToDefault"/>, this ignores <see cref="IsFormatLocked"/>
    /// because a format that cannot render (e.g. missing the document-number block) is unusable and
    /// must be recoverable even after documents have been emitted. The sequence counters
    /// (<see cref="CurrentSequence"/>, <see cref="StartNumber"/>) are left untouched so numbering
    /// continuity is preserved.
    /// </summary>
    public void RepairFormatToDefault()
    {
        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(DocumentType);
        FormatBlocksJson = NumberingSchemeDefaults.SerializeBlocks(blocks);
        UpdatedAt = DateTime.UtcNow;
    }
}