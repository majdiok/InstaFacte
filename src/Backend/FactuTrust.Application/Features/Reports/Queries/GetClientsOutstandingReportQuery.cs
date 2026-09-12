using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Encours commercial agrégé par client : factures impayées + commandes confirmées non facturées.
/// Alimente l'état déterministe <c>encours_commercial_clients</c> du Studio IA.
/// </summary>
public sealed record GetClientsOutstandingReportQuery : IRequest<Result<IReadOnlyList<ClientOutstandingReportRowDto>>>;

public sealed class GetClientsOutstandingReportQueryHandler
    : IRequestHandler<GetClientsOutstandingReportQuery, Result<IReadOnlyList<ClientOutstandingReportRowDto>>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IClientOutstandingService _outstandingService;

    public GetClientsOutstandingReportQueryHandler(
        IClientRepository clientRepository,
        IClientOutstandingService outstandingService)
    {
        _clientRepository = clientRepository;
        _outstandingService = outstandingService;
    }

    public async Task<Result<IReadOnlyList<ClientOutstandingReportRowDto>>> Handle(
        GetClientsOutstandingReportQuery request,
        CancellationToken cancellationToken)
    {
        var clients = await _clientRepository.GetActiveClientsAsync(cancellationToken);
        if (clients.Count == 0)
            return Result.Success<IReadOnlyList<ClientOutstandingReportRowDto>>(Array.Empty<ClientOutstandingReportRowDto>());

        var rows = new List<ClientOutstandingReportRowDto>();
        foreach (var client in clients)
        {
            var outstanding = await _outstandingService.GetOutstandingAsync(client.Id, cancellationToken);
            if (!outstanding.IsSuccess || outstanding.Value.TotalOutstanding <= 0)
                continue;

            var dto = outstanding.Value;
            rows.Add(new ClientOutstandingReportRowDto
            {
                ClientId = dto.ClientId,
                ClientName = dto.ClientName,
                UnpaidInvoicesAmount = dto.UnpaidInvoicesAmount,
                ConfirmedOrdersAmount = dto.ConfirmedOrdersAmount,
                TotalOutstanding = dto.TotalOutstanding,
                CreditLimit = dto.CreditLimit,
                AvailableCredit = dto.AvailableCredit,
                IsOverLimit = dto.IsOverLimit,
                UnpaidInvoiceCount = dto.UnpaidInvoiceCount,
                OverdueAmount = dto.OverdueAmount,
                Currency = dto.Currency
            });
        }

        return Result.Success<IReadOnlyList<ClientOutstandingReportRowDto>>(
            rows.OrderBy(r => r.ClientName).ToList());
    }
}
