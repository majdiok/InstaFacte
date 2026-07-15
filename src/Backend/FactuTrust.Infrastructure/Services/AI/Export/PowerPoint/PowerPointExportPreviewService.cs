using System.Globalization;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>
/// Parses and enriches assistant messages to produce export preview metadata for the UI wizard.
/// </summary>
public sealed class PowerPointExportPreviewService : IPowerPointExportPreviewService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly PowerPointRenderingOptions _renderingOptions;

    public PowerPointExportPreviewService(
        IConversationRepository conversationRepository,
        IOptions<PowerPointRenderingOptions>? renderingOptions = null)
    {
        _conversationRepository = conversationRepository;
        _renderingOptions = renderingOptions?.Value ?? new PowerPointRenderingOptions();
    }

    public async Task<PowerPointResponsePreviewDto?> PreviewResponseAsync(
        Guid conversationId,
        Guid messageId,
        string? customTitle,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.UserId != userId)
            return null;

        var culture = new CultureInfo(PowerPointDeckLimits.DefaultLocale);
        var model = AssistantResponseParser.Parse(conversation, messageId, customTitle, culture);
        if (model is null)
            return null;

        model = AssistantResponseEnricher.Enrich(model, _renderingOptions);

        var outline = new List<string> { "Séparateur de section" };

        foreach (var section in model.Sections.Where(s => s.Kind == MarkdownSectionKind.ExecutiveSummary))
            outline.Add(section.Title);

        if (model.Kpis.Count > 0)
            outline.Add(model.Kpis.Count == 1 ? "Indicateur clé" : $"Indicateurs clés ({model.Kpis.Count})");

        if (model.Charts.Count > 0)
            outline.AddRange(model.Charts.Select(c => c.Title ?? "Graphique"));

        if (model.Tables.Count > 0)
            outline.AddRange(model.Tables.Select(t => t.Title ?? "Tableau"));

        foreach (var section in model.Sections.Where(s =>
                     s.Kind != MarkdownSectionKind.ExecutiveSummary &&
                     s.Kind != MarkdownSectionKind.KeyIndicators))
            outline.Add(section.Title);

        if (!string.IsNullOrWhiteSpace(model.MarkdownText))
            outline.Add(model.Title);

        return new PowerPointResponsePreviewDto
        {
            ConversationId = conversationId,
            MessageId = messageId,
            Title = model.Title,
            KpiCount = model.Kpis.Count,
            TableCount = model.Tables.Count,
            ChartCount = model.Charts.Count,
            SectionCount = model.Sections.Count,
            HasText = !string.IsNullOrWhiteSpace(model.MarkdownText) || model.Sections.Count > 0,
            SlideOutline = outline
        };
    }
}
