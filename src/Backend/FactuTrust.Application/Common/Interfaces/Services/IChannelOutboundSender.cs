namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Envoi sortant vers la passerelle de canal (endpoint <c>/send</c> de la passerelle Node,
/// authentifié par clé Bearer). Contrat best-effort : ne lève jamais — renvoie <c>false</c> quand
/// la fonctionnalité est désactivée, la configuration incomplète ou l'envoi en échec.
/// </summary>
public interface IChannelOutboundSender
{
    /// <summary>Envoie un message texte WhatsApp à la discussion donnée (ex. « 21612345678@c.us »).</summary>
    Task<bool> SendWhatsAppTextAsync(string chatId, string text, CancellationToken cancellationToken = default);
}
