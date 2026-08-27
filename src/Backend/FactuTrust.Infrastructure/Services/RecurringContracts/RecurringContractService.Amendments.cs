using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

/// <summary>
/// Historique des avenants (T5) et édition des notes à tout statut (T13/D15).
/// </summary>
public sealed partial class RecurringContractService
{
    public async Task<IReadOnlyList<RecurringContractAmendmentDto>?> ListAmendmentsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await _db.RecurringContracts.AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists) return null;

        var amendments = await _db.RecurringContractAmendments.AsNoTracking()
            .Where(a => a.RecurringContractId == id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var names = await ResolveUserNamesAsync(
            amendments.Select(a => a.CreatedByUserId), cancellationToken);

        return amendments.Select(a => new RecurringContractAmendmentDto
        {
            Id = a.Id,
            Type = a.AmendmentType,
            TypeDisplay = a.AmendmentType.ToDisplayString(),
            EffectiveDate = a.EffectiveDate,
            ProrationPolicy = a.ProrationPolicy,
            ProrationPolicyDisplay = a.ProrationPolicy.ToDisplayString(),
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            CreatedByUserId = a.CreatedByUserId,
            CreatedByUserName = a.CreatedByUserId.HasValue
                ? names.GetValueOrDefault(a.CreatedByUserId.Value)
                : null
        }).ToList();
    }

    public async Task<RecurringContractAmendmentDetailDto?> GetAmendmentAsync(
        Guid id, Guid amendmentId, CancellationToken cancellationToken = default)
    {
        var amendment = await _db.RecurringContractAmendments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.RecurringContractId == id && a.Id == amendmentId, cancellationToken);
        if (amendment is null) return null;

        var names = await ResolveUserNamesAsync(new[] { amendment.CreatedByUserId }, cancellationToken);

        return new RecurringContractAmendmentDetailDto
        {
            Id = amendment.Id,
            RecurringContractId = amendment.RecurringContractId,
            Type = amendment.AmendmentType,
            TypeDisplay = amendment.AmendmentType.ToDisplayString(),
            EffectiveDate = amendment.EffectiveDate,
            ProrationPolicy = amendment.ProrationPolicy,
            ProrationPolicyDisplay = amendment.ProrationPolicy.ToDisplayString(),
            Notes = amendment.Notes,
            CreatedAt = amendment.CreatedAt,
            CreatedByUserId = amendment.CreatedByUserId,
            CreatedByUserName = amendment.CreatedByUserId.HasValue
                ? names.GetValueOrDefault(amendment.CreatedByUserId.Value)
                : null,
            SnapshotBeforeJson = amendment.SnapshotBeforeJson,
            SnapshotAfterJson = amendment.SnapshotAfterJson
        };
    }

    /// <summary>Notes éditables à tout statut (D15) — aucune garde de statut côté domaine.</summary>
    public async Task<Result> UpdateNotesAsync(Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        var contract = await _db.RecurringContracts
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return Result.Failure(Error.NotFound("RecurringContract", id));

        contract.UpdateNotes(notes);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Résolution best-effort des noms d'utilisateurs via la base master (D13) :
    /// jamais d'échec de la requête pour un nom — absent du dictionnaire → null.
    /// La liste d'avenants d'un contrat est petite : pas de batch dédié en phase 1.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(
        IEnumerable<Guid?> userIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var userId in userIds.Where(u => u.HasValue).Select(u => u!.Value).Distinct())
        {
            try
            {
                var member = await _members.GetMemberAsync(userId, cancellationToken);
                if (member.IsSuccess)
                    names[userId] = member.Value.DisplayName;
            }
            catch
            {
                // Best-effort : un annuaire indisponible ne doit pas faire échouer la lecture.
            }
        }
        return names;
    }
}
