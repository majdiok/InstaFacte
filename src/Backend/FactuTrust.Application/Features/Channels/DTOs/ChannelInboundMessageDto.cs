namespace FactuTrust.Application.Features.Channels.DTOs;

/// <summary>
/// Message entrant transmis par la passerelle de canal (webhook signé HMAC). Record à primitives
/// sérialisables : il transite par la file Hangfire (fire-and-forget) entre le webhook et
/// l'orchestrateur de traitement.
/// </summary>
public sealed record ChannelInboundMessageDto
{
    /// <summary>Canal source (« whatsapp » en v1).</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Identifiant unique du message côté canal (clé d'idempotence).</summary>
    public string ExternalMessageId { get; init; } = string.Empty;

    /// <summary>Identité externe de l'expéditeur (ex. « 21612345678@c.us » pour WhatsApp).</summary>
    public string ExternalUserId { get; init; } = string.Empty;

    /// <summary>Identifiant de la discussion où répondre (= expéditeur pour une discussion directe).</summary>
    public string ExternalChatId { get; init; } = string.Empty;

    /// <summary>Texte du message.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Horodatage d'envoi côté canal (epoch secondes).</summary>
    public long SentAtUnixSeconds { get; init; }
}
