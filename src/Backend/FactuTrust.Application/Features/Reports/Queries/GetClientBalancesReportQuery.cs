using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Solde par client — total facturé, encaissé, et ventilation par ancienneté d'échéance.
///
/// <paramref name="AsOfDate"/> fixe la « date d'observation ». <c>null</c> = aujourd'hui.
/// Elle sert de référence à la comparaison contre <c>DueDate</c> ; l'exposer permet de
/// reproduire à l'identique un état imprimé un autre jour, ce qui est le cas d'usage
/// courant en contrôle.
/// </summary>
public sealed record GetClientBalancesReportQuery(DateTime? AsOfDate = null)
    : IRequest<Result<IReadOnlyList<ClientBalanceReportRowDto>>>;

public sealed class GetClientBalancesReportQueryHandler
    : IRequestHandler<GetClientBalancesReportQuery, Result<IReadOnlyList<ClientBalanceReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;

    public GetClientBalancesReportQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<ClientBalanceReportRowDto>>> Handle(
        GetClientBalancesReportQuery request,
        CancellationToken cancellationToken)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);
        var nonCancelled = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
        if (nonCancelled.Count == 0)
            return Result.Success<IReadOnlyList<ClientBalanceReportRowDto>>(Array.Empty<ClientBalanceReportRowDto>());

        var totalPaidByInvoice = await _paymentRepository.GetTotalPaidByInvoiceIdsAsync(
            nonCancelled.Select(i => i.Id),
            cancellationToken);

        var asOf = (request.AsOfDate ?? DateTime.UtcNow).Date;

        var byClient = nonCancelled
            .GroupBy(i => new { i.ClientId, ClientName = i.Client?.Name ?? "", Currency = i.TotalAmount.Currency })
            .Select(g =>
            {
                var totalInvoiced = g.Sum(i => i.TotalAmount.Amount);
                var totalPaid = g.Sum(i => totalPaidByInvoice.TryGetValue(i.Id, out var paid) ? paid : 0m);
                var buckets = AgingBuckets.Compute(g, totalPaidByInvoice, asOf);

                return new ClientBalanceReportRowDto
                {
                    ClientId = g.Key.ClientId,
                    ClientName = g.Key.ClientName,
                    TotalInvoiced = totalInvoiced,
                    TotalPaid = totalPaid,
                    Balance = totalInvoiced - totalPaid,
                    Currency = g.Key.Currency,
                    NotDue = buckets.NotDue,
                    Bucket0To30 = buckets.B0To30,
                    Bucket31To60 = buckets.B31To60,
                    Bucket61To90 = buckets.B61To90,
                    BucketOver90 = buckets.BOver90
                };
            })
            .OrderBy(r => r.ClientName)
            .ToList();

        return Result.Success<IReadOnlyList<ClientBalanceReportRowDto>>(byClient);
    }
}
