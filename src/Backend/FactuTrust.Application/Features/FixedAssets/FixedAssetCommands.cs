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

    public GenerateDepreciationScheduleCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationEngine engine,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<FixedAssetScheduleDto>> Handle(GenerateDepreciationScheduleCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        if (asset.InServiceDate is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("InServiceDate", "L'immobilisation doit être mise en service avant de générer le tableau."));

        if (asset.ScheduleLines.Any(l => l.IsPosted))
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("Schedule", "Impossible de regénérer : des dotations sont déjà comptabilisées."));

        var lines = _engine.GenerateSchedule(asset);
        await _assets.ReplaceScheduleLinesAsync(asset.Id, lines, cancellationToken);

        asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        return Result.Success(ToScheduleDto(asset));
    }

    internal static FixedAssetScheduleDto ToScheduleDto(FixedAsset asset) =>
        new(
            asset.Id,
            asset.InventoryNumber,
            asset.Label,
            asset.AcquisitionDate,
            asset.InServiceDate,
            asset.TotalCapitalizedCost,
            asset.DepreciationRatePercent,
            asset.UsefulLifeYears,
            asset.DepreciableBase,
            asset.ScheduleLines.Select(FixedAssetMappings.ToDto).ToList(),
            asset.DepreciationMethod,
            asset.AccelerationCoefficient);
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

    public DisposeFixedAssetCommandHandler(
        IFixedAssetRepository assets,
        IDepreciationEngine engine,
        IAccountingService accounting,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _engine = engine;
        _accounting = accounting;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(DisposeFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        var dispose = asset.Dispose(
            request.Request.DisposalDate,
            request.Request.DisposalProceeds,
            request.Request.TreasuryAccountNumber);
        if (dispose.IsFailure)
            return Result.Failure<Guid>(dispose.Error);

        if (asset.DepreciationRatePercent > 0 &&
            (!asset.ScheduleLines.Any() || !asset.ScheduleLines.Any(l => l.IsPosted)))
        {
            var regenerated = _engine.GenerateSchedule(asset);
            await _assets.ReplaceScheduleLinesAsync(asset.Id, regenerated, cancellationToken);
            asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
            if (asset is null)
                return Result.Failure<Guid>(Error.Validation("FixedAsset", "Immobilisation introuvable."));
        }

        var disposalYear = request.Request.DisposalDate.Year;
        var yearLine = asset.ScheduleLines.FirstOrDefault(l => l.FiscalYear == disposalYear && !l.IsPosted);
        if (yearLine is not null && yearLine.DepreciationAmount > 0)
        {
            var dep = await _accounting.GenerateFixedAssetDepreciationEntryAsync(asset, yearLine, cancellationToken: cancellationToken);
            if (dep.IsFailure)
                return Result.Failure<Guid>(dep.Error);
            await _assets.SaveScheduleLineAsync(yearLine, cancellationToken);
        }

        asset.SetAuditInfo(_currentUser.Email ?? "system", true);
        await _assets.UpdateAsync(asset, cancellationToken);

        var disposalEntry = await _accounting.GenerateFixedAssetDisposalEntryAsync(asset, cancellationToken);
        if (disposalEntry.IsFailure)
            return Result.Failure<Guid>(disposalEntry.Error);

        return Result.Success(asset.Id);
    }
}