using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Interface for generating sequential invoice numbers.
/// Ensures thread-safe, atomic number generation with no gaps.
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// Reserves the next invoice number atomically.
    /// Uses database-level locking to prevent duplicates.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="prefix">Invoice prefix (FAC, AVO, etc.)</param>
    /// <param name="fiscalYear">Fiscal year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The reserved invoice number</returns>
    Task<InvoiceNumber> ReserveNextNumberAsync(
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
/// Invoice number sequence tracking entity for atomic generation.
/// </summary>
public sealed class InvoiceNumberSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Prefix { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public int CurrentSequence { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private InvoiceNumberSequence() { }

    public static InvoiceNumberSequence Create(Guid tenantId, string prefix, int fiscalYear)
    {
        return new InvoiceNumberSequence
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
