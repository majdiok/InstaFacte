using FactuTrust.Application.Features.Channels.Bridge;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Filtre entrant du pont : ne relaie que les discussions directes texte (jamais soi-même, ni les
/// statuts, ni les groupes @g.us, ni les médias sans texte).
/// </summary>
public sealed class WhatsAppBridgeMessageFilterTests
{
    private const string DirectUser = "21612345678@c.us";

    [Fact]
    public void DirectTextMessage_IsForwarded()
    {
        Assert.True(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: false, DirectUser, messageType: "chat", text: "Quel est mon CA ?"));
    }

    [Fact]
    public void FromMe_IsRejected()
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: true, isStatus: false, DirectUser, "chat", "coucou"));
    }

    [Fact]
    public void Status_IsRejected()
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: true, DirectUser, "chat", "story"));
    }

    [Fact]
    public void Group_IsRejected()
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: false, "120363000000000000@g.us", "chat", "salut le groupe"));
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    [InlineData("ptt")]
    [InlineData("sticker")]
    [InlineData("")]
    public void NonChatType_IsRejected(string messageType)
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: false, DirectUser, messageType, "légende"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyText_IsRejected(string? text)
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: false, DirectUser, "chat", text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jid")]
    public void MissingOrNonDirectUser_IsRejected(string? externalUserId)
    {
        Assert.False(WhatsAppBridgeMessageFilter.ShouldForward(
            fromMe: false, isStatus: false, externalUserId, "chat", "bonjour"));
    }
}
