using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

// ── Requête d'upsert (payload API) ───────────────────────────────────────────────────────

public sealed record FiscalAdjustmentLineInput(int Kind, string? CatalogCode, string Label, decimal Amount, bool IsAutoSuggested);
public sealed record FiscalCarryForwardInput(int Kind, int OriginYear, decimal InitialAmount, decimal ImputedThisYear, int? ExpiryYear);

public sealed record UpsertFiscalResultRequest(
    int TaxpayerKind,
    decimal AccountingResult,
    decimal AppliedIsRate,
    decimal LocalTurnoverTtc,
    decimal AcomptesPaid,
    decimal WithholdingSuffered,
    decimal PriorTaxCredit,
    IReadOnlyList<FiscalAdjustmentLineInput> Adjustments,
    IReadOnlyList<FiscalCarryForwardInput> CarryForwards,
    /// <summary>Régime de minimum d'impôt : 0 = droit commun, 1 = réduit, 2 = exonéré.</summary>
    int MinimumTaxRegime = 0);

// ── Upsert (brouillon) ───────────────────────────────────────────────────────────────────

public sealed record UpsertFiscalResultDeclarationCommand(int FiscalYear, UpsertFiscalResultRequest Request)
    : IRequest<Result<FiscalResultDeclarationDto>>;

public sealed class UpsertFiscalResultDeclarationCommandHandler
    : IRequestHandler<UpsertFiscalResultDeclarationCommand, Result<FiscalResultDeclarationDto>>
{
    private readonly IFiscalResultDeclarationRepository _declarations;
    private readonly IIncomeTaxYearParameterRepository _parameters;
    private readonly AccountingSettings _settings;

    public UpsertFiscalResultDeclarationCommandHandler(
        IFiscalResultDeclarationRepository declarations,
        IIncomeTaxYearParameterRepository parameters,
        IOptions<AccountingSettings> settings)
    {
        _declarations = declarations;
        _parameters = parameters;
        _settings = settings.Value;
    }

    public async Task<Result<FiscalResultDeclarationDto>> Handle(UpsertFiscalResultDeclarationCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        if (!Enum.IsDefined(typeof(TaxpayerKind), r.TaxpayerKind))
            return Result.Failure<FiscalResultDeclarationDto>(Error.Validation("TaxpayerKind", "Type de contribuable invalide."));
        if (!Enum.IsDefined(typeof(MinimumTaxRegime), r.MinimumTaxRegime))
            return Result.Failure<FiscalResultDeclarationDto>(Error.Validation("MinimumTaxRegime", "Régime de minimum d'impôt invalide."));

        // Les paramètres d'exercice sont requis en amont : la durée de report des déficits sert à
        // déduire l'échéance d'imputation des déficits non datés.
        var parameters = await _parameters.GetOrDefaultAsync(command.FiscalYear, cancellationToken);

        var adjustments = (r.Adjustments ?? Array.Empty<FiscalAdjustmentLineInput>())
            .Select(a => FiscalAdjustmentLine.Create(
                (FiscalAdjustmentKind)a.Kind, a.CatalogCode, string.IsNullOrWhiteSpace(a.Label) ? "(sans libellé)" : a.Label, a.Amount, a.IsAutoSuggested))
            .ToList();

        var carryForwards = (r.CarryForwards ?? Array.Empty<FiscalCarryForwardInput>())
            .Select(c => FiscalCarryForwardItem.Create(
                (FiscalCarryForwardKind)c.Kind, c.OriginYear, c.InitialAmount, c.ImputedThisYear, c.ExpiryYear,
                parameters.DeficitCarryForwardYears))
            .ToList();

        var upsert = new FiscalDeclarationUpsert(
            command.FiscalYear,
            (TaxpayerKind)r.TaxpayerKind,
            r.AccountingResult,
            r.AppliedIsRate,
            r.LocalTurnoverTtc,
            r.AcomptesPaid,
            r.WithholdingSuffered,
            r.PriorTaxCredit,
            adjustments,
            carryForwards,
            (MinimumTaxRegime)r.MinimumTaxRegime);

        FiscalResultDeclaration entity;
        try
        {
            entity = await _declarations.UpsertAsync(upsert, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Feuille déjà finalisée (non modifiable).
            return Result.Failure<FiscalResultDeclarationDto>(Error.Validation("Status", ex.Message));
        }

        // Le CA suggéré n'est pas recalculé ici (il l'est au chargement) : la valeur enregistrée fait foi.
        return Result.Success(FiscalResultAssembler.Assemble(entity, parameters, _settings.FiscalLiasseEnabled));
    }
}

// ── Finalisation ─────────────────────────────────────────────────────────────────────────

public sealed record FinalizeFiscalResultDeclarationCommand(int FiscalYear) : IRequest<Result>;

public sealed class FinalizeFiscalResultDeclarationCommandHandler
    : IRequestHandler<FinalizeFiscalResultDeclarationCommand, Result>
{
    private readonly IFiscalResultDeclarationRepository _declarations;
    private readonly ICurrentUser _currentUser;

    public FinalizeFiscalResultDeclarationCommandHandler(
        IFiscalResultDeclarationRepository declarations,
        ICurrentUser currentUser)
    {
        _declarations = declarations;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(FinalizeFiscalResultDeclarationCommand command, CancellationToken cancellationToken)
    {
        // Défense en profondeur : la finalisation est réservée au cabinet en mode dossier client délégué
        // (le contrôleur applique déjà la policy FirmDelegatedContext).
        if (!_currentUser.IsAccountingFirmDelegatedContext)
            return Result.Failure(Error.Forbidden(AccountingValidationAccess.DeniedMessage));

        var userId = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system";
        var ok = await _declarations.FinalizeAsync(command.FiscalYear, userId, cancellationToken);
        return ok
            ? Result.Success()
            : Result.Failure(Error.Validation("Fiscal", "Aucune feuille de détermination du résultat fiscal à finaliser pour cet exercice."));
    }
}
