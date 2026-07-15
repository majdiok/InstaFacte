namespace FactuTrust.Application.Features.Channels.Bridge;

/// <summary>
/// État de la session WhatsApp du pont. Les valeurs jusqu'à <see cref="AuthFailure"/> sont émises
/// par le pont Node ; <see cref="DependenciesMissing"/>/<see cref="NodeMissing"/>/<see cref="Stopped"/>
/// sont posées côté hôte .NET (pré-checks et cycle de vie).
/// </summary>
public enum BridgeState
{
    Initializing = 0,
    WaitingQr = 1,
    Authenticated = 2,
    Ready = 3,
    Disconnected = 4,
    AuthFailure = 5,

    /// <summary>node_modules absent dans le dossier du pont — `npm ci` requis (pas de spawn).</summary>
    DependenciesMissing = 6,

    /// <summary>Exécutable Node introuvable sur la machine.</summary>
    NodeMissing = 7,

    /// <summary>Hôte arrêté (fonctionnalité désactivée ou application en cours d'arrêt).</summary>
    Stopped = 8
}

/// <summary>Événement remonté par le pont Node : une ligne JSON sur stdout = un événement.</summary>
public abstract record BridgeEvent;

/// <summary>Changement d'état de la session (« state »).</summary>
public sealed record BridgeStateEvent(BridgeState State) : BridgeEvent;

/// <summary>QR brut à scanner (« qr ») — le PNG est rendu côté .NET, jamais loggé.</summary>
public sealed record BridgeQrEvent(string Qr) : BridgeEvent;

/// <summary>
/// Message entrant brut (« message ») : le pont reste « bête » et transmet les champs sans filtrer ;
/// c'est <see cref="WhatsAppBridgeMessageFilter"/> côté .NET qui décide de relayer ou non.
/// </summary>
public sealed record BridgeMessageEvent(
    string ExternalMessageId,
    string ExternalUserId,
    string ExternalChatId,
    string Text,
    long SentAtUnixSeconds,
    bool FromMe,
    bool IsStatus,
    string MessageType) : BridgeEvent;

/// <summary>Accusé d'un envoi sortant corrélé par identifiant (« sent »).</summary>
public sealed record BridgeSentAck(string Id, bool Ok, string? Error) : BridgeEvent;
