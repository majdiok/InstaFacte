namespace FactuTrust.Domain.Entities;

/// <summary>
/// Disposition personnalisée du tableau de bord pour un utilisateur d'un tenant donné.
/// Stocke l'ordre des blocs sous forme JSON (tableau d'identifiants). Calquée sur
/// <see cref="DocumentTemplatePreference"/> (entité tenant simple), avec une clé unique
/// (TenantId, UserId). En l'absence de ligne, le frontend applique l'ordre par défaut.
/// </summary>
public sealed class UserDashboardLayout
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string LayoutJson { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private UserDashboardLayout() { }

    public static UserDashboardLayout Create(Guid tenantId, Guid userId, string layoutJson)
    {
        return new UserDashboardLayout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            LayoutJson = layoutJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void SetLayout(string layoutJson)
    {
        LayoutJson = layoutJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
