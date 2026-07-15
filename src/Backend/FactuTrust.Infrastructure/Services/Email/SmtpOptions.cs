using FactuTrust.Domain.Constants;

namespace FactuTrust.Infrastructure.Services.Email;

/// <summary>
/// Lot C2 — Configuration SMTP (section <c>Smtp</c> de <c>appsettings.json</c>).
///
/// Quand <see cref="Enabled"/> = false, le service email tombe en mode <i>capture</i> :
/// les <c>EmailMessage</c> sont créés en BD avec status <c>Skipped</c> (audit conservé)
/// mais aucun appel SMTP réel n'est effectué. Idéal pour développement / tests.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "noreply@factutrust.tn";
    public string FromName { get; set; } = BrandConstants.Name;

    /// <summary>Domaine canonique pour les en-têtes <c>List-Unsubscribe</c>.</summary>
    public string? UnsubscribeUrl { get; set; }
}
