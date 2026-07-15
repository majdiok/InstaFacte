using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Queries;

/// <param name="AgentScope">Expert de module (0 = assistant global, valeur historique) : cloisonne les historiques.</param>
public sealed record GetConversationsQuery(Guid UserId, int AgentScope = 0) : IRequest<Result<IReadOnlyList<ConversationDto>>>;

public sealed class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, Result<IReadOnlyList<ConversationDto>>>
{
    private readonly IConversationRepository _repository;

    public GetConversationsQueryHandler(IConversationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<ConversationDto>>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = await _repository.GetByUserIdAsync(request.UserId, request.AgentScope, cancellationToken);

        var dtos = conversations
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                LastMessageAt = c.LastMessageAt,
                SelectedModel = c.SelectedModel,
                MessageCount = c.Messages.Count,
                AgentScope = c.AgentScope
            })
            .ToList();

        return Result.Success<IReadOnlyList<ConversationDto>>(dtos);
    }
}
