using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record ProductOnboardingChecklistDto
{
    public bool Dismissed { get; init; }
    public IReadOnlyList<string> DoneIds { get; init; } = Array.Empty<string>();
}

public sealed record ProductOnboardingDto
{
    public bool Enabled { get; init; }
    public ProductOnboardingStatus Status { get; init; }
    public int Version { get; init; }
    public ProductOnboardingChecklistDto Checklist { get; init; } = new();
    public IReadOnlyList<string> AutoCompletedIds { get; init; } = Array.Empty<string>();
}

public sealed record PatchProductOnboardingRequest
{
    public ProductOnboardingStatus? Status { get; init; }
    public string? ChecklistDoneId { get; init; }
    public bool? ChecklistDismissed { get; init; }
}
