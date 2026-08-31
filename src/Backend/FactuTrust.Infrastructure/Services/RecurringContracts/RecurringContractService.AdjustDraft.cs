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
/// Whitelist : désignation, quantité, prix unitaire HT. Aucune émission, aucun journal.
/// </summary>
public sealed partial class RecurringContractService
{
    private static readonly TimeSpan SubmissionLockTimeout = TimeSpan.FromMinutes(5);

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
                PriceOverridden = true
            });
        }

        draft.UpdateLines(merged);
        await _db.SaveChangesAsync(cancellationToken);

        // Recharger le contrat (AsNoTracking) pour le mapping — le run n'a pas changé.
        var mapped = await LoadAdjustableAsync(run.Id, trackDraft: false, cancellationToken);
        if (mapped.IsFailure)
            return Result.Failure<AdjustableRecurringDraftDto>(mapped.Error);

        return Result.Success(await MapAdjustableDraftAsync(mapped.Value, cancellationToken));
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
