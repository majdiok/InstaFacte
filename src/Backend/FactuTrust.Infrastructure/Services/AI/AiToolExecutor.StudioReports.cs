using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Outils d'ÉTATS de l'assistant Studio. Deux sorties pour une seule mécanique :
/// <list type="bullet">
/// <item><c>studio_run_report</c> — lecture seule, le résultat s'affiche dans la conversation ;</item>
/// <item><c>studio_plan_report</c> — prépare un plan soumis à validation, avec un échantillon de VRAIES
/// lignes dans l'aperçu ; l'enregistrement passe ensuite par l'endpoint REST de confirmation.</item>
/// </list>
/// Le modèle ne produit jamais de SQL : il nomme un préréglage ou une source et des champs, que
/// <see cref="SqlReportEngine"/> confronte au schéma réel et aux permissions.
/// </summary>
public sealed partial class AiToolExecutor
{
    /// <summary>Lignes d'échantillon jointes à l'aperçu d'un plan : de quoi juger, pas de quoi exporter.</summary>
    private const int ReportPlanSampleRows = 10;

    /// <summary>Lignes renvoyées dans la conversation : au-delà, le tableau devient illisible et coûteux en contexte.</summary>
    private const int ReportChatRows = 200;

    private AiToolResult? GuardReportTools()
    {
        if (!_ollamaSettings.EnableStudioAiReportTools)
            return AiToolResult.Error("Les états assistés ne sont pas activés.");
        if (!_ollamaSettings.EnableStudioSqlReportEngine)
            return AiToolResult.Error("Le moteur d'états sur les tables de la solution n'est pas activé.");
        if (_sqlReports is null)
            return AiToolResult.Error("Moteur d'états indisponible.");
        return null;
    }

    /// <summary>
    /// Sources d'états lisibles par CET utilisateur : préréglages métier d'abord (un seul tour d'outil
    /// suffit à les utiliser), puis les tables autorisées groupées par domaine.
    /// </summary>
    private Task<AiToolResult> HandleStudioListReportSources(Dictionary<string, object?> args, CancellationToken ct)
    {
        if (GuardReportTools() is { } denied)
            return Task.FromResult(denied);

        var presets = SqlReportPresetCatalog.All
            .Where(p => SqlReportAccessPolicy.TryAuthorize(p.FactTable, _currentUser.HasPermission, out _, out _))
            .Select(p => new
            {
                preset = p.Key,
                nom = p.DisplayName,
                domaine = SqlReportAccessPolicy.DomainLabel(p.Domain),
                description = p.Description,
                periode = p.PeriodFieldKey is not null
            })
            .ToList();

        var tables = SqlReportAccessPolicy.AllowedFor(_currentUser.HasPermission)
            .Select(t => new { source = t.Table, nom = t.DisplayName, domaine = SqlReportAccessPolicy.DomainLabel(t.Domain) })
            .ToList();

        return Task.FromResult(AiToolResult.Ok(Serialize(new
        {
            etatsPretsALEmploi = presets,
            sources = tables,
            consigne = "Préférer un `preset` : il est exact et tient en un seul appel. " +
                       "Sinon, appeler studio_describe_report_source pour connaître les vrais champs d'une `source`."
        })));
    }

    /// <summary>Champs réels d'une source (clés <c>Table_Colonne</c>) — ancre le modèle sur le schéma vivant.</summary>
    private async Task<AiToolResult> HandleStudioDescribeReportSource(Dictionary<string, object?> args, CancellationToken ct)
    {
        if (GuardReportTools() is { } denied)
            return denied;
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out _))
            return AiToolResult.Error("Aucun tenant authentifié.");

        var source = GetStringArg(args, "source");
        if (string.IsNullOrWhiteSpace(source))
            return AiToolResult.Error("source est requis (nom de la table à analyser).");

        var described = await _sqlReports!.DescribeAsync(tenantId, source!.Trim(), ct);
        if (!described.IsSuccess)
            return AiToolResult.Error(described.Error.Description);

        return AiToolResult.Ok(Serialize(new
        {
            source = source.Trim(),
            champs = described.Value.Select(f => new { cle = f.Key, libelle = f.Label, mesurable = f.Numeric }).ToList(),
            consigne = "N'utiliser QUE ces clés. Les clés suffixées __month / __quarter / __year regroupent par période."
        }));
    }

    /// <summary>Exécute un état en lecture seule et rend le résultat à la conversation. N'enregistre rien.</summary>
    private async Task<AiToolResult> HandleStudioRunReport(Dictionary<string, object?> args, CancellationToken ct)
    {
        if (GuardReportTools() is { } denied)
            return denied;
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out _))
            return AiToolResult.Error("Aucun tenant authentifié.");

        var specJson = GetStringArg(args, "spec_json");
        if (!StudioAiReportSpec.TryParse(specJson, out var spec, out var parseError) || spec is null)
            return AiToolResult.Error(parseError ?? "Spécification d'état invalide.");

        var (factTable, definition) = StudioAiReportSpec.Materialize(spec);
        var run = await _sqlReports!.RunAsync(tenantId, factTable, definition, ReportChatRows, spec.PresetKey, ct);
        if (!run.IsSuccess)
            return AiToolResult.Error(run.Error.Description);

        return AiToolResult.Ok(Serialize(new
        {
            success = true,
            title = spec.Title,
            source = factTable,
            sourceLabel = SqlReportAccessPolicy.Describe(factTable)?.DisplayName ?? factTable,
            preset = spec.PresetKey,
            // Période retenue, au format attendu par l'humanisation déterministe ({from, to}) : sans
            // elle, un résumé ne peut pas dire SUR QUOI portent les chiffres.
            period = spec.From is null && spec.To is null
                ? null
                : new
                {
                    from = spec.From?.ToString("yyyy-MM-dd"),
                    to = spec.To?.ToString("yyyy-MM-dd")
                },
            result = run.Value,
            warnings = spec.Warnings,
            message = run.Value.TotalRows == 0
                ? "Aucune donnée sur cette période."
                : $"{run.Value.TotalRows} ligne(s) de résultat."
        }));
    }

    /// <summary>
    /// Prépare l'ENREGISTREMENT d'un état. Exécute d'abord un échantillon réel, joint à l'aperçu :
    /// l'utilisateur valide sur des chiffres. Rien n'est créé avant sa confirmation.
    /// </summary>
    private async Task<AiToolResult> HandleStudioPlanReport(Dictionary<string, object?> args, CancellationToken ct)
    {
        if (GuardReportTools() is { } denied)
            return denied;
        if (!_ollamaSettings.EnableStudioAiPlanPreview)
            return AiToolResult.Error("Le flux d'aperçu Studio n'est pas activé.");
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out _))
            return AiToolResult.Error("Aucun tenant authentifié.");

        var specJson = GetStringArg(args, "spec_json");
        if (!StudioAiReportSpec.TryParse(specJson, out var spec, out var parseError) || spec is null)
            return AiToolResult.Error(parseError ?? "Spécification d'état invalide.");

        var (factTable, definition) = StudioAiReportSpec.Materialize(spec);

        // L'échantillon vaut aussi validation : si l'état ne s'exécute pas, on ne propose pas de plan.
        var sample = await _sqlReports!.RunAsync(tenantId, factTable, definition, ReportPlanSampleRows, spec.PresetKey, ct);
        if (!sample.IsSuccess)
            return AiToolResult.Error(sample.Error.Description);

        var sourceLabel = SqlReportAccessPolicy.Describe(factTable)?.DisplayName ?? factTable;
        var summary = StudioAiPlanSummary.ForReport(spec.Title, sourceLabel, definition, sample.Value, spec.Warnings);

        return await CreatePlanAsync(StudioAiPlanKind.Report, specJson!, summary, ct);
    }
}
