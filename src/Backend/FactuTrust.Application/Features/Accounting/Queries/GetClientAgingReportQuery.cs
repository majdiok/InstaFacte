using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetClientAgingReportQuery : IRequest<Result<IReadOnlyList<AgingReportRowDto>>>;

public sealed class GetClientAgingReportQueryHandler
    : IRequestHandler<GetClientAgingReportQuery, Result<IReadOnlyList<AgingReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;

    public GetClientAgingReportQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<AgingReportRowDto>>> Handle(
        GetClientAgingReportQuery request,
        CancellationToken cancellationToken)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);
        var open = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Paid)
            .ToList();
        if (open.Count == 0)
            return Result.Success<IReadOnlyList<AgingReportRowDto>>(Array.Empty<AgingReportRowDto>());

        var paidByInvoice = await _paymentRepository.GetTotalPaidByInvoiceIdsAsync(open.Select(i => i.Id), cancellationToken);

        var today = DateTime.UtcNow.Date;
        var fromCa = today.AddDays(-365);
        var caInvoices = invoices.Where(i =>
            (i.Status is InvoiceStatus.Validated or InvoiceStatus.Signed or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid)
            && i.IssueDate >= fromCa).ToList();
        var ca = caInvoices.Sum(i => i.SubTotal.Amount);
        var daysPeriod = Math.Max(1, (today - fromCa).Days);

        var byClient = open.GroupBy(i => new { i.ClientId, Name = i.Client?.Name ?? "" });
        var rows = new List<AgingReportRowDto>();

        foreach (var g in byClient)
        {
            decimal total = 0, n0 = 0, d0 = 0, d1 = 0, d2 = 0, d3 = 0;
            foreach (var inv in g)
            {
                paidByInvoice.TryGetValue(inv.Id, out var paid);
                var bal = inv.TotalAmount.Amount - paid;
                if (bal <= 0)
                    continue;
                total += bal;
                if (!inv.DueDate.HasValue)
                {
                    n0 += bal;
                    continue;
                }
                var late = (today - inv.DueDate.Value.Date).Days;
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

            var clientCa = caInvoices.Where(i => i.ClientId == g.Key.ClientId).Sum(i => i.SubTotal.Amount);
            decimal? dso = clientCa > 0 ? Math.Round(total / clientCa * daysPeriod, 1) : null;

            rows.Add(new AgingReportRowDto
            {
                ThirdPartyId = g.Key.ClientId,
                ThirdPartyName = g.Key.Name,
                Total = total,
                NotYetDue = n0,
                Days0To30 = d0,
                Days31To60 = d1,
                Days61To90 = d2,
                DaysOver90 = d3,
                DsoOrDpo = dso
            });
        }

        return Result.Success<IReadOnlyList<AgingReportRowDto>>(rows.OrderBy(r => r.ThirdPartyName).ToList());
    }
}
