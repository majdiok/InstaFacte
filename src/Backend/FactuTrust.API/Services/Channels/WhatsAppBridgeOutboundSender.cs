using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Services.Channels;

/// <summary>
/// Adaptateur d'envoi sortant : implémente <see cref="IChannelOutboundSender"/> en déléguant au
/// pont WhatsApp. Contrat best-effort inchangé (l'ancienne impl était le client HTTP de la
/// passerelle) : renvoie <c>false</c> si désactivé, pont non prêt ou envoi en échec ; ne lève jamais
/// (un rappel WhatsApp raté ne doit jamais casser l'e-mail ni le traitement appelant).
/// </summary>
public sealed class WhatsAppBridgeOutboundSender : IChannelOutboundSender
{
    private readonly IWhatsAppBridge _bridge;
    private readonly ChannelsSettings _settings;
    private readonly ILogger<WhatsAppBridgeOutboundSender> _logger;

    public WhatsAppBridgeOutboundSender(
        IWhatsAppBridge bridge,
        IOptions<ChannelsSettings> settings,
        ILogger<WhatsAppBridgeOutboundSender> logger)
    {
        _bridge = bridge;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendWhatsAppTextAsync(string chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || !_settings.WhatsAppEnabled)
            return false;
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            return await _bridge.SendTextAsync(chatId, text, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Envoi WhatsApp échoué ({Length} caractères).", text.Length);
            return false;
        }
    }
}
