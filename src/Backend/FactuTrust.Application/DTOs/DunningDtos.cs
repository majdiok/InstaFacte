using System.ComponentModel.DataAnnotations;
using FactuTrust.Domain.Billing;

namespace FactuTrust.Application.DTOs;

// ─────────────────────────────────────────────────────────────────────────────
// Lot C6 — DTOs Renouvellement automatique + Dunning.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record DunningStepDto
{
    public int DaysAfterDueDate { get; init; }
    public DunningStepAction Action { get; init; }
    public string ActionDisplay { get; init; } = null!;
    public string? EmailTemplateCode { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed record DunningCampaignDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public IReadOnlyList<DunningStepDto> Steps { get; init; } = Array.Empty<DunningStepDto>();
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record DunningCampaignsListDto
{
    public IReadOnlyList<DunningCampaignDto> Items { get; init; } = Array.Empty<DunningCampaignDto>();
    public int ActiveCount { get; init; }
}

public sealed record CreateDunningCampaignRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = null!;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<DunningStepInput> Steps { get; init; } = Array.Empty<DunningStepInput>();

    public bool ActivateImmediately { get; init; }
}

public sealed record UpdateDunningCampaignRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = null!;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<DunningStepInput> Steps { get; init; } = Array.Empty<DunningStepInput>();
}

public sealed record DunningStepInput
{
    [Range(0, 365)]
    public int DaysAfterDueDate { get; init; }

    public DunningStepAction Action { get; init; }

    [StringLength(80)]
    public string? EmailTemplateCode { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Label { get; init; } = null!;
}

// ─────────────────────────────────────────────────────────────────────────────
// States
// ─────────────────────────────────────────────────────────────────────────────

public sealed record DunningStateDto
{
    public Guid Id { get; init; }
    public Guid SubscriptionId { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public Guid CampaignId { get; init; }
    public DateTime DueDate { get; init; }
    public int CurrentStepIndex { get; init; }
    public DateTime NextActionAt { get; init; }
    public DateTime? LastEmailSentAt { get; init; }
    public int AttemptsCount { get; init; }
    public DunningOutcome Outcome { get; init; }
    public string OutcomeDisplay { get; init; } = null!;
    public DateTime? CompletedAt { get; init; }
    public Guid? RelatedInvoiceId { get; init; }
    public string? RelatedInvoiceNumber { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record DunningStatesPageDto
{
    public IReadOnlyList<DunningStateDto> Items { get; init; } = Array.Empty<DunningStateDto>();
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int PaidCount { get; init; }
    public int SuspendedCount { get; init; }
    public int GiveUpCount { get; init; }
}

public sealed record ExtendGracePeriodRequest
{
    [Range(1, 30)]
    public int Days { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Display helpers
// ─────────────────────────────────────────────────────────────────────────────

public static class DunningDisplayExtensions
{
    public static string ToDisplayString(this DunningOutcome o) => o switch
    {
        DunningOutcome.Active => "En cours",
        DunningOutcome.Paid => "Payé",
        DunningOutcome.Suspended => "Suspendu",
        DunningOutcome.GiveUp => "Abandonné",
        _ => o.ToString()
    };

    public static string ToDisplayString(this DunningStepAction a) => a switch
    {
        DunningStepAction.SendEmail => "Envoyer e-mail",
        DunningStepAction.MarkPastDue => "Marquer impayé (PastDue)",
        DunningStepAction.SuspendSubscription => "Suspendre l'abonnement",
        _ => a.ToString()
    };
}
