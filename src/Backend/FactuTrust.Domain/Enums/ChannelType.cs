namespace FactuTrust.Domain.Enums;

/// <summary>
/// External messaging channel for ingress (link codes, identity binding, idempotency logs).
/// Values are stored as int; add new channels only with new explicit values (no reordering).
/// </summary>
public enum ChannelType
{
    Unknown = 0,
    Telegram = 1,
    WhatsApp = 2,
}
