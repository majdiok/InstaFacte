using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

/// <summary>
/// Clone (T3/D6) et renouvellement manuel (T4/D7) des contrats récurrents.
/// </summary>
public sealed partial class RecurringContractService
{
    /// <summary>
    /// Clone un contrat en brouillon : en-tête copié, lignes actives uniquement recréées
    /// (EffectiveFrom = nouvelle StartDate, EffectiveTo = null, ordre préservé),
    /// SourceQuoteId non conservé, SetupFeeBilled réinitialisé, aucun run/avenant/usage copié.
    /// Le clonage d'un contrat clos (Cancelled/Expired) est autorisé (repartir d'un contrat clos).
    /// </summary>
    public async Task<Result<Guid>> CloneAsync(Guid id, CloneRecurringContractDto dto, CancellationToken cancellationToken = default)
    {
        var source = await _db.RecurringContracts.Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (source is null)
            return Result.Failure<Guid>(Error.NotFound("RecurringContract", id));

        var startDate = dto.StartDate?.Date ?? DateTime.UtcNow.Date;
        var endDate = source.EndDate.HasValue
            ? startDate + (source.EndDate.Value - source.StartDate)
            : (DateTime?)null;
        var clientId = dto.ClientId ?? source.ClientId;
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("ClientId", "Client introuvable"));

        var created = RecurringContract.CreateDraft(
            clientId, source.BillingFrequency, source.BillingDayOfMonth, startDate, endDate,
            source.AutoRenew, source.NoticePeriodDays, source.Currency,
            source.PaymentTermTemplateId, source.PriceListId, sourceQuoteId: null,
            dto.Reference ?? source.Reference, source.Notes);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        var clone = created.Value;
        var number = await GenerateContractNumberAsync(0, cancellationToken);
        clone.AssignNumber(number);

        foreach (var line in source.Lines.Where(l => l.IsActive).OrderBy(l => l.SortOrder))
        {
            var lineResult = clone.AddLine(
                line.LineType, line.Description, line.Quantity, line.UnitPriceHT, line.VatRate,
                line.ProductId, line.UsageMetricId, line.IncludedQuantity, line.OverageUnitPriceHT);
            if (lineResult.IsFailure)
                return Result.Failure<Guid>(lineResult.Error);
        }

        clone.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _db.RecurringContracts.AddAsync(clone, cancellationToken);
        var save = await SaveWithNumberRetryAsync(clone, cancellationToken);
        if (save.IsFailure) return Result.Failure<Guid>(save.Error);
        return Result.Success(clone.Id);
    }

    /// <summary>
    /// Renouvellement manuel (D7) : prolonge un contrat actif d'une durée identique à la durée
    /// initiale (calendaire quand la période initiale est calendaire), ou réactive un contrat
    /// expiré. Chaque renouvellement trace un avenant de type Renewal avec snapshots avant/après.
    /// Limitation phase 1 : les lignes dont EffectiveTo est dépassé ne sont pas rouvertes.
    /// </summary>
    public async Task<Result<DateTime>> RenewAsync(Guid id, RenewRecurringContractDto dto, CancellationToken cancellationToken = default)
    {
        var contract = await _db.RecurringContracts
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null)
            return Result.Failure<DateTime>(Error.NotFound("RecurringContract", id));

        var beforeJson = SerializeHeaderSnapshot(contract);
        var renew = contract.Renew(DateTime.UtcNow);
        if (renew.IsFailure)
            return Result.Failure<DateTime>(renew.Error);

        var amendment = RecurringContractAmendment.Create(
            id, RecurringContractAmendmentType.Renewal, DateTime.UtcNow.Date, ProrationPolicy.None,
            dto.Notes, beforeJson, SerializeHeaderSnapshot(contract), _currentUser.UserId);
        await _db.RecurringContractAmendments.AddAsync(amendment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(contract.EndDate!.Value);
    }
}
