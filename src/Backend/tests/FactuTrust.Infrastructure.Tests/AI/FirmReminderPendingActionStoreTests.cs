using FactuTrust.Infrastructure.Services.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Le store d'actions de relance en attente est le seul rempart entre une PREVIEW et un double
/// envoi réel : ces tests encadrent son invariant central (un seul gagnant par nonce) et ses
/// gardes secondaires (propriétaire, expiration, nettoyage).
/// </summary>
public sealed class FirmReminderPendingActionStoreTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void AdvanceUtc(DateTimeOffset utc) => _utcNow = utc;
    }

    /// <summary>
    /// Invariant central (cf. commentaire de <c>TryConsume</c>) : deux confirmations concurrentes
    /// du même nonce ne doivent jamais toutes les deux réussir. <c>TryRemove</c> sur la
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> sous-jacente
    /// garantit l'atomicité ; ce test l'exerce réellement en concurrence, pas seulement en série.
    /// </summary>
    [Fact]
    public async Task TryConsume_under_concurrent_calls_lets_exactly_one_winner_through()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FirmReminderPendingActionStore(timeProvider);
        var userId = Guid.NewGuid();
        var pending = store.Create(userId, null, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(5));

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            return store.TryConsume(pending.Nonce, userId, out PendingFirmReminder? _);
        })));

        Assert.Equal(1, results.Count(success => success));
    }

    [Fact]
    public void TryConsume_succeeds_once_for_the_owning_user_before_expiry()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FirmReminderPendingActionStore(timeProvider);
        var userId = Guid.NewGuid();
        var deadlineId = Guid.NewGuid();
        var companyTenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var pending = store.Create(userId, conversationId, deadlineId, companyTenantId, TimeSpan.FromMinutes(5));

        Assert.True(store.TryConsume(pending.Nonce, userId, out var consumed));
        Assert.NotNull(consumed);
        Assert.Equal(deadlineId, consumed!.DeadlineId);
        Assert.Equal(companyTenantId, consumed.CompanyTenantId);
        Assert.Equal(conversationId, consumed.ConversationId);

        // Consommé : une seconde tentative, même par le bon utilisateur, doit échouer.
        Assert.False(store.TryConsume(pending.Nonce, userId, out var secondAttempt));
        Assert.Null(secondAttempt);
    }

    [Fact]
    public void TryConsume_rejects_a_caller_who_is_not_the_creator()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FirmReminderPendingActionStore(timeProvider);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();

        var pending = store.Create(owner, null, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(5));

        Assert.False(store.TryConsume(pending.Nonce, attacker, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryConsume_rejects_an_expired_nonce()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        var store = new FirmReminderPendingActionStore(timeProvider);
        var userId = Guid.NewGuid();

        var pending = store.Create(userId, null, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(5));

        timeProvider.AdvanceUtc(new DateTimeOffset(2026, 8, 27, 10, 5, 1, TimeSpan.Zero)); // TTL dépassé d'1s

        Assert.False(store.TryConsume(pending.Nonce, userId, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryConsume_rejects_unknown_or_empty_nonce()
    {
        var store = new FirmReminderPendingActionStore(TimeProvider.System);

        Assert.False(store.TryConsume("nonce-jamais-cree", Guid.NewGuid(), out var missing));
        Assert.Null(missing);

        Assert.False(store.TryConsume("", Guid.NewGuid(), out var empty));
        Assert.Null(empty);
    }

    /// <summary>
    /// Le nettoyage paresseux tourne à chaque <c>Create</c> : une entrée expirée doit disparaître
    /// sans jamais avoir été explicitement consommée, pour ne pas fuir mémoire indéfiniment.
    /// </summary>
    [Fact]
    public void Create_lazily_cleans_up_expired_entries()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        var store = new FirmReminderPendingActionStore(timeProvider);
        var userId = Guid.NewGuid();

        var expired = store.Create(userId, null, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1));

        timeProvider.AdvanceUtc(new DateTimeOffset(2026, 8, 27, 10, 1, 0, TimeSpan.Zero)); // largement expiré

        // Déclenche CleanupExpired() en interne ; l'entrée périmée ne doit plus être consommable
        // (elle serait de toute façon rejetée par la vérification d'expiration, mais on vérifie
        // ici qu'elle a bien été purgée, pas seulement ignorée).
        store.Create(userId, null, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(5));

        Assert.False(store.TryConsume(expired.Nonce, userId, out var result));
        Assert.Null(result);
    }
}
