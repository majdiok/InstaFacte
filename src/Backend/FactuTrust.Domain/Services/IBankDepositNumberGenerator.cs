using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Generates sequential REM-YYYY-NNNNNN numbers per tenant and fiscal year.
/// </summary>
public interface IBankDepositNumberGenerator
{
    Task<BankDepositNumber> ReserveNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sequence row for bank deposit numbers (REM prefix).
/// </summary>
public sealed class BankDepositNumberSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int FiscalYear { get; private set; }
    public int CurrentSequence { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private BankDepositNumberSequence() { }

    public static BankDepositNumberSequence Create(Guid tenantId, int fiscalYear)
    {
        return new BankDepositNumberSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
