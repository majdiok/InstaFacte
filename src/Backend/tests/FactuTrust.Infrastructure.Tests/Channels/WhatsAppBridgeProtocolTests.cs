using System.Text.Json;
using FactuTrust.Application.Features.Channels.Bridge;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Protocole texte (lignes JSON) du pont WhatsApp : analyse de chaque type d'événement, tolérance
/// aux lignes non-JSON / de type inconnu (bruit Puppeteer), sérialisation des commandes avec
/// échappement correct (guillemets, sauts de ligne, emoji).
/// </summary>
public sealed class WhatsAppBridgeProtocolTests
{
    [Theory]
    [InlineData("initializing", BridgeState.Initializing)]
    [InlineData("waiting_qr", BridgeState.WaitingQr)]
    [InlineData("authenticated", BridgeState.Authenticated)]
    [InlineData("ready", BridgeState.Ready)]
    [InlineData("disconnected", BridgeState.Disconnected)]
    [InlineData("auth_failure", BridgeState.AuthFailure)]
    public void ParseLine_State_MapsEachKnownState(string raw, BridgeState expected)
    {
        var evt = WhatsAppBridgeProtocol.ParseLine($$"""{"type":"state","state":"{{raw}}"}""");

        var stateEvent = Assert.IsType<BridgeStateEvent>(evt);
        Assert.Equal(expected, stateEvent.State);
    }

    [Fact]
    public void ParseLine_UnknownState_ReturnsNull()
    {
        Assert.Null(WhatsAppBridgeProtocol.ParseLine("""{"type":"state","state":"quantum"}"""));
    }

    [Fact]
    public void ParseLine_Qr_ReturnsRawQr()
    {
        var evt = WhatsAppBridgeProtocol.ParseLine("""{"type":"qr","qr":"2@abc/DEF+ghi=="}""");

        var qr = Assert.IsType<BridgeQrEvent>(evt);
        Assert.Equal("2@abc/DEF+ghi==", qr.Qr);
    }

    [Fact]
    public void ParseLine_Message_ParsesAllFields()
    {
        var line = """
            {"type":"message","externalMessageId":"true_216_ABC","externalUserId":"21612345678@c.us","externalChatId":"21612345678@c.us","text":"Bonjour 👋","sentAtUnixSeconds":1720000000,"fromMe":false,"isStatus":false,"messageType":"chat"}
            """;

        var evt = WhatsAppBridgeProtocol.ParseLine(line);

        var message = Assert.IsType<BridgeMessageEvent>(evt);
        Assert.Equal("true_216_ABC", message.ExternalMessageId);
        Assert.Equal("21612345678@c.us", message.ExternalUserId);
        Assert.Equal("21612345678@c.us", message.ExternalChatId);
        Assert.Equal("Bonjour 👋", message.Text);
        Assert.Equal(1720000000, message.SentAtUnixSeconds);
        Assert.False(message.FromMe);
        Assert.False(message.IsStatus);
        Assert.Equal("chat", message.MessageType);
    }

    [Fact]
    public void ParseLine_Message_MissingRequiredIds_ReturnsNull()
    {
        Assert.Null(WhatsAppBridgeProtocol.ParseLine(
            """{"type":"message","externalUserId":"21612345678@c.us","text":"x"}"""));
    }

    [Theory]
    [InlineData("""{"type":"sent","id":"7","ok":true,"error":null}""", "7", true, null)]
    [InlineData("""{"type":"sent","id":"9","ok":false,"error":"boom"}""", "9", false, "boom")]
    public void ParseLine_Sent_ParsesAck(string line, string id, bool ok, string? error)
    {
        var evt = WhatsAppBridgeProtocol.ParseLine(line);

        var ack = Assert.IsType<BridgeSentAck>(evt);
        Assert.Equal(id, ack.Id);
        Assert.Equal(ok, ack.Ok);
        Assert.Equal(error, ack.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ceci n'est pas du json")]
    [InlineData("[1,2,3]")]                       // JSON mais pas un objet
    [InlineData("""{"noType":true}""")]           // sans champ « type »
    [InlineData("""{"type":42}""")]               // type non-chaîne
    [InlineData("""{"type":"gibberish"}""")]      // type inconnu
    [InlineData("puppeteer: navigating to blank")] // bruit de bibliothèque
    public void ParseLine_GarbageOrUnknown_ReturnsNull(string? line)
    {
        Assert.Null(WhatsAppBridgeProtocol.ParseLine(line));
    }

    [Fact]
    public void SerializeSend_EscapesQuotesNewlinesAndEmoji_RoundTrips()
    {
        var payload = WhatsAppBridgeProtocol.SerializeSend("42", "21612345678@c.us", "Ligne 1\nGuillemet \" et emoji 🎉");

        // Une seule ligne (pas de saut de ligne littéral qui casserait le cadrage stdin).
        Assert.DoesNotContain('\n', payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.Equal("send", root.GetProperty("type").GetString());
        Assert.Equal("42", root.GetProperty("id").GetString());
        Assert.Equal("21612345678@c.us", root.GetProperty("chatId").GetString());
        Assert.Equal("Ligne 1\nGuillemet \" et emoji 🎉", root.GetProperty("text").GetString());
    }

    [Fact]
    public void SerializeLogoutAndShutdown_ProduceExpectedType()
    {
        using var logout = JsonDocument.Parse(WhatsAppBridgeProtocol.SerializeLogout());
        Assert.Equal("logout", logout.RootElement.GetProperty("type").GetString());

        using var shutdown = JsonDocument.Parse(WhatsAppBridgeProtocol.SerializeShutdown());
        Assert.Equal("shutdown", shutdown.RootElement.GetProperty("type").GetString());
    }
}
