using FactuTrust.Domain.Entities.AI;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge la conversation uniquement si elle appartient à <paramref name="userId"/> — filtre
    /// appliqué dans la requête (pas de vérification post-chargement) pour éviter toute fuite
    /// d'existence : une conversation d'un autre utilisateur du même tenant retourne null,
    /// identique à un identifiant inexistant.
    /// </summary>
    Task<Conversation?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads conversation metadata plus the most recent messages (for chat LLM context), filtré
    /// par <paramref name="userId"/> dans la requête (même contrat que <see cref="GetByIdForUserAsync"/> :
    /// pas de fuite d'existence, une conversation d'un autre utilisateur retourne null).
    /// </summary>
    Task<Conversation?> GetByIdForChatAsync(Guid id, Guid userId, int maxMessages, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Conversations de l'utilisateur pour un expert de module donné (0 = assistant global).</summary>
    Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, int agentScope, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
