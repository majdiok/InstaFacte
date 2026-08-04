using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Paramètres annuels du module congés cabinet (distincts des settings timesheet).</summary>
public sealed class FirmLeaveSettings : Entity
{
    public Guid FirmTenantId { get; private set; }
    public int Year { get; private set; }
    public decimal DefaultAnnualPaidDays { get; private set; } = 30m;
    public bool AllowHalfDays { get; private set; } = true;
    public int MinNoticeDays { get; private set; }
    public bool BlockOverlap { get; private set; } = true;
    public bool CarryOverEnabled { get; private set; }
    public decimal MaxCarryOverDays { get; private set; }

    private FirmLeaveSettings() { }

    public static Result<FirmLeaveSettings> Create(Guid firmTenantId, int year, decimal defaultAnnualPaidDays = 30m)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmLeaveSettings>(Error.Validation("Tenant", "Cabinet requis."));
        if (year < 2000 || year > 2100)
            return Result.Failure<FirmLeaveSettings>(Error.Validation("Year", "Année invalide."));

        return Result.Success(new FirmLeaveSettings
        {
            FirmTenantId = firmTenantId,
            Year = year,
            DefaultAnnualPaidDays = Math.Max(0, Math.Round(defaultAnnualPaidDays, 3)),
            AllowHalfDays = true,
            BlockOverlap = true
        });
    }

    public Result Update(
        decimal defaultAnnualPaidDays,
        bool allowHalfDays,
        int minNoticeDays,
        bool blockOverlap,
        bool carryOverEnabled,
        decimal maxCarryOverDays)
    {
        if (minNoticeDays < 0)
            return Result.Failure(Error.Validation("MinNoticeDays", "Le préavis ne peut pas être négatif."));
        if (maxCarryOverDays < 0)
            return Result.Failure(Error.Validation("MaxCarryOverDays", "Le report max ne peut pas être négatif."));

        DefaultAnnualPaidDays = Math.Max(0, Math.Round(defaultAnnualPaidDays, 3));
        AllowHalfDays = allowHalfDays;
        MinNoticeDays = minNoticeDays;
        BlockOverlap = blockOverlap;
        CarryOverEnabled = carryOverEnabled;
        MaxCarryOverDays = Math.Round(maxCarryOverDays, 3);
        return Result.Success();
    }
}
