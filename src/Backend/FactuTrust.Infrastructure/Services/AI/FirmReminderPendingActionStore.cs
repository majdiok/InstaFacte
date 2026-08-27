using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Action en attente créée par la PREVIEW de <c>send_fiscal_deadline_reminder</c> et consommée par
/// l'endpoint de confirmation. Le nonce n'est jamais transporté ailleurs que dans l'événement SSE
/// et le corps de la requête POST de confirmation.
/// </summary>
public sealed record PendingFirmReminder(
    string Nonce,
    Guid UserId,
    Guid? ConversationId,
    Guid DeadlineId,
    Guid CompanyTenantId,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Store des actions de relance en attente de confirmation. Aucune persistance : une action non
/// consommée avant expiration (5 min par défaut) ou avant un redémarrage du processus est perdue —
/// c'est le comportement voulu, l'utilisateur relance simplement une nouvelle PREVIEW.
/// </summary>
public interface IFirmReminderPendingActionStore
{
    /// <summary>Crée une action en attente et retourne son nonce (usage unique).</summary>
    PendingFirmReminder Create(
        Guid userId,
        Guid? conversationId,
        Guid deadlineId,
        Guid companyTenantId,
        TimeSpan ttl);

    /// <summary>
    /// Consomme atomiquement le nonce : <c>TryRemove</c> d'abord (un seul gagnant possible en cas de
    /// double clic ou de confirmations concurrentes), puis validation du propriétaire et de
    /// l'expiration. Une entrée retirée n'est jamais remise en circulation, même si elle est
    /// finalement rejetée (autre utilisateur ou expirée) : c'est le prix normal de l'atomicité.
    /// </summary>
    bool TryConsume(string nonce, Guid callerUserId, out PendingFirmReminder? pending);
}

/// <inheritdoc cref="IFirmReminderPendingActionStore"/>
public sealed class FirmReminderPendingActionStore : IFirmReminderPendingActionStore
{
    private readonly ConcurrentDictionary<string, PendingFirmReminder> _pending = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public FirmReminderPendingActionStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public PendingFirmReminder Create(
        Guid userId,
        Guid? conversationId,
        Guid deadlineId,
        Guid companyTenantId,
        TimeSpan ttl)
    {
        CleanupExpired();

        var pending = new PendingFirmReminder(
            GenerateNonce(),
            userId,
            conversationId,
            deadlineId,
            companyTenantId,
            _timeProvider.GetUtcNow().Add(ttl));

        _pending[pending.Nonce] = pending;
        return pending;
    }

    public bool TryConsume(string nonce, Guid callerUserId, out PendingFirmReminder? pending)
    {
        pending = null;

        if (string.IsNullOrWhiteSpace(nonce))
            return false;

        // TryRemove AVANT toute validation : c'est ce qui garantit qu'entre deux confirmations
        // concurrentes du même nonce, une seule peut voir l'entrée — l'autre échoue immédiatement
        // sur le TryRemove, sans jamais lire un état partagé mutable après coup.
        if (!_pending.TryRemove(nonce, out var found))
            return false;

        if (found.UserId != callerUserId)
            return false;

        if (found.ExpiresAtUtc < _timeProvider.GetUtcNow())
            return false;

        pending = found;
        return true;
    }

    private void CleanupExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var (nonce, entry) in _pending)
        {
            if (entry.ExpiresAtUtc < now)
                _pending.TryRemove(nonce, out _);
        }
    }

    private static string GenerateNonce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
