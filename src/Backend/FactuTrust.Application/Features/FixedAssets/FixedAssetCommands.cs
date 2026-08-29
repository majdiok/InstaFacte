using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.FixedAssets;

public sealed record CreateFixedAssetCommand(CreateFixedAssetRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateFixedAssetCommandHandler : IRequestHandler<CreateFixedAssetCommand, Result<Guid>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IDepreciationRateCategoryRepository _categories;
    private readonly ICurrentUser _currentUser;

    public CreateFixedAssetCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationRateCategoryRepository categories,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var category = await _categories.GetByIdAsync(r.DepreciationRateCategoryId, cancellationToken);
        if (category is null)
            return Result.Failure<Guid>(Error.Validation("DepreciationRateCategoryId", "Catégorie d'amortissement introuvable."));

        var year = r.AcquisitionDate.Year;

        var assetAccount = string.IsNullOrWhiteSpace(r.AssetAccountNumber) ? category.DefaultAssetAccount : r.AssetAccountNumber.Trim();
        var depreciationAccount = string.IsNullOrWhiteSpace(r.DepreciationAccountNumber) ? category.DefaultDepreciationAccount : r.DepreciationAccountNumber.Trim();
        var expenseAccount = string.IsNullOrWhiteSpace(r.ExpenseAccountNumber) ? category.DefaultExpenseAccount : r.ExpenseAccountNumber.Trim();

        var accountsValidation = FixedAssetAccountRules.Validate(assetAccount, depreciationAccount, expenseAccount);
        if (accountsValidation.IsFailure)
            return Result.Failure<Guid>(accountsValidation.Error);

        var resolved = FixedAssetRateResolver.Resolve(
            category.IsNonDepreciable,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            r.DepreciationRatePercent,
            r.UsefulLifeYears);
        if (resolved.IsFailure)
            return Result.Failure<Guid>(resolved.Error);

        var email = _currentUser.Email ?? "system";
        var vatCapitalized = FixedAssetVatRules.IsVatCapitalized(category.Code, assetAccount);

        var added = await _assets.AddWithGeneratedInventoryNumberAsync(
            inventoryNumber =>
            {
                var create = FixedAsset.Create(
                    inventoryNumber,
                    r.Label,
                    category.Id,
                    resolved.Value.RatePercent,
                    resolved.Value.LifeYears,
                    assetAccount,
                    depreciationAccount,
                    expenseAccount,
                    r.AcquisitionCost,
                    r.CapitalizedFees,
                    r.ResidualValue,
                    r.AcquisitionDate,
                    r.Description,
                    r.VatAmount,
                    r.Location,
                    r.SupplierId,
                    r.DepreciationMethod,
                    r.AccelerationCoefficient ?? 1m,
                    vatCapitalized);

                if (create.IsSuccess)
                    create.Value.SetAuditInfo(email, false);

                return create;
            },
            year,
            cancellationToken);

        if (added.IsFailure)
            return Result.Failure<Guid>(added.Error);

        return Result.Success(added.Value.Id);
    }
}

public sealed record PutFixedAssetInServiceCommand(Guid Id, PutFixedAssetInServiceRequest Request) : IRequest<Result<Guid>>;

public sealed class PutFixedAssetInServiceCommandHandler : IRequestHandler<PutFixedAssetInServiceCommand, Result<Guid>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IAccountingService _accounting;
    private readonly IDepreciationEngine _engine;
    private readonly ICurrentUser _currentUser;

    public PutFixedAssetInServiceCommandHandler(
        IFixedAssetRepository assets,
        IAccountingService accounting,
        IDepreciationEngine engine,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _accounting = accounting;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(PutFixedAssetInServiceCommand request, CancellationToken cancellationToken)
    {
        var preview = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (preview is null)
            return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        IReadOnlyList<DepreciationScheduleLine>? scheduleLines = null;
        if (preview.DepreciationRatePercent > 0 && !preview.ScheduleLines.Any(l => l.IsPosted))
        {
            var simulated = preview;
            var dryRun = simulated.PutInService(request.Request.InServiceDate, request.Request.CreditAccountNumber);
            if (dryRun.IsFailure)
                return Result.Failure<Guid>(dryRun.Error);
            scheduleLines = _engine.GenerateSchedule(simulated);
        }

        var putResult = await _assets.PutInServiceInTransactionAsync(
            request.Id,
            request.Request.InServiceDate,
            request.Request.CreditAccountNumber,
            scheduleLines,
            _currentUser.Email ?? "system",
            cancellationToken);

        if (putResult.IsFailure)
            return Result.Failure<Guid>(putResult.Error);

        var asset = putResult.Value;

        // Les actifs issus d'une facture fournisseur sont déjà comptabilisés (21x au journal JA) :
        // ne pas regénérer d'écriture d'acquisition pour éviter la double comptabilisation.
        if (asset.SupplierInvoiceId is null)
        {
            var entry = await _accounting.GenerateFixedAssetAcquisitionEntryAsync(asset, cancellationToken);
            if (entry.IsFailure)
                return Result.Failure<Guid>(entry.Error);
        }

        return Result.Success(asset.Id);
    }
}

public sealed record GenerateDepreciationScheduleCommand(Guid Id) : IRequest<Result<FixedAssetScheduleDto>>;

public sealed class GenerateDepreciationScheduleCommandHandler : IRequestHandler<GenerateDepreciationScheduleCommand, Result<FixedAssetScheduleDto>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IDepreciationEngine _engine;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantUnitOfWork _unitOfWork;

    public GenerateDepreciationScheduleCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationEngine engine,
        ICurrentUser currentUser,
        ITenantUnitOfWork unitOfWork)
    {
        _assets = assets;
        _engine = engine;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FixedAssetScheduleDto>> Handle(GenerateDepreciationScheduleCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        if (asset.InServiceDate is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("InServiceDate", "L'immobilisation doit être mise en service avant de générer le tableau."));

        // État d'extourne des lignes (T13, C6) : une dotation dont l'écriture est extournée n'est
        // plus une dotation « nette » et ne doit plus bloquer la régénération.
        var reversalState = await _assets.GetScheduleLinesWithReversalStateAsync(request.Id, cancellationToken);
        var isReversedByLineId = reversalState.ToDictionary(r => r.Line.Id, r => r.IsReversed);

        if (asset.ScheduleLines.Any(l => l.IsPosted && !isReversedByLineId.GetValueOrDefault(l.Id)))
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("Schedule", "Impossible de regénérer : des dotations sont déjà comptabilisées (non extournées)."));

        // Atomicité (T13, C6) : dé-postage des lignes extournées + recalcul du cumul + merge du
        // tableau régénéré dans une seule transaction. Aucune écriture comptable modifiée (E1).
        Result txnResult;
        try
        {
            txnResult = await _unitOfWork.ExecuteAsync(async ct =>
            {
                // 1. Dé-poster les lignes dont l'écriture est explicitement extournée — conserve
                //    le lien d'audit JournalEntryId (T13, C6 : Unpost ne le remet plus à null).
                var reversedPosted = asset.ScheduleLines
                    .Where(l => l.IsPosted && isReversedByLineId.GetValueOrDefault(l.Id))
                    .ToList();
                foreach (var line in reversedPosted)
                {
                    line.Unpost();
                    await _assets.SaveScheduleLineAsync(line, ct);
                }

                // 2. Merger le tableau régénéré (préserve l'identité des lignes — T13 étape 3).
                //    Les lignes extournées sont désormais non postées → le merge peut les mettre à
                //    jour en place (même Id, JournalEntryId d'audit conservé).
                var regenerated = _engine.GenerateSchedule(asset);
                await _assets.ReplaceScheduleLinesAsync(asset.Id, regenerated, ct);

                // 3. Recalculer le cumul de l'actif à partir des lignes restées comptabilisées
                //    (non extournées) — uniquement si des lignes ont été dé-postées ; sans ligne
                //    extournée, le cumul est déjà à jour (comportement préservé, aucune écriture
                //    sur l'actif). Aucune écriture comptable modifiée (E1).
                if (reversedPosted.Count > 0)
                {
                    var remainingAccumulated = asset.ScheduleLines
                        .Where(l => l.IsPosted)
                        .Select(l => (decimal?)l.AccumulatedDepreciation)
                        .Max() ?? 0m;
                    asset.RecalculateDepreciationTotals(remainingAccumulated);
                    asset.SetAuditInfo(_currentUser.Email ?? "system", true);
                    await _assets.UpdateDepreciationTotalsAsync(asset, ct);
                }

                return Result.Success();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<FixedAssetScheduleDto>(new Error("FixedAsset.RegenerateSchedule", ex.Message));
        }

        if (txnResult.IsFailure)
            return Result.Failure<FixedAssetScheduleDto>(txnResult.Error);

        // Recharger pour le DTO : lignes fusionnées (mêmes Id, montants à jour) + état d'extourne frais.
        asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        var freshReversal = await _assets.GetScheduleLinesWithReversalStateAsync(request.Id, cancellationToken);
        return Result.Success(ToScheduleDto(asset, freshReversal));
    }

    internal static FixedAssetScheduleDto ToScheduleDto(
        FixedAsset asset,
        IReadOnlyList<(DepreciationScheduleLine Line, bool IsReversed)>? reversalState = null)
    {
        var isReversedByLineId = reversalState?.ToDictionary(r => r.Line.Id, r => r.IsReversed);
        var lines = asset.ScheduleLines
            .Select(l => FixedAssetMappings.ToDto(l, isReversedByLineId is not null && isReversedByLineId.GetValueOrDefault(l.Id)))
            .ToList();

        return new FixedAssetScheduleDto(
            asset.Id,
            asset.InventoryNumber,
            asset.Label,
            asset.AcquisitionDate,
            asset.InServiceDate,
            asset.TotalCapitalizedCost,
            asset.DepreciationRatePercent,
            asset.UsefulLifeYears,
            asset.DepreciableBase,
            lines,
            asset.DepreciationMethod,
            asset.AccelerationCoefficient);
    }
}

public sealed record UpdateFixedAssetCommand(Guid Id, UpdateFixedAssetRequest Request) : IRequest<Result<Guid>>;

public sealed class UpdateFixedAssetCommandHandler : IRequestHandler<UpdateFixedAssetCommand, Result<Guid>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IDepreciationRateCategoryRepository _categories;
    private readonly ICurrentUser _currentUser;

    public UpdateFixedAssetCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationRateCategoryRepository categories,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(UpdateFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var asset = await _assets.GetByIdAsync(request.Id, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        var categoryId = r.DepreciationRateCategoryId ?? asset.DepreciationRateCategoryId;
        var category = await _categories.GetByIdAsync(categoryId, cancellationToken);
        if (category is null)
            return Result.Failure<Guid>(Error.Validation("DepreciationRateCategoryId", "Catégorie d'amortissement introuvable."));

        var assetAccount = string.IsNullOrWhiteSpace(r.AssetAccountNumber) ? category.DefaultAssetAccount : r.AssetAccountNumber.Trim();
        var depreciationAccount = string.IsNullOrWhiteSpace(r.DepreciationAccountNumber) ? category.DefaultDepreciationAccount : r.DepreciationAccountNumber.Trim();
        var expenseAccount = string.IsNullOrWhiteSpace(r.ExpenseAccountNumber) ? category.DefaultExpenseAccount : r.ExpenseAccountNumber.Trim();

        var accountsValidation = FixedAssetAccountRules.Validate(assetAccount, depreciationAccount, expenseAccount);
        if (accountsValidation.IsFailure)
            return Result.Failure<Guid>(accountsValidation.Error);

        var resolved = FixedAssetRateResolver.Resolve(
            category.IsNonDepreciable,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            r.DepreciationRatePercent,
            r.UsefulLifeYears);
        if (resolved.IsFailure)
            return Result.Failure<Guid>(resolved.Error);

        var update = asset.UpdateDraft(
            r.Label,
            r.Description,
            r.AcquisitionCost,
            r.CapitalizedFees,
            r.ResidualValue,
            r.AcquisitionDate,
            resolved.Value.RatePercent,
            resolved.Value.LifeYears,
            assetAccount,
            depreciationAccount,
            expenseAccount,
            r.Location,
            r.DepreciationMethod,
            r.AccelerationCoefficient,
            category.Id,
            r.VatAmount,
            FixedAssetVatRules.IsVatCapitalized(category.Code, assetAccount));

        if (update.IsFailure)
            return Result.Failure<Guid>(update.Error);

        asset.SetAuditInfo(_currentUser.Email ?? "system", true);
        await _assets.UpdateAsync(asset, cancellationToken);
        return Result.Success(asset.Id);
    }
}

public sealed record PreviewDepreciationScheduleQuery(Guid Id, DateTime? InServiceDate) : IRequest<Result<FixedAssetScheduleDto>>;

/// <summary>
/// Simule le tableau d'amortissement sans rien persister : utile avant la mise en service
/// (avec une date hypothétique) ou pour vérifier le plan d'un actif en service.
/// </summary>
public sealed class PreviewDepreciationScheduleQueryHandler
    : IRequestHandler<PreviewDepreciationScheduleQuery, Result<FixedAssetScheduleDto>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IDepreciationEngine _engine;

    public PreviewDepreciationScheduleQueryHandler(IFixedAssetRepository assets, IDepreciationEngine engine)
    {
        _assets = assets;
        _engine = engine;
    }

    public async Task<Result<FixedAssetScheduleDto>> Handle(PreviewDepreciationScheduleQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        if (asset.DepreciationRatePercent <= 0)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("Schedule", "Ce bien n'est pas amortissable (taux nul)."));

        var simulated = asset;
        if (asset.InServiceDate is null)
        {
            var inServiceDate = request.InServiceDate ?? asset.AcquisitionDate;
            // Simulation sur une copie détachée (T6, B3) : l'entité chargée par ce handler n'est
            // ni mutée ni persistée ici — voir FixedAsset.CreateSimulationCopy.
            var copy = asset.CreateSimulationCopy(inServiceDate, asset.CreditAccountNumber ?? "404");
            if (copy.IsFailure)
                return Result.Failure<FixedAssetScheduleDto>(copy.Error);
            simulated = copy.Value;
        }

        var lines = _engine.GenerateSchedule(simulated);
        return Result.Success(new FixedAssetScheduleDto(
            simulated.Id,
            simulated.InventoryNumber,
            simulated.Label,
            simulated.AcquisitionDate,
            simulated.InServiceDate,
            simulated.TotalCapitalizedCost,
            simulated.DepreciationRatePercent,
            simulated.UsefulLifeYears,
            simulated.DepreciableBase,
            lines.Select(FixedAssetMappings.ToDto).ToList(),
            simulated.DepreciationMethod,
            simulated.AccelerationCoefficient));
    }
}

public sealed record PostDepreciationRunCommand(PostDepreciationRunRequest Request) : IRequest<Result<DepreciationRunResultDto>>;

public sealed class PostDepreciationRunCommandHandler : IRequestHandler<PostDepreciationRunCommand, Result<DepreciationRunResultDto>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IAccountingService _accounting;
    private readonly ITenantUnitOfWork _unitOfWork;

    public PostDepreciationRunCommandHandler(
        IFixedAssetRepository assets,
        IAccountingService accounting,
        ITenantUnitOfWork unitOfWork)
    {
        _assets = assets;
        _accounting = accounting;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepreciationRunResultDto>> Handle(PostDepreciationRunCommand request, CancellationToken cancellationToken)
    {
        var year = request.Request.FiscalYear;
        if (year < 2000 || year > 2100)
            return Result.Failure<DepreciationRunResultDto>(Error.Validation("FiscalYear", "Exercice invalide."));
        if (year > DateTime.UtcNow.Year)
            return Result.Failure<DepreciationRunResultDto>(Error.Validation("FiscalYear", "Impossible de comptabiliser des dotations d'un exercice futur."));

        var lines = await _assets.GetUnpostedScheduleLinesForYearAsync(year, cancellationToken);
        var posted = 0;
        var skipped = 0;
        var total = 0m;
        var errors = new List<string>();

        foreach (var line in lines)
        {
            var asset = line.FixedAsset;
            if (asset is null)
            {
                skipped++;
                continue;
            }

            // Atomicité par ligne (T7, B4) : écriture comptable + ligne d'échéancier + actif sont
            // enveloppés dans la MÊME transaction (unité de travail) — un échec sur cette ligne
            // (écriture générée mais échec de persistance, par exemple) annule uniquement cette
            // ligne (rollback), le run se poursuit sur les lignes suivantes. Idempotence
            // (SourceFixedAssetDepreciation + line.Id) inchangée — voir AccountingService.
            Result lineResult;
            try
            {
                lineResult = await _unitOfWork.ExecuteAsync(async ct =>
                {
                    var generated = await _accounting.GenerateFixedAssetDepreciationEntryAsync(asset, line, cancellationToken: ct);
                    if (generated.IsFailure)
                        return generated;

                    await _assets.SaveScheduleLineAsync(line, ct);
                    await _assets.UpdateAsync(asset, ct);
                    return Result.Success();
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                lineResult = Result.Failure(new Error("DepreciationRun.LineFailed", ex.Message));
            }

            if (lineResult.IsFailure)
            {
                errors.Add($"{asset.InventoryNumber}: {lineResult.Error.Description}");
                skipped++;
                continue;
            }

            posted++;
            total += line.DepreciationAmount;
        }

        var alreadyPostedCount = await _assets.GetPostedScheduleLineCountForYearAsync(year, cancellationToken);

        return Result.Success(new DepreciationRunResultDto(year, posted, skipped, total, errors, alreadyPostedCount));
    }
}

public sealed record DisposeFixedAssetCommand(Guid Id, DisposeFixedAssetRequest Request) : IRequest<Result<Guid>>;

public sealed class DisposeFixedAssetCommandHandler : IRequestHandler<DisposeFixedAssetCommand, Result<Guid>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IDepreciationEngine _engine;
    private readonly IAccountingService _accounting;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantUnitOfWork _unitOfWork;

    public DisposeFixedAssetCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationEngine engine,
        IAccountingService accounting,
        ICurrentUser currentUser,
        ITenantUnitOfWork unitOfWork)
    {
        _assets = assets;
        _engine = engine;
        _accounting = accounting;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(DisposeFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        var disposalDate = r.DisposalDate.Date;
        var disposalYear = disposalDate.Year;
        var treasury = string.IsNullOrWhiteSpace(r.TreasuryAccountNumber) ? null : r.TreasuryAccountNumber.Trim();
        var receivable = string.IsNullOrWhiteSpace(r.ReceivableAccountNumber) ? null : r.ReceivableAccountNumber.Trim();

        // Validation des comptes de règlement (452 / trésorerie / mise au rebut) — T4, A6.
        if (r.DisposalProceeds > 0)
        {
            var hasTreasury = !string.IsNullOrEmpty(treasury);
            var hasReceivable = !string.IsNullOrEmpty(receivable);
            if (hasTreasury == hasReceivable)
                return Result.Failure<Guid>(Error.Validation("DisposalAccount", "Indiquez un seul compte de règlement : trésorerie (comptant) ou créance 452 (à terme)."));
            if (hasReceivable && !receivable!.StartsWith("452"))
                return Result.Failure<Guid>(Error.Validation("ReceivableAccountNumber", "Le compte de créance sur cession doit commencer par 452."));
        }

        // Pré-condition « exercices antérieurs comptabilisés » (finding 2 — BLOQUANT).
        var priorUnpostedYears = asset.ScheduleLines
            .Where(l => l.FiscalYear < disposalYear && !l.IsPosted)
            .Select(l => l.FiscalYear)
            .Distinct()
            .OrderBy(y => y)
            .ToList();
        if (priorUnpostedYears.Count > 0)
            return Result.Failure<Guid>(Error.Validation("Schedule",
                $"Des dotations d'exercices antérieurs ({string.Join(", ", priorUnpostedYears)}) ne sont pas comptabilisées. Comptabilisez-les (page Dotations) avant d'enregistrer la cession."));

        // Pré-condition : la ligne de l'année de cession ne doit pas être déjà comptabilisée.
        if (asset.ScheduleLines.Any(l => l.FiscalYear == disposalYear && l.IsPosted))
            return Result.Failure<Guid>(Error.Validation("Schedule",
                "La dotation de l'année de cession est déjà comptabilisée (annuité pleine). La cession ne peut pas recalculer une dotation déjà postée."));

        // Atomicité tout-ou-rien (finding 1 — BLOQUANT, D9) : tout le workflow dans une transaction.
        Result<Guid> disposalResult;
        try
        {
            disposalResult = await _unitOfWork.ExecuteAsync(async ct =>
            {
                // 1. Mutation de l'actif (cession) — DisposalDate alimente le moteur de prorata.
                var dispose = asset.Dispose(disposalDate, r.DisposalProceeds, treasury, receivable);
                if (dispose.IsFailure)
                    return Result.Failure<Guid>(dispose.Error);

                // 2. Prorata de l'année de cession + remplacement des lignes non postées (B1).
                if (asset.DepreciationRatePercent > 0)
                {
                    var hasPosted = asset.ScheduleLines.Any(l => l.IsPosted);
                    if (!hasPosted)
                    {
                        // Aucune dotation comptabilisée : régénération complète (comportement préservé).
                        var regenerated = _engine.GenerateSchedule(asset);
                        await _assets.ReplaceScheduleLinesAsync(asset.Id, regenerated, ct);
                    }
                    else
                    {
                        // Dotations antérieures comptabilisées : prorata en place de la ligne de cession,
                        // suppression des lignes futures non postées. Les lignes IsPosted sont intactes.
                        var priorAccumulated = asset.ScheduleLines
                            .Where(l => l.FiscalYear < disposalYear && l.IsPosted)
                            .Sum(l => l.DepreciationAmount);

                        var prorataAmount = _engine.CalculateDisposalYearDepreciation(asset, disposalYear, priorAccumulated);
                        var target = BuildDisposalYearLine(asset, disposalYear, priorAccumulated, prorataAmount);
                        await _assets.ReplaceUnpostedScheduleLinesAsync(asset.Id, target is null ? Array.Empty<DepreciationScheduleLine>() : new[] { target }, ct);
                    }

                    // Recharger pour obtenir la ligne de cession mise à jour (montants prorata, même Id).
                    asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: ct);
                    if (asset is null)
                        return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

                    // La recharge perd l'état de cession (non encore persisté) : on le ré-applique.
                    var reDispose = asset.Dispose(disposalDate, r.DisposalProceeds, treasury, receivable);
                    if (reDispose.IsFailure)
                        return Result.Failure<Guid>(reDispose.Error);

                    // 3. Dotation complémentaire datée à la date de cession (T3).
                    var yearLine = asset.ScheduleLines.FirstOrDefault(l => l.FiscalYear == disposalYear && !l.IsPosted);
                    if (yearLine is not null && yearLine.DepreciationAmount > 0)
                    {
                        var dep = await _accounting.GenerateFixedAssetDepreciationEntryAsync(asset, yearLine, entryDate: disposalDate, cancellationToken: ct);
                        if (dep.IsFailure)
                            return Result.Failure<Guid>(dep.Error);
                        await _assets.SaveScheduleLineAsync(yearLine, ct);
                    }
                }

                // 4. Mutation de l'actif (cumuls, audit).
                asset.SetAuditInfo(_currentUser.Email ?? "system", true);
                await _assets.UpdateAsync(asset, ct);

                // 5. Écriture de sortie (schéma net 636/736 — E3).
                var disposalEntry = await _accounting.GenerateFixedAssetDisposalEntryAsync(asset, ct);
                if (disposalEntry.IsFailure)
                    return Result.Failure<Guid>(disposalEntry.Error);

                return Result.Success(asset.Id);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(new Error("FixedAsset.Disposal", ex.Message));
        }

        return disposalResult;
    }

    /// <summary>
    /// Construit la ligne cible de l'année de cession (prorata) à partir des montants réellement
    /// comptabilisés (<paramref name="priorAccumulated"/>) et de la dotation prorata calculée par
    /// le moteur. L'<c>NormalAnnualAmount</c> (annuité pleine) provient du tableau régénéré pour
    /// l'affichage ; les cumuls sont dérivés de la situation réelle.
    /// </summary>
    private DepreciationScheduleLine? BuildDisposalYearLine(
        FixedAsset asset,
        int disposalYear,
        decimal priorAccumulated,
        decimal prorataAmount)
    {
        if (prorataAmount <= 0)
            return null;

        var generated = _engine.GenerateSchedule(asset);
        var template = generated.FirstOrDefault(l => l.FiscalYear == disposalYear);
        if (template is null)
            return null;

        const MidpointRounding rounding = MidpointRounding.AwayFromZero;
        var accumulated = Math.Round(priorAccumulated + prorataAmount, 3, rounding);
        var closingNbv = Math.Round(Math.Max(asset.ResidualValue, asset.TotalCapitalizedCost - accumulated), 3, rounding);
        var openingNbv = Math.Round(Math.Max(asset.ResidualValue, asset.TotalCapitalizedCost - priorAccumulated), 3, rounding);

        return DepreciationScheduleLine.Create(
            asset.Id,
            disposalYear,
            template.PeriodMonth,
            openingNbv,
            template.NormalAnnualAmount,
            priorAccumulated,
            prorataAmount,
            accumulated,
            closingNbv).Value;
    }
}