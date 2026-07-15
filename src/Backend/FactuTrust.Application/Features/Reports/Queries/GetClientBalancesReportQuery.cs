using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get client balances (solde = total facturé - total payé) for all clients.
/// </summary>
public sealed record GetClientBalancesReportQuery
    : IRequest<Result<IReadOnlyList<ClientBalanceReportRowDto>>>;

/// <summary>
/// Handler for GetClientBalancesReportQuery.
/// </summary>
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

        var byClient = nonCancelled
            .GroupBy(i => new { i.ClientId, ClientName = i.Client?.Name ?? "", Currency = i.TotalAmount.Currency })
            .Select(g =>
            {
                var totalInvoiced = g.Sum(i => i.TotalAmount.Amount);
                var totalPaid = g.Sum(i => totalPaidByInvoice.TryGetValue(i.Id, out var paid) ? paid : 0m);
                return new ClientBalanceReportRowDto
                {
                    ClientId = g.Key.ClientId,
                    ClientName = g.Key.ClientName,
                    TotalInvoiced = totalInvoiced,
                    TotalPaid = totalPaid,
                    Balance = totalInvoiced - totalPaid,
                    Currency = g.Key.Currency
                };
            })
            .OrderBy(r => r.ClientName)
            .ToList();

        return Result.Success<IReadOnlyList<ClientBalanceReportRowDto>>(byClient);
    }
}
