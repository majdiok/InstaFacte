using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record NumberingBlockDto
{
    public NumberingBlockType Type { get; init; }
    public string? Value { get; init; }
    public int Order { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed record NumberingSchemeDto
{
    public NumberingDocumentType DocumentType { get; init; }
    public string DocumentTypeDisplay { get; init; } = string.Empty;
    public int FiscalYear { get; init; }
    public int StartNumber { get; init; }
    public int CurrentSequence { get; init; }
    public int NextSequence { get; init; }
    public int MinimumStartNumber { get; init; }
    public bool HasIssuedDocuments { get; init; }
    public IReadOnlyList<NumberingBlockDto> Blocks { get; init; } = Array.Empty<NumberingBlockDto>();
    public bool IsFormatLocked { get; init; }
    public string ExamplePreview { get; init; } = string.Empty;
}

public sealed record SaveNumberingSchemeRequest
{
    public int StartNumber { get; init; }
    public IReadOnlyList<NumberingBlockDto> Blocks { get; init; } = Array.Empty<NumberingBlockDto>();
}

public sealed record PreviewNumberingRequest
{
    public int StartNumber { get; init; }
    public IReadOnlyList<NumberingBlockDto> Blocks { get; init; } = Array.Empty<NumberingBlockDto>();
    public int? FiscalYear { get; init; }
}

public sealed record PreviewNumberingResponse
{
    public string Example { get; init; } = string.Empty;
}