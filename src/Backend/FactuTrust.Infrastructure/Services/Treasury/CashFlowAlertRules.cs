using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Treasury;

/// <summary>
/// Alertes et facteurs d'influence produits par des règles déterministes.
/// </summary>
/// <remarks>
/// Ces analyses existent indépendamment du modèle de langage : une rupture de trésorerie prévue
/// doit être signalée même quand le fournisseur IA est indisponible. La couche IA vient enrichir
/// et reformuler, jamais remplacer.
/// </remarks>
public static class CashFlowAlertRules
{
    /// <summary>Part du volume mensuel au-delà de laquelle un tiers est jugé concentré.</summary>
    private const decimal ThirdPartyConcentrationThreshold = 0.30m;

    public static IReadOnlyList<CashFlowForecastInsight> Build(
        IReadOnlyList<CashFlowMonthlyAggregate> monthly,
        IReadOnlyList<CashFlowLineDraft> lines,
        decimal openingBalance,
        CashFlowForecastSettings? settings)
    {
        var insights = new List<CashFlowForecastInsight>();
        var sortOrder = 0;

        var closing = openingBalance;
        var breached = false;

        foreach (var month in monthly.OrderBy(m => m.SequenceIndex))
        {
            closing += month.Inflows - month.Outflows;

            if (closing < 0m)
            {
                insights.Add(CashFlowForecastInsight.Alert(
                    CashFlowInsightSeverity.Critical,
                    $"Rupture de trésorerie prévue en {month.PeriodStart:MMMM yyyy}",
                    "Le solde prévisionnel devient négatif : un financement ou un décalage de décaissement est nécessaire.",
                    month.PeriodStart,
                    closing,
                    CashFlowInsightOrigin.Rule,
                    sortOrder++));
                breached = true;
            }
            else if (settings is not null && closing < settings.CriticalThreshold)
            {
                insights.Add(CashFlowForecastInsight.Alert(
                    CashFlowInsightSeverity.Critical,
                    $"Seuil critique franchi en {month.PeriodStart:MMMM yyyy}",
                    $"Le solde prévisionnel passe sous le seuil critique défini ({settings.CriticalThreshold:N3}).",
                    month.PeriodStart,
                    closing,
                    CashFlowInsightOrigin.Rule,
                    sortOrder++));
                breached = true;
            }
            else if (settings is not null && closing < settings.AlertThreshold)
            {
                insights.Add(CashFlowForecastInsight.Alert(
                    CashFlowInsightSeverity.Warning,
                    $"Baisse de trésorerie prévue en {month.PeriodStart:MMMM yyyy}",
                    $"Le solde prévisionnel passe sous le seuil d'alerte défini ({settings.AlertThreshold:N3}).",
                    month.PeriodStart,
                    closing,
                    CashFlowInsightOrigin.Rule,
                    sortOrder++));
            }
        }

        insights.AddRange(BuildMajorInflowAlerts(lines, ref sortOrder));
        insights.AddRange(BuildDrivers(monthly, lines, breached, ref sortOrder));

        return insights;
    }

    /// <summary>
    /// Signale les encaissements dont le montant pèse assez pour qu'un retard change la donne.
    /// </summary>
    private static IEnumerable<CashFlowForecastInsight> BuildMajorInflowAlerts(
        IReadOnlyList<CashFlowLineDraft> lines,
        ref int sortOrder)
    {
        var inflows = lines.Where(l => l.Direction == CashFlowDirection.Inflow).ToList();
        if (inflows.Count == 0) return Array.Empty<CashFlowForecastInsight>();

        var total = inflows.Sum(l => l.Amount);
        if (total <= 0m) return Array.Empty<CashFlowForecastInsight>();

        var biggest = inflows.OrderByDescending(l => l.Amount).First();
        if (biggest.Amount / total < ThirdPartyConcentrationThreshold)
            return Array.Empty<CashFlowForecastInsight>();

        var order = sortOrder++;
        return new[]
        {
            CashFlowForecastInsight.Alert(
                CashFlowInsightSeverity.Info,
                $"Encaissement important attendu en {biggest.ExpectedDate:MMMM yyyy}",
                $"{biggest.Label} représente {biggest.Amount / total:P0} des encaissements de la période" +
                (string.IsNullOrWhiteSpace(biggest.ThirdPartyName) ? "." : $" ({biggest.ThirdPartyName})."),
                biggest.ExpectedDate,
                biggest.Amount,
                CashFlowInsightOrigin.Rule,
                order)
        };
    }

    /// <summary>Facteurs d'influence déduits de la structure des flux.</summary>
    private static IEnumerable<CashFlowForecastInsight> BuildDrivers(
        IReadOnlyList<CashFlowMonthlyAggregate> monthly,
        IReadOnlyList<CashFlowLineDraft> lines,
        bool hasBreach,
        ref int sortOrder)
    {
        var drivers = new List<CashFlowForecastInsight>();

        var bySource = lines
            .GroupBy(l => l.SourceType)
            .Select(g => new
            {
                Source = g.Key,
                Direction = g.First().Direction,
                Volume = g.Sum(l => l.Amount)
            })
            .Where(x => x.Volume > 0m)
            .OrderByDescending(x => x.Volume)
            .Take(5)
            .ToList();

        var totalVolume = lines.Sum(l => l.Amount);
        if (totalVolume <= 0m) return drivers;

        foreach (var source in bySource)
        {
            var share = source.Volume / totalVolume;
            var impact = share switch
            {
                >= 0.30m => CashFlowImpactLevel.High,
                >= 0.15m => CashFlowImpactLevel.Medium,
                _ => CashFlowImpactLevel.Low
            };

            drivers.Add(CashFlowForecastInsight.Driver(
                DescribeSource(source.Source),
                $"{share:P0} du volume projeté sur la période.",
                impact,
                source.Direction,
                CashFlowInsightOrigin.Rule,
                sortOrder++));
        }

        // Un mois nettement plus lourd que la moyenne mérite d'être nommé : c'est souvent lui qui
        // explique une tension que le solde global ne laisse pas voir.
        if (monthly.Count > 1)
        {
            var averageOutflow = monthly.Average(m => m.Outflows);
            var heaviest = monthly.OrderByDescending(m => m.Outflows).First();

            if (averageOutflow > 0m && heaviest.Outflows > averageOutflow * 1.3m)
            {
                drivers.Add(CashFlowForecastInsight.Driver(
                    $"Concentration de décaissements en {heaviest.PeriodStart:MMMM yyyy}",
                    $"Les décaissements de ce mois dépassent de {(heaviest.Outflows / averageOutflow) - 1m:P0} la moyenne de la période.",
                    hasBreach ? CashFlowImpactLevel.High : CashFlowImpactLevel.Medium,
                    CashFlowDirection.Outflow,
                    CashFlowInsightOrigin.Rule,
                    sortOrder++));
            }
        }

        return drivers;
    }

    private static string DescribeSource(CashFlowSourceType source) => source switch
    {
        CashFlowSourceType.ClientInvoice => "Échéances clients",
        CashFlowSourceType.ClientEffet => "Effets clients en portefeuille",
        CashFlowSourceType.SalesOrderBacklog => "Carnet de commandes clients",
        CashFlowSourceType.SupplierInvoice => "Échéances fournisseurs",
        CashFlowSourceType.SupplierEffet => "Effets fournisseurs",
        CashFlowSourceType.PurchaseOrderCommitment => "Engagements sur bons de commande",
        CashFlowSourceType.Payroll => "Charges salariales récurrentes",
        CashFlowSourceType.PayrollContribution => "Charges sociales et retenues",
        CashFlowSourceType.FiscalObligation => "Échéances fiscales",
        CashFlowSourceType.LoanInstallment => "Échéances d'emprunts",
        CashFlowSourceType.RecurringCommitment => "Engagements récurrents",
        CashFlowSourceType.RecurringJournalTemplate => "Écritures récurrentes",
        _ => "Autres flux"
    };
}
