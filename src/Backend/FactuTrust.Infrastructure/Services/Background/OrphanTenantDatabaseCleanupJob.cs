using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Job Hangfire de nettoyage des artefacts orphelins laissés par la mini-saga d'inscription
/// (voir <c>AuthController.Register</c>) lorsque le provisionnement de la base tenant échoue
/// après le commit des lignes maîtres, ou lorsque le processus est interrompu entre les deux
/// phases (crash serveur, redémarrage, etc.).
///
/// Deux passes indépendantes, chacune isolée des erreurs de l'autre :
///  1. Bases SQL physiques `FactuTrust_Tenant_*` sans aucune ligne <see cref="Tenant"/> associée
///     (le nom de la base a été alloué mais l'inscription n'a jamais committé les lignes maître,
///     ou les lignes ont depuis été supprimées) : suppression directe, sans risque puisque rien
///     ne les référence.
///  2. Lignes <see cref="Tenant"/> avec <see cref="TenantProvisioningStatus.Pending"/> ou
///     <see cref="TenantProvisioningStatus.Failed"/>, sans <c>TenantConnectionString</c> associée,
///     vieilles de plus de 24h : le provisionnement n'a manifestement pas abouti et n'aboutira
///     plus. On supprime l'utilisateur applicatif (via <see cref="UserManager{TUser}"/> pour que
///     Identity nettoie proprement rôles/claims/tokens), les <see cref="UserModuleGrant"/>, les
///     abonnements et la ligne tenant elle-même — ce qui libère l'email et le NIF pour une
///     nouvelle tentative d'inscription. Si une ligne Pending/Failed possède malgré tout une
///     <c>TenantConnectionString</c> (le flip de statut final a échoué après un provisionnement
///     par ailleurs réussi), on se contente de la promouvoir en <see cref="TenantProvisioningStatus.Ready"/>
///     plutôt que de détruire des données réelles.
/// </summary>
public sealed class OrphanTenantDatabaseCleanupJob
{
    private const string TenantDatabasePrefix = "FactuTrust_Tenant_";
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(24);

    private readonly Persistence.MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;
    private readonly UserManager<Persistence.ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrphanTenantDatabaseCleanupJob> _logger;

    public OrphanTenantDatabaseCleanupJob(
        Persistence.MasterDbContext masterContext,
        ITenantService tenantService,
        UserManager<Persistence.ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<OrphanTenantDatabaseCleanupJob> logger)
    {
        _masterContext = masterContext;
        _tenantService = tenantService;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await CleanupOrphanPhysicalDatabasesAsync(cancellationToken);
        await CleanupStaleUnprovisionedTenantsAsync(cancellationToken);
    }

    private async Task CleanupOrphanPhysicalDatabasesAsync(CancellationToken cancellationToken)
    {
        var masterConnectionString = _configuration.GetConnectionString("MasterConnection");
        if (string.IsNullOrWhiteSpace(masterConnectionString))
        {
            _logger.LogWarning("OrphanTenantDatabaseCleanupJob: MasterConnection introuvable ; passe base physique ignorée.");
            return;
        }

        List<string> physicalDatabaseNames;
        try
        {
            physicalDatabaseNames = await ListTenantDatabaseNamesAsync(masterConnectionString, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OrphanTenantDatabaseCleanupJob: échec du listage des bases tenant physiques ; passe ignorée pour cette exécution.");
            return;
        }

        if (physicalDatabaseNames.Count == 0)
            return;

        var referencedDatabaseNames = await _masterContext.Tenants
            .AsNoTracking()
            .Select(t => t.DatabaseName)
            .ToListAsync(cancellationToken);
        var referenced = new HashSet<string>(referencedDatabaseNames, StringComparer.OrdinalIgnoreCase);

        foreach (var databaseName in physicalDatabaseNames)
        {
            if (referenced.Contains(databaseName))
                continue; // encore réclamée par une ligne Tenant (Pending/Ready/Failed) — laissée à la passe suivante ou au provisionnement en cours.

            _logger.LogWarning(
                "OrphanTenantDatabaseCleanupJob: suppression de la base tenant orpheline {DatabaseName} (aucune ligne Tenant ne la référence).",
                databaseName);
            await _tenantService.TryDropDatabaseAsync(databaseName, cancellationToken);
        }
    }

    private static async Task<List<string>> ListTenantDatabaseNamesAsync(string masterConnectionString, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT name FROM sys.databases WHERE LEFT(name, @prefixLength) = @prefix",
            connection);
        command.Parameters.AddWithValue("@prefix", TenantDatabasePrefix);
        command.Parameters.AddWithValue("@prefixLength", TenantDatabasePrefix.Length);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));
        return result;
    }

    private async Task CleanupStaleUnprovisionedTenantsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - StaleThreshold;

        var connectedTenantIds = await _masterContext.TenantConnectionStrings
            .AsNoTracking()
            .Select(c => c.TenantId)
            .ToListAsync(cancellationToken);
        var connected = new HashSet<Guid>(connectedTenantIds);

        var staleTenants = await _masterContext.Tenants
            .Where(t => t.ProvisioningStatus != TenantProvisioningStatus.Ready && t.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var tenant in staleTenants)
        {
            if (connected.Contains(tenant.Id))
            {
                // Provisionnement probablement réussi ; seul le flip de statut final a échoué.
                // On répare au lieu de détruire des données réelles.
                tenant.MarkProvisioningReady();
                _logger.LogInformation(
                    "OrphanTenantDatabaseCleanupJob: tenant {TenantId} réparé en Ready (une TenantConnectionString existe déjà).",
                    tenant.Id);
                continue;
            }

            _logger.LogWarning(
                "OrphanTenantDatabaseCleanupJob: suppression du tenant {TenantId} resté {Status} depuis {CreatedAt} (aucune connexion tenant, provisionnement jamais finalisé).",
                tenant.Id,
                tenant.ProvisioningStatus,
                tenant.CreatedAt);

            var orphanUsers = await _masterContext.Users
                .Where(u => u.TenantId == tenant.Id)
                .ToListAsync(cancellationToken);

            foreach (var orphanUser in orphanUsers)
            {
                await _masterContext.UserModuleGrants
                    .Where(g => g.UserId == orphanUser.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                var deleteResult = await _userManager.DeleteAsync(orphanUser);
                if (!deleteResult.Succeeded)
                {
                    _logger.LogError(
                        "OrphanTenantDatabaseCleanupJob: échec de la suppression de l'utilisateur orphelin {UserId} (tenant {TenantId}) : {Errors}",
                        orphanUser.Id,
                        tenant.Id,
                        string.Join("; ", deleteResult.Errors.Select(e => e.Description)));
                }
            }

            await _masterContext.Subscriptions
                .Where(s => s.TenantId == tenant.Id)
                .ExecuteDeleteAsync(cancellationToken);

            await _tenantService.TryDropDatabaseAsync(tenant.DatabaseName, cancellationToken);

            _masterContext.Tenants.Remove(tenant);
        }

        if (staleTenants.Count > 0)
            await _masterContext.SaveChangesAsync(cancellationToken);
    }
}
