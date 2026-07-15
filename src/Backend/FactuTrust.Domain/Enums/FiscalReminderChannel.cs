namespace FactuTrust.Domain.Enums;

public enum FiscalReminderChannel
{
    Email = 0,
    InApp = 1,
    Sms = 2,

    /// <summary>Rappel envoyé via le canal WhatsApp (passerelle de canal). Stocké int : additif sans migration.</summary>
    WhatsApp = 3
}
