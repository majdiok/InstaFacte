namespace FactuTrust.Domain.Constants;

/// <summary>
/// Constants for the default "client passager" (walk-in client) seeded in each new tenant database.
/// Used for POS and any context where a single default client is needed without creating a new one.
/// </summary>
public static class DefaultPassengerClient
{
    /// <summary>
    /// Display name of the default passenger client.
    /// </summary>
    public const string Name = "Client passager";

    /// <summary>
    /// Email used to identify the default passenger client. Unique per tenant; used for idempotent seed.
    /// </summary>
    public const string Email = "passager@factutrust.local";
}
