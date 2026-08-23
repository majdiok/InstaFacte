namespace FactuTrust.Domain.Enums;

/// <summary>
/// Persisted product-tour / first-login checklist status for an interactive user.
/// Existing production users must remain <see cref="Completed"/> (SQL default) so a release
/// does not surprise them with a tour.
/// </summary>
public enum ProductOnboardingStatus : byte
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Dismissed = 3
}
