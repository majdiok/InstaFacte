using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Queries;
using FactuTrust.Domain.Entities.AI;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Bucket B (IDOR) — GetConversationQuery.cs L23 : la conversation IA doit être filtrée par
/// ICurrentUser.UserId dans la requête, jamais chargée par ID seul (asymétrie corrigée avec
/// GetConversationsQuery qui filtre déjà via ConversationRepository.GetByUserIdAsync).
/// </summary>
public sealed class GetConversationQueryAuthorizationTests
{
    private static Mock<ICurrentUser> CurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        return mock;
    }

    /// <summary>Négatif : un autre utilisateur du même tenant ne peut pas lire la conversation.</summary>
    [Fact]
    public async Task Handle_returns_not_found_when_conversation_belongs_to_another_user()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        // Le repository applique déjà le filtre id+userId : simulateur no-match -> null.
        repo.Setup(r => r.GetByIdForUserAsync(conversationId, otherUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var handler = new GetConversationQueryHandler(repo.Object, CurrentUser(otherUserId).Object);

        var result = await handler.Handle(new GetConversationQuery(conversationId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conversation.NotFound", result.Error.Code);
        repo.VerifyAll();
        _ = ownerId; // le repository (non simulé ici) est le seul point qui connaît le vrai owner.
    }

    /// <summary>Positif : le propriétaire de la conversation peut toujours la consulter.</summary>
    [Fact]
    public async Task Handle_returns_conversation_for_its_owner()
    {
        var ownerId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerId, "Analyse ventes", "mistral");

        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdForUserAsync(conversation.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var handler = new GetConversationQueryHandler(repo.Object, CurrentUser(ownerId).Object);

        var result = await handler.Handle(new GetConversationQuery(conversation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(conversation.Id, result.Value.Id);
        repo.VerifyAll();
    }
}
