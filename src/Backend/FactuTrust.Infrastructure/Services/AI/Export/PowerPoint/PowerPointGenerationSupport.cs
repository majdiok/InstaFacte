using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>Shared helpers for legacy and hybrid PowerPoint generators.</summary>
internal static class PowerPointGenerationSupport
{
    internal static async Task<IReadOnlyList<AssistantResponseSlideModel>> BuildResponseModelsAsync(
        PowerPointExportRequestDto request,
        IConversationRepository conversationRepository,
        PowerPointRenderingOptions renderingOptions,
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        var byConversation = new Dictionary<Guid, Conversation?>();
        var models = new List<AssistantResponseSlideModel>(request.Responses.Count);

        foreach (var selection in request.Responses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!byConversation.TryGetValue(selection.ConversationId, out var conversation))
            {
                conversation = await conversationRepository.GetByIdAsync(selection.ConversationId, cancellationToken);
                byConversation[selection.ConversationId] = conversation;
            }

            if (conversation is null) continue;

            var model = AssistantResponseParser.Parse(
                conversation,
                selection.MessageId,
                selection.CustomTitle,
                culture);
            if (model is null) continue;

            model = AssistantResponseEnricher.Enrich(model, renderingOptions);
            model = ApplyContentFilter(model, selection.IncludeOnly);
            models.Add(model);
        }

        return models;
    }

    internal static AssistantResponseSlideModel ApplyContentFilter(
        AssistantResponseSlideModel model,
        SlideContentBlock? includeOnly)
    {
        if (includeOnly is null || includeOnly.Value == SlideContentBlock.None || includeOnly.Value == SlideContentBlock.All)
            return model;

        var keepText = includeOnly.Value.HasFlag(SlideContentBlock.Text);
        var keepKpis = includeOnly.Value.HasFlag(SlideContentBlock.KpiCards);
        var keepTables = includeOnly.Value.HasFlag(SlideContentBlock.Tables);
        var keepCharts = includeOnly.Value.HasFlag(SlideContentBlock.Charts);
        var keepSources = includeOnly.Value.HasFlag(SlideContentBlock.Sources);

        return model with
        {
            MarkdownText = keepText ? model.MarkdownText : null,
            Kpis = keepKpis ? model.Kpis : Array.Empty<DashboardKpiBlock>(),
            Tables = keepTables ? model.Tables : Array.Empty<DashboardTableBlock>(),
            Charts = keepCharts ? model.Charts : Array.Empty<DashboardChartBlock>(),
            SourceTools = keepSources ? model.SourceTools : Array.Empty<string>(),
            Sections = keepText ? model.Sections : Array.Empty<MarkdownSectionModel>()
        };
    }

    internal static CultureInfo ResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return new CultureInfo(PowerPointDeckLimits.DefaultLocale);

        try
        {
            return new CultureInfo(locale!);
        }
        catch (CultureNotFoundException)
        {
            return new CultureInfo(PowerPointDeckLimits.DefaultLocale);
        }
    }

    internal static string ResolveAuthor(PowerPointExportRequestDto request) =>
        string.IsNullOrWhiteSpace(request.AuthorName) ? BrandConstants.Name : request.AuthorName.Trim();

    internal static string BuildFileName(PowerPointExportRequestDto request, PowerPointTemplate template)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        var rawTitle = (request.Title ?? "Presentation").Trim();
        var slug = MakeSlug(rawTitle, maxLength: 60);
        return $"{BrandConstants.Name}-{template}-{slug}-{timestamp}.pptx";
    }

    internal static int CountSlidesIn(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var doc = PresentationDocument.Open(ms, isEditable: false);
            return doc.PresentationPart?.SlideParts.Count() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string MakeSlug(string input, int maxLength)
    {
        var clean = new string(input.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (clean.Contains("--", StringComparison.Ordinal))
            clean = clean.Replace("--", "-", StringComparison.Ordinal);
        clean = clean.Trim('-');
        if (clean.Length > maxLength) clean = clean[..maxLength];
        return string.IsNullOrEmpty(clean) ? "presentation" : clean;
    }
}
