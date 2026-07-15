using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Marker entity recording that a demo dataset (seed) has been applied on a tenant database.
/// Kept as a simple audit row so the application can detect "already seeded" state and so that
/// existing historical migrations remain consistent with the EF Core model snapshot.
/// </summary>
public sealed class DemoDataset : Entity
{
    public string Version { get; private set; } = null!;
    public DateTime AppliedAtUtc { get; private set; }
    public Guid? AppliedByUserId { get; private set; }

    private DemoDataset() { }

    public static DemoDataset Record(string version, Guid? appliedByUserId = null, DateTime? appliedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La version du jeu de démonstration est obligatoire", nameof(version));
        if (version.Length > 32)
            throw new ArgumentException("La version du jeu de démonstration est limitée à 32 caractères", nameof(version));

        return new DemoDataset
        {
            Version = version.Trim(),
            AppliedByUserId = appliedByUserId,
            AppliedAtUtc = appliedAtUtc ?? DateTime.UtcNow
        };
    }
}
