using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Commands;

public sealed record DeleteConversationCommand(Guid ConversationId) : IRequest<Result>;

public sealed class DeleteConversationCommandHandler : IRequestHandler<DeleteConversationCommand, Result>
{
    private readonly IConversationRepository _repository;

    public DeleteConversationCommandHandler(IConversationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
            return Result.Failure(new Error("Conversation.NotFound", "Conversation introuvable."));

        await _repository.DeleteAsync(request.ConversationId, cancellationToken);
        return Result.Success();
    }
}
