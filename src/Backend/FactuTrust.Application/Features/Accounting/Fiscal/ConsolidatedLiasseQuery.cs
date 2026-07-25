using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

/// <summary>
/// Assemble la liasse consolidée d'un exercice : états financiers NCT + détermination du résultat fiscal
/// + tableaux annexes (amortissements, provisions). Alimente l'export « liasse complète ».
/// </summary>
public sealed record GetConsolidatedLiasseQuery(int FiscalYear) : IRequest<Result<ConsolidatedLiasseDto>>;

public sealed class GetConsolidatedLiasseQueryHandler
    : IRequestHandler<GetConsolidatedLiasseQuery, Result<ConsolidatedLiasseDto>>
{
    private static readonly (string Prefix, string Label)[] ProvisionGroups =
    {
        ("15", "Provisions pour risques et charges"),
        ("29", "Dépréciations des immobilisations"),
        ("39", "Dépréciations des stocks"),
        ("49", "Dépréciations des comptes de tiers"),
        ("59", "Dépréciations des comptes financiers")
    };

    private readonly IMediator _mediator;
    private readonly IAccountingReportingService _reporting;
    private readonly ICompanyRepository _companies;

    public GetConsolidatedLiasseQueryHandler(IMediator mediator, IAccountingReportingService reporting, ICompanyRepository companies)
    {
        _mediator = mediator;
        _reporting = reporting;
        _companies = companies;
    }

    public async Task<Result<ConsolidatedLiasseDto>> Handle(GetConsolidatedLiasseQuery request, CancellationToken cancellationToken)
    {
        var nct = await _mediator.Send(new GetNctStatementsQuery(request.FiscalYear), cancellationToken);
        if (nct.IsFailure)
            return Result.Failure<ConsolidatedLiasseDto>(nct.Error);

        var fiscal = await _mediator.Send(new GetFiscalResultDeclarationQuery(request.FiscalYear), cancellationToken);
        if (fiscal.IsFailure)
            return Result.Failure<ConsolidatedLiasseDto>(fiscal.Error);

        var amortization = await BuildAmortizationTableAsync(request.FiscalYear, cancellationToken);
        var provisions = await BuildProvisionsTableAsync(request.FiscalYear, cancellationToken);

        var company = await _companies.GetDefaultAsync(cancellationToken);

        return Result.Success(new ConsolidatedLiasseDto
        {
            FiscalYear = request.FiscalYear,
            FinancialStatements = nct.Value,
            FiscalResult = fiscal.Value,
            AmortizationTable = amortization,
            ProvisionsTable = provisions,
            CompanyName = company?.Name ?? "Société"
        });
    }

    private async Task<IReadOnlyList<FiscalTableRowDto>> BuildAmortizationTableAsync(int fiscalYear, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetCurrentYearAmortizationTableQuery(1, 1000, fiscalYear, null, null, null), ct);
        if (result.IsFailure)
            return Array.Empty<FiscalTableRowDto>();

        return result.Value.Items
            .Select(a => new FiscalTableRowDto
            {
                Code = a.InventoryNumber,
                Label = a.Label,
                Amount = a.DotationComptabiliseeExercice,
                PreviousAmount = a.NetBookValue
            })
            .ToList();
    }

    private async Task<IReadOnlyList<FiscalTableRowDto>> BuildProvisionsTableAsync(int fiscalYear, CancellationToken ct)
    {
        var current = await _reporting.GetBalanceAsync(new DateTime(fiscalYear, 1, 1), new DateTime(fiscalYear, 12, 31), ct);
        if (current.IsFailure)
            return Array.Empty<FiscalTableRowDto>();

        var previous = await _reporting.GetBalanceAsync(new DateTime(fiscalYear - 1, 1, 1), new DateTime(fiscalYear - 1, 12, 31), ct);
        var previousRows = previous.IsSuccess ? previous.Value : Array.Empty<BalanceRowDto>();

        decimal Net(IReadOnlyList<BalanceRowDto> rows, string prefix) =>
            rows.Where(r => r.AccountNumber.StartsWith(prefix)).Sum(r => r.ClosingCredit - r.ClosingDebit);

        var table = new List<FiscalTableRowDto>();
        foreach (var g in ProvisionGroups)
        {
            var amount = Net(current.Value, g.Prefix);
            var prev = Net(previousRows, g.Prefix);
            if (amount == 0m && prev == 0m)
                continue;
            table.Add(new FiscalTableRowDto { Code = g.Prefix, Label = g.Label, Amount = amount, PreviousAmount = prev });
        }
        return table;
    }
}
