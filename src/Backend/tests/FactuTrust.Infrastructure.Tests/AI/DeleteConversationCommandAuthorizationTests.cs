using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Domain.Entities.AI;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Bucket B (IDOR) — DeleteConversationCommand.cs L20 : la suppression doit être précédée d'une
/// vérification de propriété filtrée dans la requête (GetByIdForUserAsync), pas d'un chargement
/// par id seul suivi d'un DeleteAsync inconditionnel.
/// </summary>
public sealed class DeleteConversationCommandAuthorizationTests
{
    private static Mock<ICurrentUser> CurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        return mock;
    }

    /// <summary>Négatif : un autre utilisateur ne peut pas supprimer la conversation, et DeleteAsync n'est jamais appelé.</summary>
    [Fact]
    public async Task Handle_returns_not_found_and_does_not_delete_when_conversation_belongs_to_another_user()
    {
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdForUserAsync(conversationId, otherUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var handler = new DeleteConversationCommandHandler(repo.Object, CurrentUser(otherUserId).Object);

        var result = await handler.Handle(new DeleteConversationCommand(conversationId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conversation.NotFound", result.Error.Code);
        repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.VerifyAll();
    }

    /// <summary>Positif : le propriétaire peut toujours supprimer sa propre conversation.</summary>
    [Fact]
    public async Task Handle_deletes_conversation_for_its_owner()
    {
        var ownerId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerId, "Analyse ventes", "mistral");

        var repo = new Mock<IConversationRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdForUserAsync(conversation.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        repo.Setup(r => r.DeleteAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeleteConversationCommandHandler(repo.Object, CurrentUser(ownerId).Object);

        var result = await handler.Handle(new DeleteConversationCommand(conversation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.DeleteAsync(conversation.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyAll();
    }
}
