using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Features.Numbering;

public static class NumberingMappings
{
    public static NumberingSchemeDto ToDto(
        DocumentNumberingScheme scheme,
        string examplePreview,
        int effectiveCurrentSequence)
    {
        var lastIssued = Math.Max(scheme.CurrentSequence, effectiveCurrentSequence);
        var hasIssued = scheme.IsFormatLocked || lastIssued > 0;
        var nextSequence = Math.Max(lastIssued + 1, scheme.StartNumber);
        var minimumStartNumber = hasIssued ? lastIssued + 1 : 1;

        return new NumberingSchemeDto
        {
            DocumentType = scheme.DocumentType,
            DocumentTypeDisplay = scheme.DocumentType.ToDisplayString(),
            FiscalYear = scheme.FiscalYear,
            StartNumber = scheme.StartNumber,
            CurrentSequence = lastIssued,
            NextSequence = nextSequence,
            MinimumStartNumber = minimumStartNumber,
            HasIssuedDocuments = hasIssued,
            Blocks = scheme.GetBlocks().Select(ToBlockDto).ToList(),
            IsFormatLocked = scheme.IsFormatLocked || lastIssued > 0,
            ExamplePreview = examplePreview
        };
    }

    public static NumberingSchemeDto ToDefaultDto(
        NumberingDocumentType documentType,
        int fiscalYear,
        string examplePreview,
        int effectiveCurrentSequence = 0)
    {
        var startNumber = Math.Max(1, effectiveCurrentSequence + 1);
        var hasIssued = effectiveCurrentSequence > 0;
        var nextSequence = Math.Max(effectiveCurrentSequence + 1, startNumber);
        var minimumStartNumber = hasIssued ? effectiveCurrentSequence + 1 : 1;

        return new NumberingSchemeDto
        {
            DocumentType = documentType,
            DocumentTypeDisplay = documentType.ToDisplayString(),
            FiscalYear = fiscalYear,
            StartNumber = startNumber,
            CurrentSequence = effectiveCurrentSequence,
            NextSequence = nextSequence,
            MinimumStartNumber = minimumStartNumber,
            HasIssuedDocuments = hasIssued,
            Blocks = NumberingSchemeDefaults.GetDefaultBlocks(documentType).Select(ToBlockDto).ToList(),
            IsFormatLocked = hasIssued,
            ExamplePreview = examplePreview
        };
    }

    public static NumberingBlockDto ToBlockDto(NumberingFormatBlock block) => new()
    {
        Type = block.Type,
        Value = block.Value,
        Order = block.Order,
        Label = block.Type.ToDisplayString()
    };

    public static IReadOnlyList<NumberingFormatBlock> ToDomainBlocks(IReadOnlyList<NumberingBlockDto> blocks) =>
        blocks.Select((b, index) => new NumberingFormatBlock(b.Type, b.Value, b.Order >= 0 ? b.Order : index)).ToList();
}
