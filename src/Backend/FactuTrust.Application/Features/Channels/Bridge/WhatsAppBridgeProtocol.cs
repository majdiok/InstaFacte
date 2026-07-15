using System.Text.Json;

namespace FactuTrust.Application.Features.Channels.Bridge;

/// <summary>
/// Protocole texte (lignes JSON) entre l'hôte .NET et le pont Node WhatsApp. Pur et sans dépendance
/// externe : testable unitairement sans processus. <see cref="ParseLine"/> tolère toute ligne
/// non-JSON, de forme inattendue ou de type inconnu (bruit Puppeteer / logs de bibliothèque) en
/// renvoyant <c>null</c> — jamais d'exception. La sérialisation passe par <c>System.Text.Json</c>
/// (échappement correct des guillemets, sauts de ligne et emoji).
/// </summary>
public static class WhatsAppBridgeProtocol
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Analyse une ligne stdout du pont en événement typé, ou <c>null</c> si non exploitable.</summary>
    public static BridgeEvent? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeProp) ||
                typeProp.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return typeProp.GetString() switch
            {
                "state" => ParseState(root),
                "qr" => ParseQr(root),
                "message" => ParseMessage(root),
                "sent" => ParseSent(root),
                _ => null
            };
        }
    }

    /// <summary>Mappe la chaîne d'état émise par le pont Node vers l'enum, ou <c>null</c> si inconnue.</summary>
    public static BridgeState? MapState(string? raw) => raw switch
    {
        "initializing" => BridgeState.Initializing,
        "waiting_qr" => BridgeState.WaitingQr,
        "authenticated" => BridgeState.Authenticated,
        "ready" => BridgeState.Ready,
        "disconnected" => BridgeState.Disconnected,
        "auth_failure" => BridgeState.AuthFailure,
        _ => null
    };

    public static string SerializeSend(string id, string chatId, string text) =>
        JsonSerializer.Serialize(new { type = "send", id, chatId, text }, SerializerOptions);

    public static string SerializeLogout() =>
        JsonSerializer.Serialize(new { type = "logout" }, SerializerOptions);

    public static string SerializeShutdown() =>
        JsonSerializer.Serialize(new { type = "shutdown" }, SerializerOptions);

    private static BridgeEvent? ParseState(JsonElement root) =>
        MapState(GetString(root, "state")) is { } state ? new BridgeStateEvent(state) : null;

    private static BridgeEvent? ParseQr(JsonElement root)
    {
        var qr = GetString(root, "qr");
        return string.IsNullOrEmpty(qr) ? null : new BridgeQrEvent(qr);
    }

    private static BridgeEvent? ParseMessage(JsonElement root)
    {
        var id = GetString(root, "externalMessageId");
        var user = GetString(root, "externalUserId");
        var chat = GetString(root, "externalChatId");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(chat))
            return null;

        return new BridgeMessageEvent(
            id,
            user,
            chat,
            GetString(root, "text") ?? string.Empty,
            GetInt64(root, "sentAtUnixSeconds"),
            GetBool(root, "fromMe"),
            GetBool(root, "isStatus"),
            GetString(root, "messageType") ?? string.Empty);
    }

    private static BridgeEvent? ParseSent(JsonElement root)
    {
        var id = GetString(root, "id");
        return string.IsNullOrEmpty(id) ? null : new BridgeSentAck(id, GetBool(root, "ok"), GetString(root, "error"));
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool GetBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static long GetInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v)
            ? v
            : 0;
}
