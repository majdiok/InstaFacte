using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Interface for generating sequential cash operation numbers for a tenant.
/// Ensures thread-safe atomic generation (gap-free) using database-level locking.
/// Maintains separate sequences for DEP (debit) and ENC (credit) prefixes.
/// </summary>
public interface ICashOperationNumberGenerator
{
    Task<CashOperationNumber> ReserveNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CashOperationType operationType,
        CancellationToken cancellationToken = default);

    Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CashOperationType operationType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cash operation number sequence tracking entity for atomic generation.
/// Sequences are kept per tenant/year/prefix to allow independent numbering.
/// </summary>
public sealed class CashOperationNumberSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int FiscalYear { get; private set; }

    /// <summary>
    /// The prefix this sequence tracks (DEP or ENC).
    /// </summary>
    public string Prefix { get; private set; } = null!;

    public int CurrentSequence { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CashOperationNumberSequence() { }

    public static CashOperationNumberSequence Create(Guid tenantId, int fiscalYear, string prefix)
    {
        return new CashOperationNumberSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYear = fiscalYear,
            Prefix = prefix,
            CurrentSequence = 0,
            LastUpdated = DateTime.UtcNow
        };
    }

    public int IncrementAndGet()
    {
        CurrentSequence++;
        LastUpdated = DateTime.UtcNow;
        return CurrentSequence;
    }
}
