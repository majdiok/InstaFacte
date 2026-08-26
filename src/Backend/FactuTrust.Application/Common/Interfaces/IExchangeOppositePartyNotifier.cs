using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolved in-app notification target for an Échanges opposite-party event.
/// </summary>
public sealed record ExchangeNotificationTarget(
    Guid RecipientTenantId,
    string? RecipientRole,
    Guid? RecipientUserId);

/// <summary>
/// Routes Échanges notifications to the opposite party: assigned collaborator
/// (else firm managers) when the company acts; company administrators when the
/// firm acts.
/// </summary>
public interface IExchangeOppositePartyNotifier
{
    Task<ExchangeNotificationTarget> NotifyAsync(
        ExchangeThread thread,
        Guid actorTenantId,
        NotificationType type,
        string title,
        string body,
        string tab,
        CancellationToken cancellationToken = default);
}
