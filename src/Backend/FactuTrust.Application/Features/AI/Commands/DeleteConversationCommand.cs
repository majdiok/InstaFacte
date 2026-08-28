using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Commands;

public sealed record DeleteConversationCommand(Guid ConversationId) : IRequest<Result>;

public sealed class DeleteConversationCommandHandler : IRequestHandler<DeleteConversationCommand, Result>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUser _currentUser;

    public DeleteConversationCommandHandler(IConversationRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        // Filtre appliqué dans la requête (userId + id) : même NotFound qu'un id inexistant si
        // la conversation appartient à un autre utilisateur du tenant (pas de fuite d'existence).
        var userId = _currentUser.UserId ?? Guid.Empty;
        var conversation = await _repository.GetByIdForUserAsync(request.ConversationId, userId, cancellationToken);
        if (conversation is null)
            return Result.Failure(new Error("Conversation.NotFound", "Conversation introuvable."));

        await _repository.DeleteAsync(request.ConversationId, cancellationToken);
        return Result.Success();
    }
}
