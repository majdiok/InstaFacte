using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ConversationRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task<Conversation?> GetByIdForChatAsync(
        Guid id,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (maxMessages <= 0)
            return await GetByIdAsync(id, cancellationToken);

        await using var context = _contextFactory.CreateContext();
        var conversation = await context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conversation is null)
            return null;

        var messages = await context.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderByDescending(m => m.SortOrder)
            .Take(maxMessages)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        conversation.ReplaceMessagesForChatContext(messages);
        return conversation;
    }

    public async Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, int agentScope, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.UserId == userId && c.AgentScope == agentScope)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var tracked = await context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversation.Id, cancellationToken);
        if (tracked is null)
        {
            context.Conversations.Add(conversation);
            foreach (var message in conversation.Messages)
                context.Entry(message).State = EntityState.Added;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        tracked.UpdateTitle(conversation.Title);
        context.Entry(tracked).Property(nameof(Conversation.LastMessageAt)).CurrentValue = conversation.LastMessageAt;
        context.Entry(tracked).Property(nameof(Conversation.UpdatedAt)).CurrentValue = conversation.UpdatedAt;

        var existingMessageIds = await context.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var existingSet = existingMessageIds.Count > 0
            ? existingMessageIds.ToHashSet()
            : [];

        foreach (var message in conversation.Messages)
        {
            if (existingSet.Contains(message.Id))
                continue;
            context.ConversationMessages.Add(message);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var conversation = await context.Conversations.FindAsync(new object[] { id }, cancellationToken);
        if (conversation is not null)
        {
            context.Conversations.Remove(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
