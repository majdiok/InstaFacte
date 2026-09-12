using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>Singleton tenant settings for the Timesheets app (Odoo Timesheets Configuration).</summary>
public sealed class TenantTimesheetSettings : AggregateRoot
{
    public bool BillingRateIndicatorsEnabled { get; private set; }
    public bool BillingRateLeaderboardEnabled { get; private set; }
    public bool TimeOffEntriesEnabled { get; private set; }
    public TimesheetEncodingMethod EncodingMethod { get; private set; }
    public Guid? TimeOffProjectId { get; private set; }
    public Guid? TimeOffTaskId { get; private set; }
    public decimal DefaultDailyWorkingHours { get; private set; }

    private TenantTimesheetSettings() { }

    public static TenantTimesheetSettings CreateDefault() => new()
    {
        BillingRateIndicatorsEnabled = false,
        BillingRateLeaderboardEnabled = false,
        TimeOffEntriesEnabled = false,
        EncodingMethod = TimesheetEncodingMethod.Hours,
        DefaultDailyWorkingHours = 8m
    };

    public Result Update(
        bool billingRateIndicatorsEnabled,
        bool billingRateLeaderboardEnabled,
        bool timeOffEntriesEnabled,
        TimesheetEncodingMethod encodingMethod,
        Guid? timeOffProjectId,
        Guid? timeOffTaskId,
        decimal defaultDailyWorkingHours)
    {
        if (defaultDailyWorkingHours <= 0 || defaultDailyWorkingHours > 24)
            return Result.Failure(Error.Validation("DefaultDailyWorkingHours", "Les heures ouvrées doivent être entre 0 et 24"));

        BillingRateIndicatorsEnabled = billingRateIndicatorsEnabled;
        BillingRateLeaderboardEnabled = billingRateLeaderboardEnabled;
        TimeOffEntriesEnabled = timeOffEntriesEnabled;
        EncodingMethod = encodingMethod;
        TimeOffProjectId = timeOffProjectId == Guid.Empty ? null : timeOffProjectId;
        TimeOffTaskId = timeOffTaskId == Guid.Empty ? null : timeOffTaskId;
        DefaultDailyWorkingHours = decimal.Round(defaultDailyWorkingHours, 2);
        return Result.Success();
    }
}
