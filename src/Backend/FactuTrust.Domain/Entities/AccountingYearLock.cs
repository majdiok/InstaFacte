using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Verrouillage définitif (irréversible) d'un exercice comptable. Une fois posé, il bloque la
/// réouverture des périodes de l'exercice et toute création d'écriture sur cet exercice. Entité
/// dédiée (l'entité <see cref="FiscalYear"/> n'ayant aucun DbSet), une ligne par exercice verrouillé.
/// </summary>
public sealed class AccountingYearLock : Entity
{
    public int FiscalYear { get; private set; }
    public DateTime LockedAt { get; private set; }
    public string LockedBy { get; private set; } = null!;

    private AccountingYearLock() { }

    public static AccountingYearLock Create(int fiscalYear, string lockedBy)
    {
        return new AccountingYearLock
        {
            Id = Guid.NewGuid(),
            FiscalYear = fiscalYear,
            LockedAt = DateTime.UtcNow,
            LockedBy = string.IsNullOrWhiteSpace(lockedBy) ? "system" : lockedBy.Trim()
        };
    }
}
