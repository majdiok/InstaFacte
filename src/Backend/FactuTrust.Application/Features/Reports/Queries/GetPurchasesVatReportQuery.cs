using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get purchases VAT report (TVA achats). <paramref name="RealizedOnly"/> = true exclut les
/// factures fournisseur Annulée (assiette légale de la déclaration mensuelle) ; false (défaut) =
/// comportement historique des états de consultation.
/// </summary>
public sealed record GetPurchasesVatReportQuery(DateTime FromDate, DateTime ToDate, bool RealizedOnly = false)
    : IRequest<Result<IReadOnlyList<PurchasesVatReportRowDto>>>;

/// <summary>
/// Handler for GetPurchasesVatReportQuery.
/// </summary>
public sealed class GetPurchasesVatReportQueryHandler
    : IRequestHandler<GetPurchasesVatReportQuery, Result<IReadOnlyList<PurchasesVatReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;

    public GetPurchasesVatReportQueryHandler(ISupplierInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<PurchasesVatReportRowDto>>> Handle(
        GetPurchasesVatReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var (invoices, _) = await _invoiceRepository.SearchAsync(
            null, null, null, from, to, 1, 10_000, false, cancellationToken);

        var byRate = new Dictionary<int, (decimal TotalVat, decimal TotalTaxable, string Currency)>();

        foreach (var inv in invoices)
        {
            // Assiette légale de la déclaration : exclure les factures fournisseur annulées.
            if (request.RealizedOnly && inv.Status == SupplierInvoiceStatus.Cancelled)
                continue;

            foreach (var line in inv.Lines)
            {
                var rate = (int)line.VatRate;
                var vat = line.VatAmount.Amount;
                var taxable = line.SubTotal.Amount;
                var currency = line.Total.Currency;
                if (byRate.TryGetValue(rate, out var existing))
                {
                    byRate[rate] = (
                        existing.TotalVat + vat,
                        existing.TotalTaxable + taxable,
                        currency
                    );
                }
                else
                {
                    byRate[rate] = (vat, taxable, currency);
                }
            }
        }

        var rows = byRate.Select(kv => new PurchasesVatReportRowDto
        {
            VatRatePercent = kv.Key,
            VatRateDisplay = ((VatRate)kv.Key).ToDisplayString(),
            TotalVatAmount = kv.Value.TotalVat,
            TotalTaxableAmount = kv.Value.TotalTaxable,
            Currency = kv.Value.Currency
        }).OrderBy(r => r.VatRatePercent).ToList();

        return Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(rows);
    }
}
