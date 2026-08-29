using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
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

/// <summary>
/// Élément reportable saisi (déficit ou amortissement différé).
/// ATTENTION (T12) : <see cref="ExpiryYear"/> est IGNORE du client — l'échéance est DÉRIVÉE côté
/// serveur (déficit → OriginYear + DeficitCarryForwardYears ; amortissement différé → null, illimité).
/// Le champ n'est conservé que pour la compatibilité du modèle d'entrée ; sa valeur n'est jamais persistée.
/// </summary>
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

        // ── T12 — validations des reports AVANT persistance (Error.Validation → 400, sans écriture) ──
        var carryInputs = r.CarryForwards ?? Array.Empty<FiscalCarryForwardInput>();
        foreach (var c in carryInputs)
        {
            if (!Enum.IsDefined(typeof(FiscalCarryForwardKind), c.Kind))
                return Result.Failure<FiscalResultDeclarationDto>(
                    Error.Validation("CarryForwards", "Type de report invalide (déficit ou amortissement différé)."));

            if (c.OriginYear >= command.FiscalYear || c.OriginYear < command.FiscalYear - 100)
                return Result.Failure<FiscalResultDeclarationDto>(
                    Error.Validation("CarryForwards",
                        $"L'année d'origine du report ({c.OriginYear}) doit être antérieure à l'exercice {command.FiscalYear} et postérieure à {command.FiscalYear - 100}."));

            if (c.InitialAmount < 0m)
                return Result.Failure<FiscalResultDeclarationDto>(
                    Error.Validation("CarryForwards", "Le montant initial reportable ne peut pas être négatif."));

            if (c.ImputedThisYear < 0m)
                return Result.Failure<FiscalResultDeclarationDto>(
                    Error.Validation("CarryForwards", "Le montant imputé sur l'exercice ne peut pas être négatif."));

            if (c.ImputedThisYear > c.InitialAmount)
                return Result.Failure<FiscalResultDeclarationDto>(
                    Error.Validation("CarryForwards",
                        $"Report d'origine {c.OriginYear} : l'imputation ({c.ImputedThisYear:N3}) ne peut pas dépasser le stock reportable ({c.InitialAmount:N3})."));

            // ExpiryYear DÉRIVÉ côté serveur, jamais lu du client (documenté dans FiscalCarryForwardInput).
            // Déficit ordinaire périmé imputé → rejet (ExpiryYear dérivé < exercice et imputation > 0).
            var isDeficit = c.Kind == (int)FiscalCarryForwardKind.Deficit;
            if (isDeficit && c.ImputedThisYear > 0m)
            {
                var derivedExpiry = c.OriginYear + parameters.DeficitCarryForwardYears;
                if (derivedExpiry < command.FiscalYear)
                    return Result.Failure<FiscalResultDeclarationDto>(
                        Error.Validation("CarryForwards",
                            $"Déficit ordinaire d'origine {c.OriginYear} prescrit (imputable jusqu'à {derivedExpiry}) : non imputable sur l'exercice {command.FiscalYear}."));
            }
        }

        var adjustments = (r.Adjustments ?? Array.Empty<FiscalAdjustmentLineInput>())
            .Select(a => FiscalAdjustmentLine.Create(
                (FiscalAdjustmentKind)a.Kind, a.CatalogCode, string.IsNullOrWhiteSpace(a.Label) ? "(sans libellé)" : a.Label, a.Amount, a.IsAutoSuggested))
            .ToList();

        // ExpiryYear ignoré du client (null) : dérivation serveur dans FiscalCarryForwardItem.Create
        // (déficit → OriginYear + DeficitCarryForwardYears ; amortissement différé → null, illimité).
        var carryForwards = carryInputs
            .Select(c => FiscalCarryForwardItem.Create(
                (FiscalCarryForwardKind)c.Kind, c.OriginYear, c.InitialAmount, c.ImputedThisYear, expiryYear: null,
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

/// <summary>Code catalogue de la réintégration de l'IS comptabilisé (compte 69), aligné sur FiscalAdjustmentCatalog.</summary>
internal static class FiscalFinalizationCatalogCodes
{
    public const string IncomeTaxReintegration = "R-IS";
}

public sealed class FinalizeFiscalResultDeclarationCommandHandler
    : IRequestHandler<FinalizeFiscalResultDeclarationCommand, Result>
{
    private const decimal Tolerance = 0.01m;

    private readonly IFiscalResultDeclarationRepository _declarations;
    private readonly IIncomeTaxYearParameterRepository _parameters;
    private readonly IAccountingService _accounting;
    private readonly IAccountingReportingService _reporting;
    private readonly IFiscalYearLockService _locks;
    private readonly ITenantUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public FinalizeFiscalResultDeclarationCommandHandler(
        IFiscalResultDeclarationRepository declarations,
        IIncomeTaxYearParameterRepository parameters,
        IAccountingService accounting,
        IAccountingReportingService reporting,
        IFiscalYearLockService locks,
        ITenantUnitOfWork uow,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _declarations = declarations;
        _parameters = parameters;
        _accounting = accounting;
        _reporting = reporting;
        _locks = locks;
        _uow = uow;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(FinalizeFiscalResultDeclarationCommand command, CancellationToken cancellationToken)
    {
        // Défense en profondeur : la finalisation est réservée au cabinet en mode dossier client délégué
        // (le contrôleur applique déjà la policy FirmDelegatedContext).
        if (!_currentUser.IsAccountingFirmDelegatedContext)
            return Result.Failure(Error.Forbidden(AccountingValidationAccess.DeniedMessage));

        // Tout le déroulé (écriture d'impôt + alignement de la feuille + finalisation) vit dans UNE
        // transaction ITenantUnitOfWork : commit si succès, rollback intégral sur échec (Result en
        // échec ou exception) — l'écriture d'impôt et l'alignement du perdant sont annulés.
        return await _uow.ExecuteAsync(async ct =>
        {
            var year = command.FiscalYear;

            // 2. Charger la feuille : absente → 400 ; déjà finalisée → 409.
            var existing = await _declarations.GetByYearAsync(year, ct);
            if (existing is null)
                return Result.Failure(Error.Validation("Fiscal",
                    $"Aucune feuille de détermination du résultat fiscal à finaliser pour l'exercice {year}."));
            if (existing.Status == FiscalDeclarationStatus.Finalized)
                return Result.Failure(Error.Conflict(
                    $"La feuille de détermination du résultat fiscal de l'exercice {year} est déjà finalisée."));

            // 3. Exercice verrouillé → 409.
            var locks = await _locks.GetLocksAsync(ct);
            if (locks.IsSuccess && locks.Value.Any(l => l.FiscalYear == year))
                return Result.Failure(Error.Conflict($"L'exercice {year} est verrouillé : finalisation impossible."));

            // 4. Passe 1 — montants autoritaires recalculés côté serveur (jamais du client).
            var parameters = await _parameters.GetOrDefaultAsync(year, ct);
            var dto1 = FiscalResultAssembler.Assemble(existing, parameters, _settings.FiscalLiasseEnabled);
            var taxDue = dto1.Computation.TaxDue;
            var css = dto1.Computation.Css;

            // 5. Blocages Option 2 (art. 2 code IRPP/IS) — bloquants à la finalisation
            //    (les mêmes règles ne produisent que des Warnings au brouillon, T12).
            var carryError = ValidateCarryForwardOrderingAtFinalization(year, existing.CarryForwards, parameters);
            if (carryError is not null)
                return Result.Failure(carryError);

            // 6. Écriture d'impôt (idempotente, statut Validee forcé).
            var taxEntry = await _accounting.GenerateFiscalTaxEntryAsync(year, taxDue, css, existing.Id, ct);
            if (taxEntry.IsFailure)
                return Result.Failure(taxEntry.Error);

            // 7. Recalcul des livres après écriture (lit ses propres écritures non commitées dans la
            //    transaction ambiante) : résultat net après impôt + charge de classe 69.
            var nct = await _reporting.GetNctStatementsAsync(year, ct);
            if (nct.IsFailure)
                return Result.Failure(nct.Error);

            var newNet = nct.Value.IncomeStatement.NetResult;
            var tax69 = SumClass69Charges(nct.Value.IncomeStatement);

            // 8. Alignement transactionnel de la feuille sur les livres (anti-circularité, revue v3) :
            //    AccountingResult = résultat net après impôt + ligne R-IS = charge de classe 69.
            var alignedAdjustments = BuildAlignedAdjustments(existing.Adjustments, tax69);
            var alignedCarry = existing.CarryForwards
                .Select(c => FiscalCarryForwardItem.Create(
                    c.Kind, c.OriginYear, c.InitialAmount, c.ImputedThisYear, c.ExpiryYear,
                    parameters.DeficitCarryForwardYears))
                .ToList();

            var upsert = new FiscalDeclarationUpsert(
                year,
                existing.TaxpayerKind,
                newNet,
                existing.AppliedIsRate,
                existing.LocalTurnoverTtc,
                existing.AcomptesPaid,
                existing.WithholdingSuffered,
                existing.PriorTaxCredit,
                alignedAdjustments,
                alignedCarry,
                existing.MinimumTaxRegime);

            FiscalResultDeclaration updated;
            try
            {
                updated = await _declarations.UpsertAsync(upsert, ct);
            }
            catch (InvalidOperationException ex)
            {
                // Feuille devenue finalisée entre-temps (concurrence) → 409.
                return Result.Failure(Error.Conflict(ex.Message));
            }

            // 9. Passe 2 — contrôle d'invariance : l'alignement (net après impôt + R-IS) doit laisser
            //    le résultat avant reports, la base imposable et l'impôt dû strictement inchangés.
            var dto2 = FiscalResultAssembler.Assemble(updated, parameters, _settings.FiscalLiasseEnabled);
            if (Math.Abs(dto2.Computation.ResultBeforeCarryForward - dto1.Computation.ResultBeforeCarryForward) >= Tolerance
                || Math.Abs(dto2.Computation.TaxableResult - dto1.Computation.TaxableResult) >= Tolerance
                || Math.Abs(dto2.Computation.TaxDue - taxDue) >= Tolerance
                || Math.Abs(dto2.Computation.Css - css) >= Tolerance)
            {
                return Result.Failure(Error.Conflict(
                    "Le résultat comptable de la feuille ne correspond plus aux livres après comptabilisation " +
                    "de l'impôt : reprendre le résultat suggéré et re-vérifier les réintégrations."));
            }

            // 10. Finalisation conditionnelle atomique (porte de concurrence) :
            //     false/déjà finalisée → 409 → rollback intégral (écriture et alignement annulés).
            var userId = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system";
            var outcome = await _declarations.FinalizeAsync(year, userId, ct);
            if (outcome == FiscalFinalizeOutcome.Absent)
                return Result.Failure(Error.Conflict(
                    $"La feuille de détermination du résultat fiscal de l'exercice {year} n'existe plus."));
            if (outcome == FiscalFinalizeOutcome.AlreadyFinalized)
                return Result.Failure(Error.Conflict(
                    $"La feuille de détermination du résultat fiscal de l'exercice {year} a été finalisée par une autre opération."));

            return Result.Success();
        }, cancellationToken);
    }

    /// <summary>
    ///Blocages Option 2 à la finalisation (T10 étape 5) : (a) déficit ordinaire périmé imputé ;
    /// (b) ordre non-FIFO ; (c) amortissement différé imputé avant épuisement des déficits ordinaires.
    /// </summary>
    private static Error? ValidateCarryForwardOrderingAtFinalization(
        int fiscalYear, IReadOnlyCollection<FiscalCarryForwardItem> carry, IncomeTaxYearParameter parameters)
    {
        var deficits = carry
            .Where(c => c.Kind == FiscalCarryForwardKind.Deficit)
            .OrderBy(c => c.OriginYear)
            .ToList();

        // (a) Déficit ordinaire périmé imputé.
        foreach (var d in deficits.Where(d => d.ImputedThisYear > 0m))
        {
            var expiry = d.ExpiryYear ?? (d.OriginYear + parameters.DeficitCarryForwardYears);
            if (expiry < fiscalYear)
                return Error.Validation("CarryForwards",
                    $"Déficit ordinaire d'origine {d.OriginYear} prescrit (imputable jusqu'à {expiry}) : " +
                    $"imputation interdite sur l'exercice {fiscalYear} (art. 8 code IRPP/IS).");
        }

        // (b) Ordre non-FIFO : un déficit plus récent imputé alors qu'un plus ancien imputable n'est
        //     pas intégralement imputé.
        foreach (var imputed in deficits.Where(d => d.ImputedThisYear > 0m))
        {
            foreach (var older in deficits.Where(o => o.OriginYear < imputed.OriginYear))
            {
                var olderExpiry = older.ExpiryYear ?? (older.OriginYear + parameters.DeficitCarryForwardYears);
                if (olderExpiry < fiscalYear)
                    continue;
                var remaining = Math.Max(older.InitialAmount, 0m) - Math.Max(older.ImputedThisYear, 0m);
                if (remaining > Tolerance)
                    return Error.Validation("CarryForwards",
                        $"Ordre d'imputation FIFO non respecté (art. 8 code IRPP/IS) : le déficit d'origine " +
                        $"{imputed.OriginYear} est imputé alors que le déficit plus ancien d'origine " +
                        $"{older.OriginYear} dispose encore d'un stock imputable de {remaining:N3} TND.");
            }
        }

        // (c) Amortissement différé imputé avant épuisement des déficits ordinaires imputables.
        var hasDeferredImputed = carry.Any(c =>
            c.Kind == FiscalCarryForwardKind.DeferredDepreciation && c.ImputedThisYear > 0m);
        if (hasDeferredImputed)
        {
            foreach (var d in deficits)
            {
                var dExpiry = d.ExpiryYear ?? (d.OriginYear + parameters.DeficitCarryForwardYears);
                if (dExpiry < fiscalYear)
                    continue;
                var remaining = Math.Max(d.InitialAmount, 0m) - Math.Max(d.ImputedThisYear, 0m);
                if (remaining > Tolerance)
                    return Error.Validation("CarryForwards",
                        $"Amortissement différé imputé avant épuisement des déficits ordinaires (art. 8 code IRPP/IS) : " +
                        $"le déficit d'origine {d.OriginYear} dispose encore d'un stock imputable de {remaining:N3} TND.");
            }
        }

        return null;
    }

    /// <summary>Reconstruit les ajustements en recopiant ceux existants et en alignant la ligne R-IS.</summary>
    private static IReadOnlyList<FiscalAdjustmentLine> BuildAlignedAdjustments(
        IReadOnlyCollection<FiscalAdjustmentLine> existing, decimal tax69)
    {
        var lines = new List<FiscalAdjustmentLine>();
        var risFound = false;

        foreach (var a in existing)
        {
            // La ligne R-IS est (re)construite ci-dessous au montant aligné (charge de classe 69).
            if (string.Equals(a.CatalogCode, FiscalFinalizationCatalogCodes.IncomeTaxReintegration, StringComparison.Ordinal))
            {
                risFound = true;
                if (tax69 > 0m)
                    lines.Add(FiscalAdjustmentLine.Create(
                        FiscalAdjustmentKind.Reintegration,
                        FiscalFinalizationCatalogCodes.IncomeTaxReintegration,
                        "Impôt sur les sociétés (compte 69)",
                        tax69,
                        isAutoSuggested: true));
                // tax69 == 0 : la ligne R-IS disparaît (aucun impôt comptabilisé).
                continue;
            }

            lines.Add(FiscalAdjustmentLine.Create(a.Kind, a.CatalogCode, a.Label, a.Amount, a.IsAutoSuggested));
        }

        // Aucune ligne R-IS préalable et un impôt a été comptabilisé → on la crée.
        if (!risFound && tax69 > 0m)
            lines.Add(FiscalAdjustmentLine.Create(
                FiscalAdjustmentKind.Reintegration,
                FiscalFinalizationCatalogCodes.IncomeTaxReintegration,
                "Impôt sur les sociétés (compte 69)",
                tax69,
                isAutoSuggested: true));

        return lines;
    }

    /// <summary>
    /// Somme des charges d'impôt de classe 69 de l'exercice (lignes IMP + IEX du compte de résultat),
    /// soit les débits nets des comptes 69 — la contrepartie de l'écriture d'impôt de la finalisation.
    /// </summary>
    private static decimal SumClass69Charges(NctIncomeStatementDto income)
    {
        // Le compte de résultat NCT présente le résultat avant impôt (RAI) et le résultat net (RN) ;
        // la charge d'impôt de l'exercice = RAI − RN (lorsqu'elle est positive, i.e. une charge d'impôt).
        // On retient ce delta plutôt qu'une somme de lignes brutes : il couvre l'impôt ordinaire (69)
        // et l'impôt extraordinaire (697) portés par l'écriture de finalisation.
        var delta = income.ResultBeforeTax - income.NetResult;
        return delta > 0m ? delta : 0m;
    }
}
