using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Studio.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Matrice des flags de l'endpoint capacités (B-P0-08) : tout coupé ⇒ tout faux, le workbench
/// exige l'aperçu de plan, templates/pages sont subordonnés au workbench, les libellés de modèle
/// sont des noms humains (jamais la référence canonique) et le modèle avancé reste absent tant
/// que la phase P5 n'est pas livrée.
/// </summary>
public sealed class StudioAiCapabilitiesQueryTests
{
    private readonly Mock<IPlatformAiSettingsService> _platformAiSettings = new();

    public StudioAiCapabilitiesQueryTests()
    {
        _platformAiSettings
            .Setup(s => s.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    [Fact]
    public async Task All_flags_off_yields_every_capability_false()
    {
        var result = await CreateHandler(new OllamaSettings
        {
            EnableStudioAiPlanPreview = false,
            EnableStudioSystemGeneration = false,
            EnableStudioAiModifyTools = false,
            EnableStudioAiViewTools = false,
            EnableStudioAiReportTools = false,
            EnableStudioAiWorkbench = false,
            EnableStudioTemplates = false,
            EnableStudioPages = false
        }).Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.False(dto.PlanPreviewEnabled);
        Assert.False(dto.SystemGenerationEnabled);
        Assert.False(dto.ModifyToolsEnabled);
        Assert.False(dto.ViewToolsEnabled);
        Assert.False(dto.ReportToolsEnabled);
        Assert.False(dto.WorkbenchEnabled);
        Assert.False(dto.TemplatesEnabled);
        Assert.False(dto.PagesEnabled);
        Assert.False(dto.AdvancedModelAvailable);
        Assert.Null(dto.AdvancedModelLabel);
    }

    [Fact]
    public async Task Workbench_requires_both_workbench_flag_and_plan_preview()
    {
        var result = await CreateHandler(new OllamaSettings
        {
            EnableStudioAiWorkbench = true,
            EnableStudioAiPlanPreview = true
        }).Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.True(result.Value.WorkbenchEnabled);
    }

    [Fact]
    public async Task Workbench_flag_without_plan_preview_is_reported_disabled()
    {
        var result = await CreateHandler(new OllamaSettings
        {
            EnableStudioAiWorkbench = true,
            EnableStudioAiPlanPreview = false
        }).Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.True(result.Value.PlanPreviewEnabled is false);
        Assert.False(result.Value.WorkbenchEnabled);
    }

    [Fact]
    public async Task Templates_and_pages_follow_the_workbench_gate()
    {
        var handler = CreateHandler(new OllamaSettings
        {
            EnableStudioAiPlanPreview = true,
            EnableStudioAiWorkbench = true,
            EnableStudioTemplates = true,
            EnableStudioPages = true
        });

        var enabled = await handler.Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);
        Assert.True(enabled.Value.TemplatesEnabled);
        Assert.True(enabled.Value.PagesEnabled);

        // Workbench coupé (ou incohérent) ⇒ templates et pages retombent à false.
        var withoutWorkbench = await CreateHandler(new OllamaSettings
        {
            EnableStudioAiPlanPreview = true,
            EnableStudioAiWorkbench = false,
            EnableStudioTemplates = true,
            EnableStudioPages = true
        }).Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);
        Assert.False(withoutWorkbench.Value.TemplatesEnabled);
        Assert.False(withoutWorkbench.Value.PagesEnabled);
    }

    [Fact]
    public async Task Standard_model_label_strips_the_provider_prefix()
    {
        _platformAiSettings
            .Setup(s => s.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:qwen2.5:7b-instruct");

        var result = await CreateHandler(new OllamaSettings())
            .Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.Equal("qwen2.5:7b-instruct", result.Value.StandardModelLabel);
        Assert.DoesNotContain("ollama:", result.Value.StandardModelLabel);
    }

    [Fact]
    public async Task Standard_model_label_falls_back_to_settings_then_default_model()
    {
        var fromSettings = await CreateHandler(new OllamaSettings { StudioAiModel = "llama3.1:8b" })
            .Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);
        Assert.Equal("llama3.1:8b", fromSettings.Value.StandardModelLabel);

        var fromDefault = await CreateHandler(new OllamaSettings { StudioAiModel = "", DefaultModel = "mistral" })
            .Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);
        Assert.Equal("mistral", fromDefault.Value.StandardModelLabel);
    }

    [Fact]
    public async Task Ambiguous_model_reference_is_kept_raw()
    {
        _platformAiSettings
            .Setup(s => s.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ollama:");

        var result = await CreateHandler(new OllamaSettings())
            .Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.Equal("ollama:", result.Value.StandardModelLabel);
    }

    [Fact]
    public async Task Advanced_model_is_not_exposed_before_phase_p5()
    {
        var result = await CreateHandler(new OllamaSettings
        {
            EnableStudioAiWorkbench = true,
            EnableStudioAiPlanPreview = true
        }).Handle(new StudioAiCapabilitiesQuery(), CancellationToken.None);

        Assert.False(result.Value.AdvancedModelAvailable);
        Assert.Null(result.Value.AdvancedModelLabel);
    }

    private StudioAiCapabilitiesQueryHandler CreateHandler(OllamaSettings settings) => new(
        Options.Create(settings),
        _platformAiSettings.Object,
        NullLogger<StudioAiCapabilitiesQueryHandler>.Instance);
}
