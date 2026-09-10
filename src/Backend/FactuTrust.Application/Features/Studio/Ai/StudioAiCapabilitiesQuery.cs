using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Capacités du Studio IA exposées au client (tâche B-P0-08) : pilote l'affichage de la page
/// « Studio IA » (cartes « Que voulez-vous créer ? », interrupteur « Modèle avancé », bannières).
/// Les libellés de modèle sont des NOMS HUMAINS dérivés de la référence (préfixe fournisseur
/// « ollama: » / « openrouter: » retiré) — jamais la référence canonique complète ni une
/// chaîne de connexion.
/// </summary>
public sealed record StudioAiCapabilitiesDto(
    bool PlanPreviewEnabled,
    bool SystemGenerationEnabled,
    bool ModifyToolsEnabled,
    bool ViewToolsEnabled,
    bool ReportToolsEnabled,
    bool WorkbenchEnabled,
    bool TemplatesEnabled,
    bool PagesEnabled,
    bool AdvancedModelAvailable,
    string StandardModelLabel,
    string? AdvancedModelLabel);

public sealed record StudioAiCapabilitiesQuery() : IRequest<Result<StudioAiCapabilitiesDto>>;

public sealed class StudioAiCapabilitiesQueryHandler
    : IRequestHandler<StudioAiCapabilitiesQuery, Result<StudioAiCapabilitiesDto>>
{
    // 0 = avertissement d'incohérence de flags pas encore émis (journalisé une seule fois,
    // à la première requête — pas de IStartupFilter pour un simple avertissement de configuration).
    private static int _workbenchIncoherenceLogged;

    private readonly OllamaSettings _settings;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly ILogger<StudioAiCapabilitiesQueryHandler> _logger;

    public StudioAiCapabilitiesQueryHandler(
        IOptions<OllamaSettings> settings,
        IPlatformAiSettingsService platformAiSettings,
        ILogger<StudioAiCapabilitiesQueryHandler> logger)
    {
        _settings = settings.Value;
        _platformAiSettings = platformAiSettings;
        _logger = logger;
    }

    public async Task<Result<StudioAiCapabilitiesDto>> Handle(
        StudioAiCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        // Le workbench suppose le flux « plan → aperçu → confirmation » : sans l'aperçu, les
        // endpoints d'édition n'ont pas de sens — l'incohérence est signalée puis ignorée.
        var workbenchEnabled = _settings.EnableStudioAiWorkbench && _settings.EnableStudioAiPlanPreview;
        if (_settings.EnableStudioAiWorkbench && !_settings.EnableStudioAiPlanPreview
            && Interlocked.Exchange(ref _workbenchIncoherenceLogged, 1) == 0)
        {
            _logger.LogWarning(
                "Ollama:EnableStudioAiWorkbench ignoré : EnableStudioAiPlanPreview est désactivé.");
        }

        var standardModelLabel = HumanFriendlyModelLabel(
            await _platformAiSettings.GetStudioAiModelRefAsync(cancellationToken));

        // Modèle avancé : disponible seulement si le flag est levé ET qu'un modèle est réellement
        // configuré en back-office (colonne Master PlatformAiSettings.StudioAiAdvancedModelRef).
        // Lecture séquentielle : la base master est partagée, jamais de Task.WhenAll ici.
        var advancedModelRef = _settings.EnableStudioAiAdvancedModel
            ? await _platformAiSettings.GetStudioAiAdvancedModelRefAsync(cancellationToken)
            : null;
        var advancedModelAvailable = !string.IsNullOrWhiteSpace(advancedModelRef);

        return Result.Success(new StudioAiCapabilitiesDto(
            PlanPreviewEnabled: _settings.EnableStudioAiPlanPreview,
            SystemGenerationEnabled: _settings.EnableStudioSystemGeneration,
            ModifyToolsEnabled: _settings.EnableStudioAiModifyTools,
            ViewToolsEnabled: _settings.EnableStudioAiViewTools,
            ReportToolsEnabled: _settings.EnableStudioAiReportTools,
            WorkbenchEnabled: workbenchEnabled,
            TemplatesEnabled: _settings.EnableStudioTemplates && workbenchEnabled,
            PagesEnabled: _settings.EnableStudioPages && workbenchEnabled,
            AdvancedModelAvailable: advancedModelAvailable,
            StandardModelLabel: standardModelLabel,
            AdvancedModelLabel: advancedModelAvailable ? HumanFriendlyModelLabel(advancedModelRef) : null));
    }

    /// <summary>
    /// Libellé humain du modèle Studio : la partie « nom de modèle » de la référence canonique
    /// (ex. « ollama:qwen2.5:7b-instruct » ⇒ « qwen2.5:7b-instruct »). Chaîne de repli :
    /// référence plateforme → <c>Ollama:StudioAiModel</c> → <c>Ollama:DefaultModel</c> (même
    /// ordre que la résolution d'envoi de message). Si l'analyse est ambiguë, la valeur brute
    /// est conservée telle quelle — c'est un nom de modèle, jamais une chaîne de connexion.
    ///
    /// <para>Pour le modèle avancé, l'appelant garantit une référence non vide : la chaîne de repli
    /// ne s'applique jamais (un modèle avancé absent doit rester <c>null</c>, pas retomber sur le
    /// modèle standard).</para>
    /// </summary>
    private string HumanFriendlyModelLabel(string? studioModelRef)
    {
        var effective = !string.IsNullOrWhiteSpace(studioModelRef) ? studioModelRef
            : !string.IsNullOrWhiteSpace(_settings.StudioAiModel) ? _settings.StudioAiModel
            : _settings.DefaultModel;

        if (string.IsNullOrWhiteSpace(effective))
            return string.Empty;

        var parsed = ModelRef.Parse(effective);
        return string.IsNullOrWhiteSpace(parsed.ProviderModelId)
            ? effective.Trim()
            : parsed.ProviderModelId;
    }
}
