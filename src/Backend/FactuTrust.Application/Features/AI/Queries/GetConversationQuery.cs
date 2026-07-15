using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Queries;

public sealed record GetConversationQuery(Guid ConversationId) : IRequest<Result<ConversationDetailDto>>;

public sealed class GetConversationQueryHandler : IRequestHandler<GetConversationQuery, Result<ConversationDetailDto>>
{
    private readonly IConversationRepository _repository;

    public GetConversationQueryHandler(IConversationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ConversationDetailDto>> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
            return Result.Failure<ConversationDetailDto>(new Error("Conversation.NotFound", "Conversation introuvable."));

        var dto = new ConversationDetailDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            LastMessageAt = conversation.LastMessageAt,
            SelectedModel = conversation.SelectedModel,
            Messages = conversation.Messages
                .OrderBy(m => m.SortOrder)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    ToolName = m.ToolName,
                    ToolCallId = m.ToolCallId
                })
                .ToList()
        };

        return Result.Success(dto);
    }
}
