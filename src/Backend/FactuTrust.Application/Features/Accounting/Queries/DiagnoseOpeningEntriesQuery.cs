using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Diagnostic des écritures d'à-nouveaux (« JAN ») actives : recalcule les soldes de clôture
/// ancrés attendus pour chaque exercice clôturé et compare, ligne à ligne, avec les lignes
/// effectivement portées par l'écriture <c>SourceOpeningBalance</c> correspondante. Sert de base
/// à <see cref="Commands.RepairOpeningEntriesCommand"/> et à l'écran de contrôles de pré-clôture.
/// </summary>
public sealed record DiagnoseOpeningEntriesQuery : IRequest<Result<IReadOnlyList<OpeningEntryDiagnosticDto>>>;

public sealed class DiagnoseOpeningEntriesQueryHandler
    : IRequestHandler<DiagnoseOpeningEntriesQuery, Result<IReadOnlyList<OpeningEntryDiagnosticDto>>>
{
    private readonly IAccountingService _accountingService;

    public DiagnoseOpeningEntriesQueryHandler(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    public Task<Result<IReadOnlyList<OpeningEntryDiagnosticDto>>> Handle(
        DiagnoseOpeningEntriesQuery request, CancellationToken cancellationToken)
        => _accountingService.DiagnoseOpeningEntriesAsync(cancellationToken);
}
