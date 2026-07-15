using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Communications;

/// <summary>
/// Statut d'envoi d'un message email.
/// </summary>
public enum EmailMessageStatus
{
    /// <summary>En file d'attente (job Hangfire pas encore exécuté).</summary>
    Queued = 0,
    /// <summary>Envoi en cours (job en traitement).</summary>
    Sending = 1,
    /// <summary>Envoyé avec succès au serveur SMTP.</summary>
    Sent = 2,
    /// <summary>Échec côté SMTP (IP bloquée, mailbox full, etc.).</summary>
    Failed = 3,
    /// <summary>Le destinataire a bouncé (hard ou soft bounce — détecté via webhook si configuré).</summary>
    Bounced = 4,
    /// <summary>Annulé manuellement avant envoi.</summary>
    Cancelled = 5,
    /// <summary>
    /// Aucun envoi réel (dev mode / SMTP désactivé) — message stocké pour audit mais juste loggué.
    /// </summary>
    Skipped = 6
}

/// <summary>
/// Lot C2 — Trace persistante de chaque email envoyé (ou tenté) par la plateforme.
///
/// Stockée dans la base master pour audit + diagnostic + retry. Chaque ligne contient
/// le rendu HTML final (post Scriban) pour permettre de reconstituer ce que l'utilisateur a vu.
/// </summary>
public sealed class EmailMessage : Entity
{
    public string ToEmail { get; private set; } = null!;
    public string? ToName { get; private set; }
    public string TemplateCode { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string? RenderedHtml { get; private set; }
    public string? RenderedText { get; private set; }
    public EmailMessageStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public DateTime? BouncedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int AttemptsCount { get; private set; }
    public Guid? RelatedTenantId { get; private set; }

    private EmailMessage() { }

    public static EmailMessage CreateQueued(
        string toEmail,
        string? toName,
        string templateCode,
        string subject,
        string? renderedHtml,
        string? renderedText,
        Guid? relatedTenantId)
    {
        return new EmailMessage
        {
            ToEmail = (toEmail ?? string.Empty).Trim(),
            ToName = toName?.Trim(),
            TemplateCode = (templateCode ?? string.Empty).Trim(),
            Subject = (subject ?? string.Empty).Trim(),
            RenderedHtml = renderedHtml,
            RenderedText = renderedText,
            Status = EmailMessageStatus.Queued,
            AttemptsCount = 0,
            RelatedTenantId = relatedTenantId
        };
    }

    public void MarkSending() => Status = EmailMessageStatus.Sending;

    public void MarkSent(string? providerMessageId)
    {
        Status = EmailMessageStatus.Sent;
        ProviderMessageId = providerMessageId;
        SentAt = DateTime.UtcNow;
        ErrorMessage = null;
        AttemptsCount++;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = EmailMessageStatus.Failed;
        ErrorMessage = (errorMessage ?? "Unknown error").Trim();
        AttemptsCount++;
    }

    public void MarkSkipped(string reason)
    {
        Status = EmailMessageStatus.Skipped;
        ErrorMessage = reason;
        AttemptsCount++;
    }

    public void MarkBounced(DateTime bouncedAt, string? reason)
    {
        Status = EmailMessageStatus.Bounced;
        BouncedAt = bouncedAt;
        ErrorMessage = reason;
    }

    public void MarkOpened(DateTime openedAt) => OpenedAt = openedAt;

    /// <summary>Réinitialise le statut pour un retry manuel (statut → Queued).</summary>
    public void RequeueForRetry()
    {
        if (Status is EmailMessageStatus.Sent or EmailMessageStatus.Bounced)
            return;
        Status = EmailMessageStatus.Queued;
        ErrorMessage = null;
    }
}
