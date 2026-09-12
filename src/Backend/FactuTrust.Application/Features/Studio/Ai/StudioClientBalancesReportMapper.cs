using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Projette les soldes clients MediatR vers le format tabulaire du Studio.</summary>
public static class StudioClientBalancesReportMapper
{
    public static ReportResultDto ToReportResult(IReadOnlyList<ClientBalanceReportRowDto> rows)
    {
        var columns = new[]
        {
            new ReportColumn("ClientName", "Client", "dimension"),
            new ReportColumn("TotalInvoiced", "Facturé", "measure"),
            new ReportColumn("TotalPaid", "Encaissé", "measure"),
            new ReportColumn("Balance", "Solde", "measure"),
            new ReportColumn("NotDue", "Non échu", "measure"),
            new ReportColumn("Bucket0To30", "0-30 j", "measure"),
            new ReportColumn("Bucket31To60", "31-60 j", "measure"),
            new ReportColumn("Bucket61To90", "61-90 j", "measure"),
            new ReportColumn("BucketOver90", "> 90 j", "measure"),
            new ReportColumn("Currency", "Devise", "dimension")
        };

        var projected = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ClientName"] = r.ClientName,
                ["TotalInvoiced"] = r.TotalInvoiced,
                ["TotalPaid"] = r.TotalPaid,
                ["Balance"] = r.Balance,
                ["NotDue"] = r.NotDue,
                ["Bucket0To30"] = r.Bucket0To30,
                ["Bucket31To60"] = r.Bucket31To60,
                ["Bucket61To90"] = r.Bucket61To90,
                ["BucketOver90"] = r.BucketOver90,
                ["Currency"] = r.Currency
            })
            .ToList();

        return new ReportResultDto(columns, projected, projected.Count);
    }
}
