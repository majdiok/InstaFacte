using FactuTrust.Domain.Communications;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot C2 — Vue résumée d'un message email pour la liste.</summary>
public sealed record EmailMessageListItemDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string ToEmail { get; init; } = null!;
    public string? ToName { get; init; }
    public string TemplateCode { get; init; } = null!;
    public string Subject { get; init; } = null!;
    public EmailMessageStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public DateTime? SentAt { get; init; }
    public string? ErrorMessage { get; init; }
    public int AttemptsCount { get; init; }
    public Guid? RelatedTenantId { get; init; }
}

/// <summary>Lot C2 — Vue détaillée (avec corps HTML rendu).</summary>
public sealed record EmailMessageDetailDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string ToEmail { get; init; } = null!;
    public string? ToName { get; init; }
    public string TemplateCode { get; init; } = null!;
    public string Subject { get; init; } = null!;
    public string? RenderedHtml { get; init; }
    public string? RenderedText { get; init; }
    public EmailMessageStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string? ProviderMessageId { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? OpenedAt { get; init; }
    public DateTime? BouncedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public int AttemptsCount { get; init; }
    public Guid? RelatedTenantId { get; init; }
}

public sealed record EmailMessagesPageDto
{
    public IReadOnlyList<EmailMessageListItemDto> Items { get; init; } = Array.Empty<EmailMessageListItemDto>();
    public int TotalCount { get; init; }
    public int QueuedCount { get; init; }
    public int SentCount { get; init; }
    public int FailedCount { get; init; }
    public int Last24h { get; init; }
}

public sealed record SendTestEmailRequest
{
    public string ToEmail { get; init; } = null!;
}

public static class EmailMessageStatusExtensions
{
    public static string ToDisplayString(this EmailMessageStatus status) => status switch
    {
        EmailMessageStatus.Queued => "En file",
        EmailMessageStatus.Sending => "Envoi en cours",
        EmailMessageStatus.Sent => "Envoyé",
        EmailMessageStatus.Failed => "Échec",
        EmailMessageStatus.Bounced => "Rejeté",
        EmailMessageStatus.Cancelled => "Annulé",
        EmailMessageStatus.Skipped => "Ignoré (SMTP désactivé)",
        _ => status.ToString()
    };
}
