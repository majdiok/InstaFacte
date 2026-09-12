using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Projette l'encours commercial multi-clients vers le format tabulaire du Studio.</summary>
public static class StudioClientsOutstandingReportMapper
{
    public static ReportResultDto ToReportResult(IReadOnlyList<ClientOutstandingReportRowDto> rows)
    {
        var columns = new[]
        {
            new ReportColumn("ClientName", "Client", "dimension"),
            new ReportColumn("UnpaidInvoicesAmount", "Encours factures", "measure"),
            new ReportColumn("ConfirmedOrdersAmount", "Encours commandes", "measure"),
            new ReportColumn("TotalOutstanding", "Total encours", "measure"),
            new ReportColumn("CreditLimit", "Plafond", "measure"),
            new ReportColumn("AvailableCredit", "Marge restante", "measure"),
            new ReportColumn("IsOverLimit", "Dépassement", "dimension"),
            new ReportColumn("UnpaidInvoiceCount", "Nb factures", "measure"),
            new ReportColumn("OverdueAmount", "Montant > 30 j", "measure"),
            new ReportColumn("Currency", "Devise", "dimension")
        };

        var projected = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ClientName"] = r.ClientName,
                ["UnpaidInvoicesAmount"] = r.UnpaidInvoicesAmount,
                ["ConfirmedOrdersAmount"] = r.ConfirmedOrdersAmount,
                ["TotalOutstanding"] = r.TotalOutstanding,
                ["CreditLimit"] = r.CreditLimit,
                ["AvailableCredit"] = r.AvailableCredit,
                ["IsOverLimit"] = r.IsOverLimit ? "Oui" : "Non",
                ["UnpaidInvoiceCount"] = r.UnpaidInvoiceCount,
                ["OverdueAmount"] = r.OverdueAmount,
                ["Currency"] = r.Currency
            })
            .ToList();

        return new ReportResultDto(columns, projected, projected.Count);
    }
}
