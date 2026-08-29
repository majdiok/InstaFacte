using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Fiscal;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

// ── Lecture des paramètres fiscaux d'un exercice ─────────────────────────────────────────

public sealed record GetIncomeTaxParametersQuery(int FiscalYear) : IRequest<Result<IncomeTaxYearParameterDto>>;

public sealed class GetIncomeTaxParametersQueryHandler
    : IRequestHandler<GetIncomeTaxParametersQuery, Result<IncomeTaxYearParameterDto>>
{
    private readonly IIncomeTaxYearParameterRepository _parameters;

    public GetIncomeTaxParametersQueryHandler(IIncomeTaxYearParameterRepository parameters) => _parameters = parameters;

    public async Task<Result<IncomeTaxYearParameterDto>> Handle(GetIncomeTaxParametersQuery request, CancellationToken cancellationToken)
    {
        if (request.FiscalYear < 2000 || request.FiscalYear > 2100)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var p = await _parameters.GetOrDefaultAsync(request.FiscalYear, cancellationToken);
        return Result.Success(Map(p));
    }

    internal static IncomeTaxYearParameterDto Map(IncomeTaxYearParameter p) => new()
    {
        FiscalYear = p.FiscalYear,
        IsStandardRate = p.IsStandardRate,
        IsReducedRate = p.IsReducedRate,
        IsSectorRate = p.IsSectorRate,
        MinTaxRate = p.MinTaxRate,
        MinTaxReducedRate = p.MinTaxReducedRate,
        MinTaxFloorTnd = p.MinTaxFloorTnd,
        MinTaxFloorReducedTnd = p.MinTaxFloorReducedTnd,
        CssApplies = p.CssApplies,
        CssRate = p.CssRate,
        CssFloorTnd = p.CssFloorTnd,
        AcompteRate = p.AcompteRate,
        AcompteCount = p.AcompteCount,
        DeficitCarryForwardYears = p.DeficitCarryForwardYears,
        RoundTaxableToDinar = p.RoundTaxableToDinar,
        IrppBracketsJson = p.IrppBracketsJson,
        IsUserModified = p.IsUserModified
    };
}

// ── Enregistrement des paramètres fiscaux d'un exercice ──────────────────────────────────

public sealed record UpdateIncomeTaxParametersCommand(int FiscalYear, IncomeTaxYearParameterDto Parameters)
    : IRequest<Result<IncomeTaxYearParameterDto>>;

public sealed class UpdateIncomeTaxParametersCommandHandler
    : IRequestHandler<UpdateIncomeTaxParametersCommand, Result<IncomeTaxYearParameterDto>>
{
    private readonly IIncomeTaxYearParameterRepository _parameters;

    public UpdateIncomeTaxParametersCommandHandler(IIncomeTaxYearParameterRepository parameters) => _parameters = parameters;

    public async Task<Result<IncomeTaxYearParameterDto>> Handle(UpdateIncomeTaxParametersCommand command, CancellationToken cancellationToken)
    {
        if (command.FiscalYear < 2000 || command.FiscalYear > 2100)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var d = command.Parameters;
        if (d is null)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("Parameters", "Paramètres manquants."));

        // Garde-fous : des taux exprimés en fraction (0,15 = 15 %) et des planchers non négatifs.
        if (IsInvalidRate(d.IsStandardRate) || IsInvalidRate(d.IsReducedRate) || IsInvalidRate(d.IsSectorRate)
            || IsInvalidRate(d.MinTaxRate) || IsInvalidRate(d.MinTaxReducedRate)
            || IsInvalidRate(d.CssRate) || IsInvalidRate(d.AcompteRate))
        {
            return Result.Failure<IncomeTaxYearParameterDto>(
                Error.Validation("Rates", "Les taux doivent être exprimés en fraction entre 0 et 1 (ex. 0,15 pour 15 %)."));
        }

        if (d.MinTaxFloorTnd < 0 || d.MinTaxFloorReducedTnd < 0 || d.CssFloorTnd < 0)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("Floors", "Les planchers ne peuvent pas être négatifs."));

        // T13 — bornes de bon sens : le nombre d'acomptes provisionnels est compris entre 0 et 12
        // (au plus mensuel), la durée de report des déficits entre 1 et 20 exercices.
        if (d.AcompteCount < 0 || d.AcompteCount > 12)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("AcompteCount",
                "Le nombre d'acomptes provisionnels doit être compris entre 0 et 12."));
        if (d.DeficitCarryForwardYears < 1 || d.DeficitCarryForwardYears > 20)
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("DeficitCarryForwardYears",
                "La durée de report des déficits doit être comprise entre 1 et 20 exercices."));

        // T13 — barème IRPP strict (borne ≥ 0, strictement croissant, taux ∈ [0;100]) : la saisie est
        // rejetée AVANT persistance avec un message précis. Le « Parse » tolérant reste réservé à la
        // lecture des données historiques (le moteur ne doit pas planter sur une ligne ancienne).
        if (!IrppScale.TryParseStrict(d.IrppBracketsJson, out _, out var irppError))
            return Result.Failure<IncomeTaxYearParameterDto>(Error.Validation("IrppBracketsJson", irppError ?? "Barème IRPP invalide ou vide."));

        var upsert = new IncomeTaxParameterUpsert(
            d.IsStandardRate, d.IsReducedRate, d.IsSectorRate,
            d.MinTaxRate, d.MinTaxReducedRate, d.MinTaxFloorTnd, d.MinTaxFloorReducedTnd,
            d.CssApplies, d.CssRate, d.CssFloorTnd,
            d.AcompteRate, d.AcompteCount, d.DeficitCarryForwardYears,
            d.RoundTaxableToDinar, d.IrppBracketsJson);

        var saved = await _parameters.UpsertAsync(command.FiscalYear, upsert, cancellationToken);
        return Result.Success(GetIncomeTaxParametersQueryHandler.Map(saved));
    }

    private static bool IsInvalidRate(decimal rate) => rate < 0m || rate > 1m;
}
