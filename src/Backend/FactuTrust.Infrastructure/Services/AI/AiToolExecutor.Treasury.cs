using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Outils d'assistant du module « Trésorerie prévisionnelle par IA ».
/// </summary>
/// <remarks>
/// Même contrat que <c>AiToolExecutor.Forecasting.cs</c> : les services sont optionnels, une garde
/// explicite renvoie un message compréhensible quand le module est éteint, et l'assistant ne reçoit
/// que des chiffres déjà calculés — il n'en produit aucun.
/// </remarks>
public sealed partial class AiToolExecutor
{
    private AiToolResult? GuardTreasuryForecastEnabled()
    {
        if (!_treasuryForecastOptions.Enabled || _cashFlowForecast is null || _cashFlowRepository is null)
            return AiToolResult.Error(
                "Le module Trésorerie prévisionnelle n'est pas activé pour cet espace.");

        return null;
    }

    private async Task<AiToolResult> HandleGetCashFlowForecast(
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        if (GuardTreasuryForecastEnabled() is { } guard) return guard;

        var horizon = ParseIntOrDefault(args, "horizon_months", _treasuryForecastOptions.DefaultHorizonMonths);
        if (horizon < 1 || horizon > _treasuryForecastOptions.MaxHorizonMonths)
            return AiToolResult.Error(
                $"horizon_months doit être compris entre 1 et {_treasuryForecastOptions.MaxHorizonMonths}.");

        var result = await _cashFlowForecast!.GetOrComputeAsync(horizon, cancellationToken);
        if (result.IsFailure)
            return AiToolResult.Error(result.Error.Description);

        var run = result.Value;

        // Projection résumée : l'assistant a besoin des agrégats et des signaux, pas des centaines
        // de lignes de flux — get_cash_flow_lines est là pour ça.
        var payload = new
        {
            periode = new
            {
                debut = run.PeriodStart.ToString("yyyy-MM-dd"),
                fin = run.PeriodEnd.ToString("yyyy-MM-dd"),
                horizonMois = run.HorizonMonths
            },
            devise = run.Currency,
            soldeOuverture = run.OpeningBalance,
            soldeCloture = run.ClosingBalance,
            encaissementsPrevus = run.TotalInflows,
            decaissementsPrevus = run.TotalOutflows,
            fluxNet = run.NetFlow,
            indiceConfiance = run.ConfidencePercent,
            ponderationIa = run.AiAdjustmentApplied,
            mois = run.Buckets
                .OrderBy(b => b.SequenceIndex)
                .Select(b => new
                {
                    mois = b.PeriodStart.ToString("yyyy-MM"),
                    soldeInitial = b.OpeningBalance,
                    encaissements = b.Inflows,
                    decaissements = b.Outflows,
                    fluxNet = b.NetFlow,
                    soldeFinal = b.ClosingBalance,
                    fourchette = new { basse = b.LowClosingBalance, haute = b.HighClosingBalance }
                }),
            scenarios = run.Scenarios.Select(s => new
            {
                type = CashFlowForecastMappings.ToSlug(s.Kind),
                soldeFinal = s.ClosingBalance,
                fluxNet = s.NetFlow,
                probabilite = s.ProbabilityPercent,
                probabiliteMoteur = s.DeterministicProbabilityPercent
            }),
            alertes = run.Insights
                .Where(i => i.Kind == CashFlowInsightKind.Alert)
                .OrderBy(i => i.SortOrder)
                .Select(i => new
                {
                    gravite = i.Severity.ToString().ToLowerInvariant(),
                    titre = i.Title,
                    detail = i.Detail,
                    mois = i.PeriodStart?.ToString("yyyy-MM"),
                    soldeEstime = i.EstimatedBalance
                }),
            facteurs = run.Insights
                .Where(i => i.Kind == CashFlowInsightKind.Driver)
                .OrderBy(i => i.SortOrder)
                .Select(i => new { libelle = i.Title, detail = i.Detail, impact = i.Impact?.ToString().ToLowerInvariant() })
        };

        return AiToolResult.Ok(Serialize(payload));
    }

    private async Task<AiToolResult> HandleGetCashFlowLines(
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        if (GuardTreasuryForecastEnabled() is { } guard) return guard;

        var result = await _cashFlowForecast!.GetOrComputeAsync(
            _treasuryForecastOptions.DefaultHorizonMonths,
            cancellationToken);

        if (result.IsFailure)
            return AiToolResult.Error(result.Error.Description);

        CashFlowDirection? direction;
        CashFlowSourceType? sourceType;
        try
        {
            direction = CashFlowForecastMappings.ParseOptionalDirection(GetStringArg(args, "direction"));
            sourceType = CashFlowForecastMappings.ParseSourceType(GetStringArg(args, "source_type"));
        }
        catch (ArgumentException ex)
        {
            return AiToolResult.Error(ex.Message);
        }

        var topN = Math.Clamp(ParseIntOrDefault(args, "top_n", 20), 1, 50);

        var lines = await _cashFlowRepository!.ListLinesAsync(
            new CashFlowLineQueryCriteria
            {
                ForecastRunId = result.Value.Id,
                Direction = direction,
                SourceType = sourceType,
                ExpectedFrom = ParseOptionalDate(args, "from_date"),
                ExpectedTo = ParseOptionalDate(args, "to_date"),
                Page = 1,
                PageSize = topN
            },
            cancellationToken);

        var payload = new
        {
            total = lines.TotalCount,
            montantTotalPondere = lines.TotalAmount,
            flux = lines.Items.Select(l => new
            {
                sens = CashFlowForecastMappings.ToSlug(l.Direction),
                source = CashFlowForecastMappings.ToSlug(l.SourceType),
                reference = l.SourceReference,
                libelle = l.Label,
                tiers = l.ThirdPartyName,
                dateContractuelle = l.ContractualDate.ToString("yyyy-MM-dd"),
                dateAttendue = l.ExpectedDate.ToString("yyyy-MM-dd"),
                montant = l.Amount,
                probabilite = l.ProbabilityPercent,
                certain = l.IsConfirmed
            })
        };

        return AiToolResult.Ok(Serialize(payload));
    }
}
