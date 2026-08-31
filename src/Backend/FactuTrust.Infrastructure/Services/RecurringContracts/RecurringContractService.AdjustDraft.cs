using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Validators;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

/// <summary>
/// Ajustement contrôlé du brouillon d'une échéance (modal « Ajustement du brouillon »).
/// Whitelist : désignation, quantité, prix unitaire HT, TVA. Produit et nombre de lignes figés.
/// Synchronise le snapshot du billing run et aligne les lignes catalogue du contrat.
/// Aucune émission, aucun journal comptable.
/// </summary>
public sealed partial class RecurringContractService
{
    private static readonly TimeSpan SubmissionLockTimeout = TimeSpan.FromMinutes(5);

    private const string UsageLineDesignationPrefix = "Consommation période";
    private const string ProrationLineDesignation = "Ajustement prorata période";

    public async Task<Result<AdjustableRecurringDraftDto>> GetAdjustableDraftAsync(
        Guid billingRunId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAdjustableAsync(billingRunId, trackDraft: false, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(loaded.Error);

        return Result.Success(await MapAdjustableDraftAsync(loaded.Value, cancellationToken));
    }

    public async Task<Result<AdjustableRecurringDraftDto>> AdjustDraftLinesAsync(
        Guid billingRunId,
        AdjustRecurringDraftLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAdjustableAsync(billingRunId, trackDraft: true, cancellationToken);
        if (loaded.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(loaded.Error);

        var (run, draft, _) = loaded.Value;
        var existing = draft.GetLines();
        var incoming = request.Lines ?? Array.Empty<AdjustRecurringDraftLineDto>();

        if (incoming.Count != existing.Count)
            return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                "Lines",
                "Le nombre de lignes ne peut pas être modifié."));

        var expectedIndexes = Enumerable.Range(0, existing.Count);
        if (incoming.Select(l => l.Index).OrderBy(i => i).SequenceEqual(expectedIndexes) == false)
            return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                "Lines",
                "Les index de lignes sont incomplets ou hors bornes."));

        var byIndex = incoming.ToDictionary(l => l.Index);
        var merged = new List<DraftInvoiceLine>(existing.Count);

        for (var i = 0; i < existing.Count; i++)
        {
            var current = existing[i];
            var patch = byIndex[i];

            if (patch.Quantity <= 0 || patch.Quantity < TunisianValidationRules.NumericLimits.MinQuantity)
                return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                    "Quantity",
                    $"Ligne {i + 1} : la quantité doit être supérieure à zéro."));

            if (patch.UnitPriceHT < 0)
                return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                    "UnitPriceHT",
                    $"Ligne {i + 1} : le prix unitaire HT ne peut pas être négatif."));

            if (!TunisianValidationRules.IsValidVatRate(patch.VatRate))
                return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                    "VatRate",
                    $"Ligne {i + 1} : le taux de TVA doit être 0, 7, 13 ou 19 %."));

            var designation = (patch.Designation ?? string.Empty).Trim();
            if (designation.Length == 0)
                designation = current.Designation ?? string.Empty;

            if (designation.Length > TunisianValidationRules.MaxLengths.Designation)
                return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                    "Designation",
                    $"Ligne {i + 1} : la désignation ne peut pas dépasser {TunisianValidationRules.MaxLengths.Designation} caractères."));

            if (string.IsNullOrWhiteSpace(designation) && string.IsNullOrWhiteSpace(current.ProductId))
                return Result.Failure<AdjustableRecurringDraftDto>(Error.Validation(
                    "Designation",
                    $"Ligne {i + 1} : la désignation est obligatoire."));

            merged.Add(current with
            {
                Designation = designation,
                Quantity = patch.Quantity,
                UnitPriceHT = patch.UnitPriceHT,
                VatRate = patch.VatRate,
                PriceOverridden = true
            });
        }

        draft.UpdateLines(merged);

        var (fixedAmount, usageAmount, prorationAmount) = SplitDraftAmountsByKind(existing, merged);
        var revise = run.ReviseDraftAmounts(fixedAmount, usageAmount, prorationAmount);
        if (revise.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(revise.Error);

        var syncLines = await SyncContractCatalogLinesAsync(run, merged, cancellationToken);
        if (syncLines.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(syncLines.Error);

        await _db.SaveChangesAsync(cancellationToken);

        var mapped = await LoadAdjustableAsync(run.Id, trackDraft: false, cancellationToken);
        if (mapped.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(mapped.Error);

        return Result.Success(await MapAdjustableDraftAsync(mapped.Value, cancellationToken));
    }

    /// <summary>
    /// Ventile le HT des lignes draft (qty × prix) en Fixed / Usage / Proration.
    /// La classification se base sur les désignations d'origine (avant patch) pour rester stable
    /// si l'utilisateur renomme une ligne synthétique Usage/Prorata.
    /// </summary>
    private static (decimal Fixed, decimal Usage, decimal Proration) SplitDraftAmountsByKind(
        IReadOnlyList<DraftInvoiceLine> original,
        IReadOnlyList<DraftInvoiceLine> merged)
    {
        decimal fixedHt = 0m;
        decimal usageHt = 0m;
        decimal prorationHt = 0m;

        for (var i = 0; i < merged.Count; i++)
        {
            var lineHt = TunisianValidationRules.RoundToMillimes(merged[i].Quantity * merged[i].UnitPriceHT);
            var kind = ClassifyDraftLineKind(original[i].Designation);

            switch (kind)
            {
                case DraftLineKind.Usage:
                    usageHt += lineHt;
                    break;
                case DraftLineKind.Proration:
                    prorationHt += lineHt;
                    break;
                default:
                    fixedHt += lineHt;
                    break;
            }
        }

        return (fixedHt, usageHt, prorationHt);
    }

    private static DraftLineKind ClassifyDraftLineKind(string? designation)
    {
        var d = designation ?? string.Empty;
        if (d.StartsWith(UsageLineDesignationPrefix, StringComparison.Ordinal))
            return DraftLineKind.Usage;
        if (d.StartsWith(ProrationLineDesignation, StringComparison.Ordinal))
            return DraftLineKind.Proration;
        return DraftLineKind.Fixed;
    }

    private enum DraftLineKind { Fixed, Usage, Proration }

    /// <summary>
    /// Aligne les lignes catalogue actives du contrat (même filtre/ordre que BuildDraftLines)
    /// sur les premières lignes du brouillon. Les lignes synthétiques Usage/Prorata ne sont pas
    /// répercutées sur <see cref="RecurringContractLine"/>.
    /// </summary>
    private async Task<Result> SyncContractCatalogLinesAsync(
        RecurringContractBillingRun run,
        IReadOnlyList<DraftInvoiceLine> merged,
        CancellationToken cancellationToken)
    {
        var contract = await _db.RecurringContracts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == run.RecurringContractId, cancellationToken);
        if (contract is null)
            return Result.Failure(Error.NotFound("RecurringContract", run.RecurringContractId));

        var catalogLines = GetCatalogLinesForDraft(contract, run.PeriodTo).ToList();
        if (merged.Count < catalogLines.Count)
            return Result.Failure(Error.Validation(
                "Lines",
                "Incohérence structurelle : le brouillon a moins de lignes que le catalogue du contrat."));

        for (var i = 0; i < catalogLines.Count; i++)
        {
            var contractLine = catalogLines[i];
            var draftLine = merged[i];
            var updated = contractLine.Update(
                draftLine.Designation ?? contractLine.Description,
                draftLine.Quantity,
                draftLine.UnitPriceHT,
                draftLine.VatRate,
                contractLine.IncludedQuantity,
                contractLine.OverageUnitPriceHT,
                contractLine.SortOrder);
            if (updated.IsFailure)
                return updated;
        }

        return Result.Success();
    }

    /// <summary>Même filtre que <c>RecurringContractBillingService.BuildDraftLines</c> (hors usage/prorata).</summary>
    internal static IEnumerable<RecurringContractLine> GetCatalogLinesForDraft(
        RecurringContract contract,
        DateTime periodTo)
    {
        foreach (var contractLine in contract.GetActiveLinesOn(periodTo))
        {
            if (contractLine.LineType == RecurringContractLineType.OneTimeSetup && contract.SetupFeeBilled)
                continue;
            if (contractLine.LineType == RecurringContractLineType.UsageMetered)
                continue;
            yield return contractLine;
        }
    }

    private async Task<Result<(RecurringContractBillingRun Run, InvoiceDraft Draft, RecurringContract Contract)>> LoadAdjustableAsync(
        Guid billingRunId,
        bool trackDraft,
        CancellationToken cancellationToken)
    {
        var run = await _db.RecurringContractBillingRuns
            .FirstOrDefaultAsync(r => r.Id == billingRunId, cancellationToken);
        if (run is null)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.NotFound("BillingRun", billingRunId));

        if (run.Status != RecurringContractBillingRunStatus.DraftCreated || !run.InvoiceDraftId.HasValue)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.Conflict("Ce brouillon n'est pas ajustable. Il a déjà été facturé, a échoué, ou n'est pas encore prêt."));

        var draftQuery = _db.InvoiceDrafts.AsQueryable();
        if (!trackDraft)
            draftQuery = draftQuery.AsNoTracking();

        var draft = await draftQuery.FirstOrDefaultAsync(d => d.Id == run.InvoiceDraftId.Value, cancellationToken);
        if (draft is null)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.NotFound("Draft", run.InvoiceDraftId.Value));

        if (draft.IsConverted)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.Conflict("Ce brouillon a déjà été converti en facture."));

        if (draft.IsExpired)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.Conflict("Ce brouillon a expiré."));

        if (draft.IsSubmitting
            && draft.SubmissionStartedAt.HasValue
            && DateTime.UtcNow - draft.SubmissionStartedAt.Value <= SubmissionLockTimeout)
        {
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.Conflict("Une émission est déjà en cours pour ce brouillon."));
        }

        var contract = await _db.RecurringContracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == run.RecurringContractId, cancellationToken);
        if (contract is null)
            return Result.Failure<(RecurringContractBillingRun, InvoiceDraft, RecurringContract)>(
                Error.NotFound("RecurringContract", run.RecurringContractId));

        return Result.Success((run, draft, contract));
    }

    private async Task<AdjustableRecurringDraftDto> MapAdjustableDraftAsync(
        (RecurringContractBillingRun Run, InvoiceDraft Draft, RecurringContract Contract) source,
        CancellationToken cancellationToken)
    {
        var (run, draft, contract) = source;
        var draftLines = draft.GetLines();
        var productIds = draftLines
            .Select(l => Guid.TryParse(l.ProductId, out var id) && id != Guid.Empty ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var productNames = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var currency = draft.GetMetadata()?.Currency ?? contract.Currency;
        var wizardLines = draftLines.Select(ToWizardLine).ToList();
        var calculated = InvoiceCalculationService.CalculateTotals(wizardLines, currency);

        var lines = draftLines.Select((l, index) =>
        {
            Guid? productId = Guid.TryParse(l.ProductId, out var id) && id != Guid.Empty ? id : null;
            return new AdjustableRecurringDraftLineDto
            {
                Index = index,
                ProductId = productId?.ToString(),
                ProductName = productId.HasValue && productNames.TryGetValue(productId.Value, out var name)
                    ? name
                    : null,
                Designation = l.Designation ?? string.Empty,
                Quantity = l.Quantity,
                UnitPriceHT = l.UnitPriceHT,
                VatRate = l.VatRate,
                FodecApplicable = l.FodecApplicable
            };
        }).ToList();

        return new AdjustableRecurringDraftDto
        {
            BillingRunId = run.Id,
            InvoiceDraftId = draft.Id,
            ContractId = contract.Id,
            Currency = currency,
            PeriodFrom = run.PeriodFrom,
            PeriodTo = run.PeriodTo,
            ContractBillingFrequency = contract.BillingFrequency,
            IsConverted = draft.IsConverted,
            ExpiresAt = draft.ExpiresAt,
            Lines = lines,
            Totals = new AdjustableRecurringDraftTotalsDto
            {
                TotalHT = calculated.TotalHT,
                TotalVat = calculated.TotalVat,
                TotalFodec = calculated.TotalFodec,
                FiscalStampAmount = calculated.FiscalStampAmount,
                TotalTTC = calculated.TotalTTC,
                Currency = calculated.Currency
            }
        };
    }

    private static WizardStepLineDto ToWizardLine(DraftInvoiceLine line) => new()
    {
        ProductId = line.ProductId,
        Designation = line.Designation,
        Description = line.Description,
        Quantity = line.Quantity,
        Unit = line.Unit,
        UnitPriceHT = line.UnitPriceHT,
        PriceOverridden = line.PriceOverridden,
        DiscountType = line.DiscountType,
        DiscountValue = line.DiscountValue,
        VatRate = line.VatRate,
        FodecApplicable = line.FodecApplicable
    };
}
