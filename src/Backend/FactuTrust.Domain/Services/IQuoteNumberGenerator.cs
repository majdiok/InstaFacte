using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Interface for generating sequential quote numbers.
/// Ensures thread-safe, atomic number generation with no gaps.
/// </summary>
public interface IQuoteNumberGenerator
{
    /// <summary>
    /// Reserves the next quote number atomically.
    /// Uses database-level locking to prevent duplicates.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="prefix">Quote prefix (DEV, PRO, etc.)</param>
    /// <param name="fiscalYear">Fiscal year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The reserved quote number</returns>
    Task<QuoteNumber> ReserveNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next number without reserving it (for preview purposes).
    /// </summary>
    Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a number follows the expected sequence.
    /// </summary>
    Task<bool> ValidateSequenceIntegrityAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Quote number sequence tracking entity for atomic generation.
/// </summary>
public sealed class QuoteNumberSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Prefix { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public int CurrentSequence { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private QuoteNumberSequence() { }

    public static QuoteNumberSequence Create(Guid tenantId, string prefix, int fiscalYear)
    {
        return new QuoteNumberSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Prefix = prefix.ToUpperInvariant(),
            FiscalYear = fiscalYear,
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
