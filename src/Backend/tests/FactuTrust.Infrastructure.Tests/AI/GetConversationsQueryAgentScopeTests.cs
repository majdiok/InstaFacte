using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Queries;
using FactuTrust.Domain.Entities.AI;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class GetConversationsQueryAgentScopeTests
{
    [Fact]
    public async Task Handler_passes_scope_to_repository_and_maps_agent_scope()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Analyse ventes", "ollama:qwen2.5:3b", agentScope: 1);
        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByUserIdAsync(userId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation> { conversation });

        var handler = new GetConversationsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetConversationsQuery(userId, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value);
        Assert.Equal(1, dto.AgentScope);
        Assert.Equal("Analyse ventes", dto.Title);
        repo.VerifyAll();
    }

    /// <summary>Rétro-compatibilité : sans scope explicite, la requête filtre sur 0 (assistant global).</summary>
    [Fact]
    public async Task Handler_defaults_to_global_scope_zero()
    {
        var userId = Guid.NewGuid();
        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByUserIdAsync(userId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());

        var handler = new GetConversationsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetConversationsQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        repo.VerifyAll();
    }

    [Fact]
    public void Conversation_created_without_scope_defaults_to_global_zero()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Titre");
        Assert.Equal(0, conversation.AgentScope);
    }
}
