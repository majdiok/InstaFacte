namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Plan-limit keys for low-code Studio quotas + generous fallbacks used when a tenant's plan rows
/// predate these keys (existing installs that won't be re-seeded). Keys must match PlanSeeder.
/// </summary>
public static class StudioQuotas
{
    public const string MaxEntitiesKey = "MaxCustomEntities";
    public const string MaxFieldsKey = "MaxCustomFieldsPerEntity";
    public const string MaxRecordsKey = "MaxCustomRecordsPerEntity";

    public const int MaxEntitiesFallback = 50;
    public const int MaxFieldsFallback = 100;
    public const int MaxRecordsFallback = 100_000;
}
