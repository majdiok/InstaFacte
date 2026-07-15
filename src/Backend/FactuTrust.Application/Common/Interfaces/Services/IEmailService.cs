namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for sending emails.
/// Lot C2 — étendu avec une méthode <see cref="EnqueueTemplatedAsync"/> qui passe par
/// les templates plateforme + Hangfire pour livraison asynchrone et auditée.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email with optional attachments (synchronous, raw — utilisé en interne par
    /// le job Hangfire et conservé pour les usages "fire and forget" historiques).
    /// </summary>
    Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an invoice email to the client.
    /// </summary>
    Task SendInvoiceEmailAsync(
        string clientEmail,
        string clientName,
        string invoiceNumber,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lot C2 — Met en file un email basé sur un template plateforme (welcome, account-invited,
    /// password-reset, …). Crée immédiatement une ligne <c>EmailMessage</c> en BD pour audit
    /// et délègue l'envoi réel à un job Hangfire (asynchrone).
    /// </summary>
    Task<Guid> EnqueueTemplatedAsync(
        string toEmail,
        string? toName,
        string templateCode,
        IReadOnlyDictionary<string, object?>? model,
        Guid? relatedTenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an email attachment.
/// </summary>
public sealed class EmailAttachment
{
    public string FileName { get; init; } = null!;
    public byte[] Content { get; init; } = null!;
    public string ContentType { get; init; } = "application/octet-stream";
}
