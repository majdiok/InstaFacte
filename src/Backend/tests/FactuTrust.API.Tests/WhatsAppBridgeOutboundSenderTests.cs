using FactuTrust.API.Services.Channels;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Channels.Bridge;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Adaptateur d'envoi sortant (<see cref="WhatsAppBridgeOutboundSender"/>, projet API) : best-effort
/// strict — false quand désactivé ou entrées vides, délègue au pont sinon, ne lève jamais (même si
/// le pont lève).
/// </summary>
public sealed class WhatsAppBridgeOutboundSenderTests
{
    private static WhatsAppBridgeOutboundSender Build(Mock<IWhatsAppBridge> bridge, bool enabled = true)
    {
        var settings = Options.Create(new ChannelsSettings { Enabled = enabled, WhatsAppEnabled = enabled });
        return new WhatsAppBridgeOutboundSender(bridge.Object, settings, NullLogger<WhatsAppBridgeOutboundSender>.Instance);
    }

    [Fact]
    public async Task Disabled_ReturnsFalse_WithoutCallingBridge()
    {
        var bridge = new Mock<IWhatsAppBridge>(MockBehavior.Strict);

        var sender = Build(bridge, enabled: false);
        var result = await sender.SendWhatsAppTextAsync("21612345678@c.us", "bonjour");

        Assert.False(result);
        bridge.Verify(b => b.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "texte")]
    [InlineData("   ", "texte")]
    [InlineData("21612345678@c.us", "")]
    [InlineData("21612345678@c.us", "   ")]
    public async Task EmptyInputs_ReturnFalse_WithoutCallingBridge(string chatId, string text)
    {
        var bridge = new Mock<IWhatsAppBridge>(MockBehavior.Strict);

        var sender = Build(bridge);
        var result = await sender.SendWhatsAppTextAsync(chatId, text);

        Assert.False(result);
        bridge.Verify(b => b.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HappyPath_DelegatesToBridge()
    {
        var bridge = new Mock<IWhatsAppBridge>();
        bridge.Setup(b => b.SendTextAsync("21612345678@c.us", "réponse", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sender = Build(bridge);
        var result = await sender.SendWhatsAppTextAsync("21612345678@c.us", "réponse");

        Assert.True(result);
        bridge.Verify(b => b.SendTextAsync("21612345678@c.us", "réponse", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BridgeNotReady_ReturnsFalse()
    {
        var bridge = new Mock<IWhatsAppBridge>();
        bridge.Setup(b => b.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // pont non prêt → renvoie false
        bridge.SetupGet(b => b.Status).Returns(new BridgeStatus(BridgeState.WaitingQr, null, null, DateTime.UtcNow));

        var sender = Build(bridge);
        var result = await sender.SendWhatsAppTextAsync("21612345678@c.us", "réponse");

        Assert.False(result);
    }

    [Fact]
    public async Task BridgeThrows_IsSwallowed_ReturnsFalse()
    {
        var bridge = new Mock<IWhatsAppBridge>();
        bridge.Setup(b => b.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pont HS"));

        var sender = Build(bridge);
        var result = await sender.SendWhatsAppTextAsync("21612345678@c.us", "réponse");

        Assert.False(result);
    }
}
