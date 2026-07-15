namespace FactuTrust.Application.Features.Channels.Bridge;

/// <summary>
/// Filtre entrant du pont WhatsApp : ne relaie que les discussions directes texte. Le pont Node
/// transmet tous les messages bruts ; c'est ce filtre (côté .NET, testable) qui écarte ses propres
/// messages, les statuts, les groupes (<c>@g.us</c>) et les médias sans texte. Reprend à
/// l'identique la logique de l'ancien filtre gateway.
/// </summary>
public static class WhatsAppBridgeMessageFilter
{
    public static bool ShouldForward(bool fromMe, bool isStatus, string? externalUserId, string? messageType, string? text)
    {
        if (fromMe || isStatus)
            return false;

        // Discussions directes uniquement : les groupes sont en @g.us, les statuts en broadcast.
        if (string.IsNullOrWhiteSpace(externalUserId) || !externalUserId.EndsWith("@c.us", StringComparison.Ordinal))
            return false;

        if (!string.Equals(messageType, "chat", StringComparison.Ordinal))
            return false;

        return !string.IsNullOrWhiteSpace(text);
    }
}
