using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Factory interface for creating tenant-specific DbContext instances.
/// This interface allows for easier testing by enabling mocking.
/// </summary>
public interface ITenantDbContextFactory
{
    /// <summary>
    /// Creates a new TenantDbContext instance for the current tenant.
    /// Si une transaction ambiante (<see cref="TenantAmbientTransaction"/>) est active,
    /// le contexte retourné est enrôlé sur cette connexion/transaction.
    /// </summary>
    TenantDbContext CreateContext();

    /// <summary>
    /// Creates a context that NEVER joins the ambient transaction.
    /// Obligatoire pour les services qui ouvrent leur propre transaction
    /// (audit hash-chaîné, réservation de numéros, périodes comptables…).
    /// Implémentation par défaut : identique à <see cref="CreateContext"/>
    /// (les factories de test sans transaction ambiante n'ont rien à faire).
    /// </summary>
    TenantDbContext CreateIsolatedContext() => CreateContext();
}
