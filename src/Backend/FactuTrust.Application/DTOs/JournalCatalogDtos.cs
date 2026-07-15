namespace FactuTrust.Application.DTOs;

public sealed record JournalFamilyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
}

public sealed record JournalDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public Guid? FamilyId { get; init; }
    public string? FamilyLabel { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
}

public sealed record CreateJournalRequest
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public Guid? FamilyId { get; init; }
}

public sealed record UpdateJournalRequest
{
    public string Label { get; init; } = null!;
    public Guid? FamilyId { get; init; }
}

public sealed record CreateJournalFamilyRequest
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
}
