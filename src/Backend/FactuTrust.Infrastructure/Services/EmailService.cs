using System.Net;
using System.Net.Mail;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Communications;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Email;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Lot C2 — Implémentation réelle d'<see cref="IEmailService"/>.
///
/// Stratégie :
/// <list type="bullet">
///   <item>Si <c>SmtpOptions.Enabled</c> = false (par défaut), tout est loggué et écrit dans
///         <c>EmailMessages</c> avec status <c>Skipped</c>. Aucun appel réseau.</item>
///   <item>Sinon, envoi via <see cref="System.Net.Mail.SmtpClient"/> (intégré .NET, pas de
///         NuGet externe pour éviter les CVE de MailKit/MimeKit).</item>
///   <item>Le rendu des templates est fait par <see cref="SimpleTemplateRenderer"/> (substitution
///         <c>{{ key }}</c>, sans dépendance externe).</item>
///   <item>Pour <see cref="EnqueueTemplatedAsync"/>, l'envoi réel est délégué à un job Hangfire
///         (<see cref="SendEmailJob"/>) — l'appelant retourne immédiatement après création du
///         <c>EmailMessage</c>.</item>
/// </list>
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpOptions _options;
    private readonly MasterDbContext _db;
    private readonly IBackgroundJobClient _jobClient;

    public EmailService(
        ILogger<EmailService> logger,
        IOptions<SmtpOptions> options,
        MasterDbContext db,
        IBackgroundJobClient jobClient)
    {
        _logger = logger;
        _options = options.Value;
        _db = db;
        _jobClient = jobClient;
    }

    // ===== EnqueueTemplatedAsync =============================================
    public async Task<Guid> EnqueueTemplatedAsync(
        string toEmail,
        string? toName,
        string templateCode,
        IReadOnlyDictionary<string, object?>? model,
        Guid? relatedTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var template = EmailTemplates.Get(templateCode);
        if (template is null)
            throw new InvalidOperationException($"Template '{templateCode}' inconnu.");

        var modelDict = model ?? new Dictionary<string, object?>();
        var modelString = modelDict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());

        if (!modelString.ContainsKey("recipientName") && !string.IsNullOrEmpty(toName))
            modelString["recipientName"] = toName;
        if (!modelString.ContainsKey("timestamp"))
            modelString["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        var subject = SimpleTemplateRenderer.Render(template.SubjectTemplate, modelString);
        var html = SimpleTemplateRenderer.Render(template.HtmlBodyTemplate, modelString);
        var text = SimpleTemplateRenderer.Render(template.TextBodyTemplate, modelString);

        var message = EmailMessage.CreateQueued(toEmail, toName, templateCode, subject, html, text, relatedTenantId);
        _db.EmailMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        _jobClient.Enqueue<SendEmailJob>(job => job.ExecuteAsync(message.Id, CancellationToken.None));

        return message.Id;
    }

    // ===== SendEmailAsync (legacy + utilisé par le job interne) ===============
    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[Email-skipped] To={To} Subject={Subject} (SMTP disabled)", to, subject);
            return;
        }

        await SendViaSmtpAsync(to, null, subject, htmlBody, null, attachments, cancellationToken);
    }

    public Task SendInvoiceEmailAsync(
        string clientEmail,
        string clientName,
        string invoiceNumber,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Votre facture {invoiceNumber}";
        var html = $"<p>Bonjour {clientName},</p><p>Veuillez trouver ci-joint la facture {invoiceNumber}.</p>";
        var attachments = new[]
        {
            new EmailAttachment
            {
                FileName = $"facture-{invoiceNumber}.pdf",
                Content = pdfAttachment,
                ContentType = "application/pdf"
            }
        };
        return SendEmailAsync(clientEmail, subject, html, attachments, cancellationToken);
    }

    // ===== Internal — appelé par SendEmailJob =================================
    internal async Task SendMessageInternalAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            message.MarkSkipped("SMTP disabled (Smtp:Enabled=false)");
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[Email-skipped] To={To} Subject={Subject}", message.ToEmail, message.Subject);
            return;
        }

        message.MarkSending();
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendViaSmtpAsync(
                message.ToEmail,
                message.ToName,
                message.Subject,
                message.RenderedHtml ?? string.Empty,
                message.RenderedText,
                attachments: null,
                cancellationToken);

            message.MarkSent(providerMessageId: null);
            _logger.LogInformation(
                "Email {Id} sent to {To} (template={Template})",
                message.Id, message.ToEmail, message.TemplateCode);
        }
        catch (Exception ex)
        {
            message.MarkFailed(ex.Message);
            _logger.LogError(ex,
                "Email {Id} send failed to {To} (template={Template})",
                message.Id, message.ToEmail, message.TemplateCode);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ===== SMTP raw envoi via System.Net.Mail =================================
    private async Task SendViaSmtpAsync(
        string toEmail,
        string? toName,
        string subject,
        string htmlBody,
        string? textBody,
        IEnumerable<EmailAttachment>? attachments,
        CancellationToken cancellationToken)
    {
        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(_options.Username))
        {
            smtp.Credentials = new NetworkCredential(_options.Username, _options.Password ?? string.Empty);
        }

        var fromAddress = new MailAddress(_options.FromEmail, _options.FromName);
        var toAddress = string.IsNullOrEmpty(toName)
            ? new MailAddress(toEmail)
            : new MailAddress(toEmail, toName);

        using var mail = new MailMessage(fromAddress, toAddress)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        if (!string.IsNullOrEmpty(textBody))
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                textBody, System.Text.Encoding.UTF8, "text/plain"));
        }

        if (!string.IsNullOrEmpty(_options.UnsubscribeUrl))
        {
            mail.Headers.Add("List-Unsubscribe", $"<{_options.UnsubscribeUrl}>");
        }

        if (attachments is not null)
        {
            foreach (var att in attachments)
            {
                using var stream = new MemoryStream(att.Content);
                mail.Attachments.Add(new Attachment(stream, att.FileName, att.ContentType));
            }
        }

        await smtp.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>Lot C2 — Job Hangfire d'envoi réel d'un <c>EmailMessage</c>.</summary>
public sealed class SendEmailJob
{
    private readonly EmailService _emailService;
    private readonly MasterDbContext _db;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(IEmailService emailService, MasterDbContext db, ILogger<SendEmailJob> logger)
    {
        _emailService = (EmailService)emailService;
        _db = db;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 600, 3600 })]
    public async Task ExecuteAsync(Guid emailMessageId, CancellationToken cancellationToken = default)
    {
        var message = await _db.EmailMessages
            .FirstOrDefaultAsync(m => m.Id == emailMessageId, cancellationToken);
        if (message is null)
        {
            _logger.LogWarning("SendEmailJob: EmailMessage {Id} not found, skipping", emailMessageId);
            return;
        }

        if (message.Status is EmailMessageStatus.Sent or EmailMessageStatus.Cancelled)
        {
            _logger.LogDebug("SendEmailJob: message {Id} already in terminal state {Status}", message.Id, message.Status);
            return;
        }

        await _emailService.SendMessageInternalAsync(message, cancellationToken);
    }
}
