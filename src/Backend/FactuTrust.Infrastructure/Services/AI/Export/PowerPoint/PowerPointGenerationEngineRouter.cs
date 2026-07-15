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
/// Routes export requests to legacy programmatic or hybrid template engine based on feature flags.
/// </summary>
public sealed class PowerPointGenerationEngineRouter : IPowerPointGenerator
{
    private readonly PowerPointGenerator _legacyGenerator;
    private readonly HybridTemplateGenerator _hybridGenerator;
    private readonly IPowerPointThemeResolver _themeResolver;
    private readonly IPowerPointBaseTemplateRepository _templateRepository;
    private readonly PowerPointRenderingOptions _renderingOptions;
    private readonly ILogger<PowerPointGenerationEngineRouter> _logger;

    public PowerPointGenerationEngineRouter(
        PowerPointGenerator legacyGenerator,
        HybridTemplateGenerator hybridGenerator,
        IPowerPointThemeResolver themeResolver,
        IPowerPointBaseTemplateRepository templateRepository,
        IOptions<PowerPointRenderingOptions>? renderingOptions,
        ILogger<PowerPointGenerationEngineRouter> logger)
    {
        _legacyGenerator = legacyGenerator;
        _hybridGenerator = hybridGenerator;
        _themeResolver = themeResolver;
        _templateRepository = templateRepository;
        _renderingOptions = renderingOptions?.Value ?? new PowerPointRenderingOptions();
        _logger = logger;
    }

    public async Task<PowerPointGenerationResult> GenerateAsync(
        PowerPointExportRequestDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (ShouldUseHybrid(request))
        {
            _logger.LogDebug("Routing template {Template} to hybrid engine", request.Template);
            try
            {
                return await _hybridGenerator.GenerateAsync(request, userId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Hybrid engine failed for template {Template}; falling back to legacy generator",
                    request.Template);
                return await _legacyGenerator.GenerateAsync(request, userId, cancellationToken);
            }
        }

        return await _legacyGenerator.GenerateAsync(request, userId, cancellationToken);
    }

    private bool ShouldUseHybrid(PowerPointExportRequestDto request)
    {
        if (!_renderingOptions.TemplateHybridEnabled)
            return false;

        if ((int)request.Template < _renderingOptions.HybridThemeMinId)
            return false;

        var theme = _themeResolver.Resolve(request.Template);
        if (theme.Engine != PowerPointThemeEngine.Hybrid)
            return false;

        return _templateRepository.HasTemplate(theme, request.Orientation);
    }
}
