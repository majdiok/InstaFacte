using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Clôture d'un mois de feuilles de temps pour un cabinet.
/// </summary>
/// <remarks>
/// Une période clôturée n'accepte plus aucune écriture — création, modification, suppression,
/// validation ou dévalidation. Le déverrouillage est réservé au manager, exige un motif et
/// conserve la trace de l'auteur : c'est ce qui rend la période auditable en revue.
/// </remarks>
public sealed class FirmTimeSheetPeriodLock : Entity
{
    public Guid FirmTenantId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }

    public DateTime LockedAt { get; private set; }
    public Guid LockedByUserId { get; private set; }
    public string LockedByDisplayName { get; private set; } = null!;
    public string? LockReason { get; private set; }

    /// <summary>Faux tant que la période est ouverte ; l'historique de déverrouillage est conservé.</summary>
    public bool IsLocked { get; private set; }

    public DateTime? UnlockedAt { get; private set; }
    public Guid? UnlockedByUserId { get; private set; }
    public string? UnlockedByDisplayName { get; private set; }
    public string? UnlockReason { get; private set; }

    private FirmTimeSheetPeriodLock() { }

    public static Result<FirmTimeSheetPeriodLock> Create(
        Guid firmTenantId,
        int year,
        int month,
        Guid lockedByUserId,
        string lockedByDisplayName,
        string? reason = null)
    {
        var period = ValidatePeriod(firmTenantId, year, month);
        if (period.IsFailure)
            return Result.Failure<FirmTimeSheetPeriodLock>(period.Error);
        if (lockedByUserId == Guid.Empty)
            return Result.Failure<FirmTimeSheetPeriodLock>(Error.Validation("User", "Auteur de la clôture requis"));

        return Result.Success(new FirmTimeSheetPeriodLock
        {
            FirmTenantId = firmTenantId,
            Year = year,
            Month = month,
            IsLocked = true,
            LockedAt = DateTime.UtcNow,
            LockedByUserId = lockedByUserId,
            LockedByDisplayName = Normalize(lockedByDisplayName),
            LockReason = Trim(reason)
        });
    }

    public Result Lock(Guid userId, string displayName, string? reason = null)
    {
        if (IsLocked)
            return Result.Failure(Error.Validation("Period", "La période est déjà clôturée."));
        if (userId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Auteur de la clôture requis"));

        IsLocked = true;
        LockedAt = DateTime.UtcNow;
        LockedByUserId = userId;
        LockedByDisplayName = Normalize(displayName);
        LockReason = Trim(reason);
        UnlockedAt = null;
        UnlockedByUserId = null;
        UnlockedByDisplayName = null;
        UnlockReason = null;
        return Result.Success();
    }

    /// <summary>Rouvre la période. Le motif est obligatoire : c'est la pièce justificative de la réouverture.</summary>
    public Result Unlock(Guid userId, string displayName, string reason)
    {
        if (!IsLocked)
            return Result.Failure(Error.Validation("Period", "La période n'est pas clôturée."));
        if (userId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Auteur du déverrouillage requis"));
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("Reason", "Un motif est obligatoire pour rouvrir une période clôturée."));

        IsLocked = false;
        UnlockedAt = DateTime.UtcNow;
        UnlockedByUserId = userId;
        UnlockedByDisplayName = Normalize(displayName);
        UnlockReason = reason.Trim();
        return Result.Success();
    }

    private static Result ValidatePeriod(Guid firmTenantId, int year, int month)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure(Error.Validation("Tenant", "Cabinet requis"));
        if (year is < 2000 or > 2100)
            return Result.Failure(Error.Validation("Year", "Année invalide"));
        if (month is < 1 or > 12)
            return Result.Failure(Error.Validation("Month", "Mois invalide"));
        return Result.Success();
    }

    private static string Normalize(string? displayName)
    {
        var trimmed = displayName?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "Manager" : trimmed;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
