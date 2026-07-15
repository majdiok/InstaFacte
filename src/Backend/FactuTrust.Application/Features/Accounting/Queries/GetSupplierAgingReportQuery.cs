using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetSupplierAgingReportQuery : IRequest<Result<IReadOnlyList<AgingReportRowDto>>>;

public sealed class GetSupplierAgingReportQueryHandler
    : IRequestHandler<GetSupplierAgingReportQuery, Result<IReadOnlyList<AgingReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;

    public GetSupplierAgingReportQueryHandler(
        ISupplierInvoiceRepository supplierInvoiceRepository,
        ISupplierPaymentRepository supplierPaymentRepository)
    {
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
    }

    public async Task<Result<IReadOnlyList<AgingReportRowDto>>> Handle(
        GetSupplierAgingReportQuery request,
        CancellationToken cancellationToken)
    {
        var invoices = await _supplierInvoiceRepository.GetAllAsync(cancellationToken);
        var open = invoices.Where(i => i.Status != SupplierInvoiceStatus.Cancelled && i.Status != SupplierInvoiceStatus.Paid).ToList();
        if (open.Count == 0)
            return Result.Success<IReadOnlyList<AgingReportRowDto>>(Array.Empty<AgingReportRowDto>());

        var paidMap = await _supplierPaymentRepository.GetTotalPaidBySupplierInvoiceIdsAsync(open.Select(i => i.Id), cancellationToken);

        var today = DateTime.UtcNow.Date;
        var fromCa = today.AddDays(-365);
        var caInvoices = invoices.Where(i =>
            (i.Status is SupplierInvoiceStatus.Pending or SupplierInvoiceStatus.PartiallyPaid or SupplierInvoiceStatus.Paid)
            && i.InvoiceDate >= fromCa).ToList();
        var ca = caInvoices.Sum(i => i.SubTotal.Amount);
        var daysPeriod = Math.Max(1, (today - fromCa).Days);

        var bySupplier = open.GroupBy(i => new { i.SupplierId, Name = i.Supplier?.Name ?? "" });
        var rows = new List<AgingReportRowDto>();

        foreach (var g in bySupplier)
        {
            decimal total = 0, n0 = 0, d0 = 0, d1 = 0, d2 = 0, d3 = 0;
            foreach (var inv in g)
            {
                paidMap.TryGetValue(inv.Id, out var paid);
                var bal = inv.TotalAmount.Amount - paid;
                if (bal <= 0)
                    continue;
                total += bal;
                var late = (today - inv.DueDate.Date).Days;
                if (late <= 0)
                    n0 += bal;
                else if (late <= 30)
                    d0 += bal;
                else if (late <= 60)
                    d1 += bal;
                else if (late <= 90)
                    d2 += bal;
                else
                    d3 += bal;
            }

            if (total <= 0)
                continue;

            var sCa = caInvoices.Where(i => i.SupplierId == g.Key.SupplierId).Sum(i => i.SubTotal.Amount);
            decimal? dpo = sCa > 0 ? Math.Round(total / sCa * daysPeriod, 1) : null;

            rows.Add(new AgingReportRowDto
            {
                ThirdPartyId = g.Key.SupplierId,
                ThirdPartyName = g.Key.Name,
                Total = total,
                NotYetDue = n0,
                Days0To30 = d0,
                Days31To60 = d1,
                Days61To90 = d2,
                DaysOver90 = d3,
                DsoOrDpo = dpo
            });
        }

        return Result.Success<IReadOnlyList<AgingReportRowDto>>(rows.OrderBy(r => r.ThirdPartyName).ToList());
    }
}
