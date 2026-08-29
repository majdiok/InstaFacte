using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

// ── Catalogue des lignes standard ──────────────────────────────────────────────────────

public sealed record GetFiscalAdjustmentCatalogQuery : IRequest<Result<IReadOnlyList<FiscalAdjustmentCatalogEntryDto>>>;

public sealed class GetFiscalAdjustmentCatalogQueryHandler
    : IRequestHandler<GetFiscalAdjustmentCatalogQuery, Result<IReadOnlyList<FiscalAdjustmentCatalogEntryDto>>>
{
    public Task<Result<IReadOnlyList<FiscalAdjustmentCatalogEntryDto>>> Handle(GetFiscalAdjustmentCatalogQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<FiscalAdjustmentCatalogEntryDto> items = FiscalAdjustmentCatalog.All
            .Select(e => new FiscalAdjustmentCatalogEntryDto
            {
                Code = e.Code,
                Kind = (int)e.Kind,
                Label = e.Label,
                Hint = e.Hint
            })
            .ToList();

        return Task.FromResult(Result.Success(items));
    }
}

// ── Feuille de détermination du résultat fiscal (avec calcul + suggestions auto) ─────────

public sealed record GetFiscalResultDeclarationQuery(int FiscalYear) : IRequest<Result<FiscalResultDeclarationDto>>;

public sealed class GetFiscalResultDeclarationQueryHandler
    : IRequestHandler<GetFiscalResultDeclarationQuery, Result<FiscalResultDeclarationDto>>
{
    private readonly IFiscalResultDeclarationRepository _declarations;
    private readonly IIncomeTaxYearParameterRepository _parameters;
    private readonly IAccountingReportingService _reporting;
    private readonly AccountingSettings _settings;

    public GetFiscalResultDeclarationQueryHandler(
        IFiscalResultDeclarationRepository declarations,
        IIncomeTaxYearParameterRepository parameters,
        IAccountingReportingService reporting,
        IOptions<AccountingSettings> settings)
    {
        _declarations = declarations;
        _parameters = parameters;
        _reporting = reporting;
        _settings = settings.Value;
    }

    public async Task<Result<FiscalResultDeclarationDto>> Handle(GetFiscalResultDeclarationQuery request, CancellationToken cancellationToken)
    {
        var parameters = await _parameters.GetOrDefaultAsync(request.FiscalYear, cancellationToken);
        var existing = await _declarations.GetByYearAsync(request.FiscalYear, cancellationToken);

        // CA local TTC repris de la comptabilité : assiette du minimum d'impôt, proposé en aide à la
        // saisie (la valeur retenue par le comptable prime — ex. exclusion du chiffre d'affaires export).
        var suggestedTurnover = await TryComputeLocalTurnoverTtcAsync(request.FiscalYear, cancellationToken);
        // T11 — résultat comptable net (après impôt) repris de l'état de résultat, proposé à côté du
        // champ saisi (la valeur saisie n'est jamais écrasée). Calculé aussi pour les feuilles existantes.
        var suggestedAccountingResult = await TryComputeSuggestedAccountingResultAsync(request.FiscalYear, cancellationToken);

        if (existing is not null)
            return Result.Success(FiscalResultAssembler.Assemble(
                existing, parameters, _settings.FiscalLiasseEnabled, suggestedTurnover, suggestedAccountingResult));

        // Aucune feuille : aperçu initial à partir du résultat comptable net + suggestions auto certaines.
        var nct = await _reporting.GetNctStatementsAsync(request.FiscalYear, cancellationToken);
        if (nct.IsFailure)
            return Result.Failure<FiscalResultDeclarationDto>(nct.Error);

        var netResult = nct.Value.IncomeStatement.NetResult;
        var isCharge = Math.Max(nct.Value.IncomeStatement.ResultBeforeTax - netResult, 0m);

        var suggestions = new List<FiscalAdjustmentLineDto>();
        if (isCharge > 0m)
            suggestions.Add(new FiscalAdjustmentLineDto
            {
                CatalogCode = "R-IS",
                Kind = (int)FiscalAdjustmentKind.Reintegration,
                Label = "Impôt sur les sociétés (compte 69)",
                Amount = Math.Round(isCharge, 3),
                IsAutoSuggested = true
            });

        var penalties = await TryComputePenaltiesAsync(request.FiscalYear, cancellationToken);
        if (penalties > 0m)
            suggestions.Add(new FiscalAdjustmentLineDto
            {
                CatalogCode = "R-PENALITES",
                Kind = (int)FiscalAdjustmentKind.Reintegration,
                Label = "Amendes, pénalités et majorations de retard",
                Amount = Math.Round(penalties, 3),
                IsAutoSuggested = true
            });

        var dto = FiscalResultAssembler.AssembleNew(
            request.FiscalYear, netResult, suggestions, parameters, _settings.FiscalLiasseEnabled, suggestedTurnover);
        return Result.Success(dto);
    }

    /// <summary>
    /// Chiffre d'affaires local TTC de l'exercice = produits d'exploitation (classe 70, crédit − débit)
    /// + TVA collectée (comptes 4367x). Assiette du minimum d'impôt (0,2 % / 0,1 % selon le régime).
    /// Indicatif : le comptable ajuste si une partie du chiffre d'affaires est à l'export.
    /// </summary>
    private async Task<decimal> TryComputeLocalTurnoverTtcAsync(int fiscalYear, CancellationToken ct)
    {
        var from = new DateTime(fiscalYear, 1, 1);
        var to = new DateTime(fiscalYear, 12, 31);
        var balance = await _reporting.GetBalanceAsync(from, to, ct);
        return balance.IsFailure ? 0m : FiscalResultAssembler.EstimateLocalTurnoverTtc(balance.Value);
    }

    /// <summary>
    /// Résultat comptable <b>net (après impôt)</b> de l'exercice, repris de l'état de résultat NCT
    /// (T11). Proposé comme aide à la saisie du champ « résultat comptable » — la valeur saisie par le
    /// comptable prime et n'est jamais écrasée. Null si les états ne sont pas disponibles.
    /// </summary>
    private async Task<decimal?> TryComputeSuggestedAccountingResultAsync(int fiscalYear, CancellationToken ct)
    {
        var nct = await _reporting.GetNctStatementsAsync(fiscalYear, ct);
        return nct.IsSuccess ? nct.Value.IncomeStatement.NetResult : null;
    }

    /// <summary>
    /// Suggestion R-PENALITES : somme des mouvements débiteurs des pénalités et majorations de retard
    /// sur l'exercice, pour réintégration non déductible.
    /// </summary>
    /// <remarks>
    /// Lit DEUX racines de comptes :
    /// <list type="bullet">
    /// <item><c>668</c> — autres charges non déductibles (amendes, pénalités comptabilisées au compte
    /// standard du plan SCE).</item>
    /// <item><c>6712</c> — pénalités et majorations de retard fiscales. Ce compte N'EXISTE PAS au
    /// catalogue livré (§1.4) : il s'agit d'un SOUS-COMPTE potentiel créé au gré des tenants. Un
    /// tenant sans <c>6712</c> ne lève JAMAIS d'exception ici : la balance ne retourne simplement aucune
    /// ligne pour ce préfixe et la suggestion se réduit aux débits <c>668</c> seuls. C'est le
    /// comportement de repli documenté et testé (T17) : aucune charge de pénalité fiscale
    /// spécifique <c>6712</c> n'est détectée → suggestion = débits <c>668</c> uniquement, sans erreur.
    /// </list>
    /// </remarks>
    private async Task<decimal> TryComputePenaltiesAsync(int fiscalYear, CancellationToken ct)
    {
        var from = new DateTime(fiscalYear, 1, 1);
        var to = new DateTime(fiscalYear, 12, 31);
        var balance = await _reporting.GetBalanceAsync(from, to, ct);
        if (balance.IsFailure)
            return 0m;

        decimal sum = 0m;
        foreach (var row in balance.Value)
        {
            if (row.AccountNumber.StartsWith("6712") || row.AccountNumber.StartsWith("668"))
                sum += row.MovementDebit - row.MovementCredit;
        }
        return Math.Max(sum, 0m);
    }
}
