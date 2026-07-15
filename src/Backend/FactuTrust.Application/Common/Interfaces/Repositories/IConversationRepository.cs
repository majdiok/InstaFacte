using FactuTrust.Domain.Entities.AI;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads conversation metadata plus the most recent messages (for chat LLM context).</summary>
    Task<Conversation?> GetByIdForChatAsync(Guid id, int maxMessages, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Conversations de l'utilisateur pour un expert de module donné (0 = assistant global).</summary>
    Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, int agentScope, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
