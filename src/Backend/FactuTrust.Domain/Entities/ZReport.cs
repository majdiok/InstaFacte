using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Immutable Z-close snapshot for a <see cref="CashRegisterSession"/>.
/// </summary>
public sealed class ZReport : AggregateRoot
{
    public ZReportNumber Number { get; private set; } = null!;
    public Guid CashRegisterSessionId { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public string SnapshotJson { get; private set; } = null!;

    private ZReport() { }

    public static Result<ZReport> Create(
        ZReportNumber number,
        Guid cashRegisterSessionId,
        string snapshotJson)
    {
        if (number is null)
            return Result.Failure<ZReport>(
                Error.Validation("Number", "Le numéro de clôture Z est obligatoire"));

        if (cashRegisterSessionId == Guid.Empty)
            return Result.Failure<ZReport>(
                Error.Validation("CashRegisterSessionId", "La session de caisse est obligatoire"));

        if (string.IsNullOrWhiteSpace(snapshotJson))
            return Result.Failure<ZReport>(
                Error.Validation("SnapshotJson", "Le snapshot de clôture Z est obligatoire"));

        var report = new ZReport
        {
            Number = number,
            CashRegisterSessionId = cashRegisterSessionId,
            GeneratedAt = DateTime.UtcNow,
            SnapshotJson = snapshotJson
        };

        return Result.Success(report);
    }
}
