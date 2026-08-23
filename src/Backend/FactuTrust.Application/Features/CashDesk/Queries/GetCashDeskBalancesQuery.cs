using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.CashDesk.Queries;

/// <summary>
/// Query to get cash desk balances for a selected month.
/// Solde primaire = month totals, Solde secondaire = YTD totals.
/// Now computes real balance = credits - debits per payment method.
/// </summary>
public sealed record GetCashDeskBalancesQuery(int Year, int Month) : IRequest<CashDeskBalancesDto>;

public sealed class GetCashDeskBalancesQueryHandler
    : IRequestHandler<GetCashDeskBalancesQuery, CashDeskBalancesDto>
{
    private readonly ICashOperationRepository _cashOperationRepository;

    public GetCashDeskBalancesQueryHandler(ICashOperationRepository cashOperationRepository)
    {
        _cashOperationRepository = cashOperationRepository;
    }

    public async Task<CashDeskBalancesDto> Handle(
        GetCashDeskBalancesQuery request,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var ytdStart = new DateTime(request.Year, 1, 1);
        var ytdEnd = monthEnd;

        var primaryTotals = await _cashOperationRepository.GetNonCancelledTotalsByMethodAndTypeAsync(
            monthStart, monthEnd, cancellationToken);

        var secondaryTotals = await _cashOperationRepository.GetNonCancelledTotalsByMethodAndTypeAsync(
            ytdStart, ytdEnd, cancellationToken);

        // La traite n'est pas un instrument de caisse (elle route vers 413/403, jamais vers une
        // opération de caisse) : on l'exclut des soldes de trésorerie pour ne pas afficher une ligne
        // systématiquement nulle.
        var methods = Enum.GetValues<PaymentMethod>().Where(m => m != PaymentMethod.Traite).ToArray();

        var primaryRows = methods.Select(m =>
        {
            primaryTotals.TryGetValue((m, CashOperationType.Credit), out var credits);
            primaryTotals.TryGetValue((m, CashOperationType.Debit), out var debits);
            return new CashDeskBalanceRowDto
            {
                Method = m,
                MethodDisplay = m.ToDisplayString(),
                Amount = credits - debits,
                Credits = credits,
                Debits = debits
            };
        }).ToList();

        var secondaryRows = methods.Select(m =>
        {
            secondaryTotals.TryGetValue((m, CashOperationType.Credit), out var credits);
            secondaryTotals.TryGetValue((m, CashOperationType.Debit), out var debits);
            return new CashDeskBalanceRowDto
            {
                Method = m,
                MethodDisplay = m.ToDisplayString(),
                Amount = credits - debits,
                Credits = credits,
                Debits = debits
            };
        }).ToList();

        return new CashDeskBalancesDto
        {
            Currency = Money.DefaultCurrency,
            Primary = primaryRows,
            Secondary = secondaryRows,
            PrimaryTotal = primaryRows.Sum(r => r.Amount),
            SecondaryTotal = secondaryRows.Sum(r => r.Amount),
            PrimaryCreditsTotal = primaryRows.Sum(r => r.Credits),
            PrimaryDebitsTotal = primaryRows.Sum(r => r.Debits),
            SecondaryCreditsTotal = secondaryRows.Sum(r => r.Credits),
            SecondaryDebitsTotal = secondaryRows.Sum(r => r.Debits)
        };
    }
}
