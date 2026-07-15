using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;

/// <summary>
/// Generates decks by opening a hybrid base .pptx (decorated master + layouts) and injecting IA content.
/// </summary>
public sealed class HybridTemplateGenerator
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IPowerPointThemeResolver _themeResolver;
    private readonly IPowerPointBaseTemplateRepository _templateRepository;
    private readonly ILogger<HybridTemplateGenerator> _logger;
    private readonly PowerPointValidationOptions _validationOptions;
    private readonly PowerPointRenderingOptions _renderingOptions;

    public HybridTemplateGenerator(
        IConversationRepository conversationRepository,
        IPowerPointThemeResolver themeResolver,
        IPowerPointBaseTemplateRepository templateRepository,
        ILogger<HybridTemplateGenerator> logger,
        IOptions<PowerPointValidationOptions>? validationOptions = null,
        IOptions<PowerPointRenderingOptions>? renderingOptions = null)
    {
        _conversationRepository = conversationRepository;
        _themeResolver = themeResolver;
        _templateRepository = templateRepository;
        _logger = logger;
        _validationOptions = validationOptions?.Value ?? PowerPointValidationOptions.Default;
        _renderingOptions = renderingOptions?.Value ?? new PowerPointRenderingOptions();
    }

    public async Task<PowerPointGenerationResult> GenerateAsync(
        PowerPointExportRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var culture = PowerPointGenerationSupport.ResolveCulture(request.Locale);
        var theme = _themeResolver.Resolve(request.Template);
        var dimensions = SlideDimensions.For(request.Orientation);

        var responseModels = await PowerPointGenerationSupport.BuildResponseModelsAsync(
            request, _conversationRepository, _renderingOptions, culture, cancellationToken);
        if (responseModels.Count == 0)
            throw new InvalidOperationException("Aucune réponse exploitable n'a été trouvée dans la sélection.");

        var templateBytes = _templateRepository.GetTemplateBytes(theme, request.Orientation);
        using var templateStream = new MemoryStream(templateBytes, writable: false);
        using var outputStream = new MemoryStream();
        templateStream.CopyTo(outputStream);
        outputStream.Position = 0;

        using (var document = PresentationDocument.Open(outputStream, true))
        {
            var presentationPart = document.PresentationPart
                ?? throw new InvalidOperationException("Hybrid template missing PresentationPart.");

            RemoveExistingSlides(presentationPart);

            var masterPart = presentationPart.SlideMasterParts.First();
            var layoutMapper = new TemplateLayoutMapper(masterPart);
            var defaultLayout = layoutMapper.Resolve(PowerPointSlideKind.Blank);

            var context = new SlideBuildContext(
                document,
                masterPart,
                defaultLayout,
                theme,
                dimensions,
                request,
                culture,
                PowerPointGenerationSupport.ResolveAuthor(request),
                _renderingOptions)
            {
                UseHybridMasterBackground = true
            };
            context.ConfigureHybridLayouts(layoutMapper);

            PowerPointDeckOrchestrator.RunBuilders(context, responseModels, _renderingOptions, _logger);

            cancellationToken.ThrowIfCancellationRequested();
            document.Save();
            PowerPointValidationGate.ValidateIfEnabled(document, _validationOptions, _logger);
        }

        stopwatch.Stop();
        var bytes = outputStream.ToArray();

        _logger.LogDebug(
            "HybridTemplateGenerator produced {SlideCount} slides ({SizeBytes} bytes) in {DurationMs} ms for user {UserId}",
            PowerPointGenerationSupport.CountSlidesIn(bytes),
            bytes.LongLength,
            stopwatch.ElapsedMilliseconds,
            userId);

        return new PowerPointGenerationResult
        {
            Content = bytes,
            FileName = PowerPointGenerationSupport.BuildFileName(request, theme.Template),
            SlideCount = PowerPointGenerationSupport.CountSlidesIn(bytes),
            Duration = stopwatch.Elapsed,
            ConversationIds = responseModels.Select(r => r.ConversationId).Distinct().ToList(),
            MessageIds = responseModels.Select(r => r.MessageId).ToList()
        };
    }

    private static void RemoveExistingSlides(PresentationPart presentationPart)
    {
        foreach (var slidePart in presentationPart.SlideParts.ToList())
            presentationPart.DeletePart(slidePart);

        var slideIdList = presentationPart.Presentation?.SlideIdList;
        slideIdList?.RemoveAllChildren();
    }
}
