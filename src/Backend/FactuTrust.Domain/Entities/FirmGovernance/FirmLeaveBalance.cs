using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Solde d'ouverture / ajustement par collaborateur et année.
/// Consommé et restant sont calculés hors agrégat à partir des demandes Approuvées.
/// </summary>
public sealed class FirmLeaveBalance : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public decimal OpeningBalanceDays { get; private set; }
    public decimal AdjustmentDays { get; private set; }
    public string? Notes { get; private set; }

    private FirmLeaveBalance() { }

    public static Result<FirmLeaveBalance> Create(
        Guid firmTenantId,
        Guid userId,
        int year,
        decimal openingBalanceDays = 0m,
        decimal adjustmentDays = 0m,
        string? notes = null)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmLeaveBalance>(Error.Validation("Tenant", "Cabinet requis."));
        if (userId == Guid.Empty)
            return Result.Failure<FirmLeaveBalance>(Error.Validation("UserId", "Collaborateur requis."));
        if (year < 2000 || year > 2100)
            return Result.Failure<FirmLeaveBalance>(Error.Validation("Year", "Année invalide."));

        return Result.Success(new FirmLeaveBalance
        {
            FirmTenantId = firmTenantId,
            UserId = userId,
            Year = year,
            OpeningBalanceDays = Math.Round(openingBalanceDays, 3),
            AdjustmentDays = Math.Round(adjustmentDays, 3),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
    }

    public Result Set(decimal openingBalanceDays, decimal adjustmentDays, string? notes)
    {
        OpeningBalanceDays = Math.Round(openingBalanceDays, 3);
        AdjustmentDays = Math.Round(adjustmentDays, 3);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return Result.Success();
    }

    public static decimal ComputeRemaining(decimal opening, decimal adjustment, decimal consumed) =>
        Math.Round(opening + adjustment - consumed, 3, MidpointRounding.AwayFromZero);
}
