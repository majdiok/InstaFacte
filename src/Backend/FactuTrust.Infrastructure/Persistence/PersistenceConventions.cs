using FactuTrust.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Conventions transverses appliquées à la fin d'<see cref="DbContext.OnModelCreating"/>
/// dans <see cref="TenantDbContext"/> et <see cref="MasterDbContext"/>.
/// </summary>
internal static class PersistenceConventions
{
    /// <summary>
    /// Marque comme <see cref="ValueGenerated.Never"/> toutes les clés primaires <c>Id</c>
    /// de type <see cref="System.Guid"/> portées par un type dérivé de <see cref="Entity"/>.
    ///
    /// Justification : le constructeur d'<see cref="Entity"/> initialise <c>Id = Guid.NewGuid()</c>.
    /// Or, la convention EF Core mappe par défaut ces clés en <see cref="ValueGenerated.OnAdd"/>.
    /// Le change-tracker considère alors, lors d'un <c>DetectChanges</c> découvrant un enfant non
    /// suivi via une navigation d'un parent suivi, que la clé « déjà positionnée » signifie une
    /// ligne préexistante — l'entité est trackée en <c>Modified</c>, un UPDATE est émis sur un
    /// <c>Id</c> qui n'existe pas encore, et EF Core lève un <c>DbUpdateConcurrencyException</c>
    /// (traduit en HTTP 409 <c>CONCURRENCY_CONFLICT</c> par le middleware global).
    ///
    /// Cf. « DetectChanges honors store-generated key values » dans la doc EF Core 3.0+ :
    /// https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-3.x/breaking-changes#detectchanges-honors-store-generated-key-values
    ///
    /// Périmètre volontairement restreint :
    /// - types <c>owned</c> exclus (les Money/valeur-objets ont leurs propres colonnes) ;
    /// - types Identity ASP.NET (qui n'héritent pas d'<see cref="Entity"/>) intacts ;
    /// - clés composites intactes ;
    /// - clés non nommées <c>Id</c> ou non-<see cref="System.Guid"/> intactes.
    ///
    /// Ne change AUCUN DDL : les colonnes existantes n'ont pas de <c>DEFAULT NEWID()</c>.
    /// N'affecte pas <c>DbSet.Add/Update/Attach</c> — seulement le chemin de découverte
    /// via <c>NavigationFixer.AttachGraph</c>.
    /// </summary>
    public static void ApplyClientGeneratedGuidKeys(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType)) continue;

            var key = entityType.FindPrimaryKey();
            if (key is null || key.Properties.Count != 1) continue;

            var pk = key.Properties[0];
            if (pk.Name != nameof(Entity.Id)) continue;
            if (pk.ClrType != typeof(System.Guid)) continue;

            pk.ValueGenerated = ValueGenerated.Never;
        }
    }
}
