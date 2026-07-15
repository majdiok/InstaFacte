using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Queries;

public sealed record PreviewPowerPointExportQuery(
    Guid ConversationId,
    Guid MessageId,
    Guid UserId,
    string? CustomTitle = null)
    : IRequest<Result<PowerPointResponsePreviewDto>>;

public sealed class PreviewPowerPointExportQueryValidator : AbstractValidator<PreviewPowerPointExportQuery>
{
    public PreviewPowerPointExportQueryValidator()
    {
        RuleFor(q => q.ConversationId).NotEmpty();
        RuleFor(q => q.MessageId).NotEmpty();
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.CustomTitle)
            .MaximumLength(200)
            .When(q => !string.IsNullOrWhiteSpace(q.CustomTitle));
    }
}

public sealed class PreviewPowerPointExportQueryHandler
    : IRequestHandler<PreviewPowerPointExportQuery, Result<PowerPointResponsePreviewDto>>
{
    private readonly IPowerPointExportPreviewService _previewService;

    public PreviewPowerPointExportQueryHandler(IPowerPointExportPreviewService previewService)
    {
        _previewService = previewService;
    }

    public async Task<Result<PowerPointResponsePreviewDto>> Handle(
        PreviewPowerPointExportQuery request,
        CancellationToken cancellationToken)
    {
        var preview = await _previewService.PreviewResponseAsync(
            request.ConversationId,
            request.MessageId,
            request.CustomTitle,
            request.UserId,
            cancellationToken);

        if (preview is null)
            return Result.Failure<PowerPointResponsePreviewDto>(
                new Error("PowerPoint.MessageNotFound", "Réponse introuvable ou accès refusé."));

        return Result.Success(preview);
    }
}
