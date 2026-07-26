using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

/// <summary>
/// Livre d'inventaire d'un exercice : compose la liasse consolidée, la balance de clôture et un
/// tableau de provisions détaillé par compte. Aucune requête ni donnée propre — assemblage d'états
/// existants, donc figé par construction dès que l'exercice est verrouillé.
/// </summary>
public sealed record GetInventoryBookQuery(int FiscalYear) : IRequest<Result<InventoryBookDto>>;

public sealed class GetInventoryBookQueryHandler : IRequestHandler<GetInventoryBookQuery, Result<InventoryBookDto>>
{
    // Mêmes racines de provisions que la liasse consolidée (ConsolidatedLiasseQuery.ProvisionGroups) —
    // ici ventilées compte par compte au lieu d'un total par groupe.
    private static readonly string[] ProvisionPrefixes = { "15", "29", "39", "49", "59" };

    private readonly IMediator _mediator;
    private readonly IAccountingReportingService _reporting;
    private readonly IFiscalYearLockService _lockService;

    public GetInventoryBookQueryHandler(
        IMediator mediator,
        IAccountingReportingService reporting,
        IFiscalYearLockService lockService)
    {
        _mediator = mediator;
        _reporting = reporting;
        _lockService = lockService;
    }

    public async Task<Result<InventoryBookDto>> Handle(GetInventoryBookQuery request, CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<InventoryBookDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var liasse = await _mediator.Send(new GetConsolidatedLiasseQuery(request.FiscalYear), cancellationToken);
        if (liasse.IsFailure)
            return Result.Failure<InventoryBookDto>(liasse.Error);

        var from = new DateTime(request.FiscalYear, 1, 1);
        var to = new DateTime(request.FiscalYear, 12, 31);
        var balance = await _reporting.GetBalanceAsync(from, to, cancellationToken);
        if (balance.IsFailure)
            return Result.Failure<InventoryBookDto>(balance.Error);

        // Provisions détaillées : une ligne par compte des racines de provision, montant = solde
        // créditeur net. La somme par racine égale le total du groupe de la liasse (même source).
        var detailedProvisions = balance.Value
            .Where(r => ProvisionPrefixes.Any(p => r.AccountNumber.StartsWith(p, StringComparison.Ordinal)))
            .Select(r => new FiscalTableRowDto
            {
                Code = r.AccountNumber,
                Label = r.Label,
                Amount = r.ClosingCredit - r.ClosingDebit
            })
            .Where(r => r.Amount != 0m)
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();

        // Statut de verrouillage (best-effort : l'indisponibilité du verrou ne bloque pas l'édition).
        bool isLocked = false;
        DateTime? lockedAt = null;
        var locks = await _lockService.GetLocksAsync(cancellationToken);
        if (locks.IsSuccess)
        {
            var match = locks.Value.FirstOrDefault(l => l.FiscalYear == request.FiscalYear);
            if (match is not null)
            {
                isLocked = true;
                lockedAt = match.LockedAt;
            }
        }

        return Result.Success(new InventoryBookDto
        {
            FiscalYear = request.FiscalYear,
            CompanyName = liasse.Value.CompanyName,
            Liasse = liasse.Value,
            ClosingBalance = balance.Value,
            DetailedProvisions = detailedProvisions,
            IsYearLocked = isLocked,
            LockedAt = lockedAt
        });
    }
}
