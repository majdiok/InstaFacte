using System.Diagnostics;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Service for managing tenant databases.
/// </summary>
public sealed class TenantService : ITenantService
{
    // La chaîne de connexion d'un tenant ne change qu'à sa création : le cache évite
    // un aller-retour base master + un déchiffrement DataProtection à CHAQUE requête
    // authentifiée (TenantMiddleware). TTL de sûreté aligné sur TenantMigrationGuard.
    private const string ConnectionStringCacheKeyPrefix = "TenantService.ConnectionString.";
    private static readonly TimeSpan ConnectionStringCacheDuration = TimeSpan.FromMinutes(15);

    private readonly MasterDbContext _masterContext;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantService> _logger;
    private readonly TenantDatabaseProvisioner _provisioner;
    private readonly ISectorDataTemplateApplier? _sectorTemplateApplier;

    public TenantService(
        MasterDbContext masterContext,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<TenantService> logger,
        TenantDatabaseProvisioner provisioner,
        ISectorDataTemplateApplier? sectorTemplateApplier = null)
    {
        _masterContext = masterContext;
        _protector = dataProtectionProvider.CreateProtector("TenantConnectionStrings");
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _provisioner = provisioner;
        _sectorTemplateApplier = sectorTemplateApplier;
    }

    public async Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{ConnectionStringCacheKeyPrefix}{tenantId}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var tenantConnection = await _masterContext.TenantConnectionStrings
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenantConnection is null)
            return null; // Échec non mis en cache : un tenant tout juste provisionné doit être vu immédiatement.

        try
        {
            var connectionString = _protector.Unprotect(tenantConnection.EncryptedConnectionString);
            _cache.Set(cacheKey, connectionString, ConnectionStringCacheDuration);
            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt connection string for tenant {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<string> CreateTenantDatabaseAsync(Guid tenantId, string databaseName, string? warehouseName = null, CancellationToken cancellationToken = default)
    {
        var masterConnectionString = GetMasterConnectionString();

        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };

        var tenantConnectionString = builder.ConnectionString;

        var sw = Stopwatch.StartNew();
        await _provisioner.ProvisionNewTenantDatabaseAsync(
            masterConnectionString,
            databaseName,
            tenantId,
            cancellationToken);
        LogProvisionStep("Provision", sw.ElapsedMilliseconds, tenantId, databaseName);

        sw.Restart();
        await SeedDefaultWarehouseAsync(tenantConnectionString, warehouseName, cancellationToken);
        LogProvisionStep("SeedWarehouse", sw.ElapsedMilliseconds, tenantId, databaseName);

        sw.Restart();
        await SeedDefaultPassengerClientAsync(tenantConnectionString, cancellationToken);
        LogProvisionStep("SeedPassengerClient", sw.ElapsedMilliseconds, tenantId, databaseName);

        sw.Restart();
        await SeedWithholdingTaxSystemTypesAsync(tenantConnectionString, cancellationToken);
        LogProvisionStep("SeedWithholding", sw.ElapsedMilliseconds, tenantId, databaseName);

        sw.Restart();
        await ApplySectorDataTemplatesAsync(tenantId, tenantConnectionString, cancellationToken);
        LogProvisionStep("SectorTemplates", sw.ElapsedMilliseconds, tenantId, databaseName);

        var encryptedConnectionString = _protector.Protect(tenantConnectionString);

        var tenantConnection = new TenantConnectionString
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EncryptedConnectionString = encryptedConnectionString,
            CreatedAt = DateTime.UtcNow
        };

        _masterContext.TenantConnectionStrings.Add(tenantConnection);
        await _masterContext.SaveChangesAsync(cancellationToken);
        _cache.Remove($"{ConnectionStringCacheKeyPrefix}{tenantId}");
        TenantMigrationGuard.MarkApplied(_cache, tenantId);

        _logger.LogInformation(
            "Created database {DatabaseName} for tenant {TenantId} with default warehouse, passenger client, and withholding tax catalog",
            databaseName,
            tenantId);

        return tenantConnectionString;
    }

    public async Task<string> CreateAccountingFirmDatabaseAsync(Guid tenantId, string databaseName, CancellationToken cancellationToken = default)
    {
        var masterConnectionString = GetMasterConnectionString();

        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };

        var tenantConnectionString = builder.ConnectionString;

        await _provisioner.ProvisionNewTenantDatabaseAsync(
            masterConnectionString,
            databaseName,
            tenantId,
            cancellationToken);

        var encryptedConnectionString = _protector.Protect(tenantConnectionString);

        var tenantConnection = new TenantConnectionString
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EncryptedConnectionString = encryptedConnectionString,
            CreatedAt = DateTime.UtcNow
        };

        _masterContext.TenantConnectionStrings.Add(tenantConnection);
        await _masterContext.SaveChangesAsync(cancellationToken);
        _cache.Remove($"{ConnectionStringCacheKeyPrefix}{tenantId}");
        TenantMigrationGuard.MarkApplied(_cache, tenantId);

        _logger.LogInformation("Created lightweight database {DatabaseName} for accounting firm tenant {TenantId}", databaseName, tenantId);

        return tenantConnectionString;
    }

    public Task TryDropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var masterConnectionString = GetMasterConnectionString();
        return _provisioner.TryDropDatabaseAsync(masterConnectionString, databaseName, cancellationToken);
    }

    public Task EnsureTenantTemplateAsync(CancellationToken cancellationToken = default)
    {
        var masterConnectionString = GetMasterConnectionString();
        return _provisioner.EnsureTenantTemplateAsync(masterConnectionString, cancellationToken);
    }

    private string GetMasterConnectionString() =>
        _configuration.GetConnectionString("MasterConnection")
        ?? throw new InvalidOperationException("Chaîne de connexion maître introuvable");

    public async Task<bool> DatabaseExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connectionString = await GetConnectionStringAsync(tenantId, cancellationToken);
        
        if (string.IsNullOrEmpty(connectionString))
            return false;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task ApplyMigrationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connectionString = await GetConnectionStringAsync(tenantId, cancellationToken);
        
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException($"Chaîne de connexion introuvable pour l'entreprise {tenantId}");

        await ApplyMigrationsToNewDatabaseAsync(connectionString, cancellationToken);
        _cache.Remove($"{TenantMigrationGuard.CacheKeyPrefix}{tenantId}");
    }

    private void LogProvisionStep(string step, long durationMs, Guid tenantId, string databaseName)
    {
        _logger.LogInformation(
            "TenantProvision.Step={Step} DurationMs={DurationMs} TenantId={TenantId} DatabaseName={DatabaseName}",
            step,
            durationMs,
            tenantId,
            databaseName);
    }

    private async Task ApplyMigrationsToNewDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var context = new TenantDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task SeedDefaultWarehouseAsync(string connectionString, string? warehouseName, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TenantDbContext(options);

        // Check if any warehouse already exists
        var existingDefault = await context.Warehouses.FirstOrDefaultAsync(w => w.IsDefault, cancellationToken);
        if (existingDefault != null)
        {
            // If user provided a specific name, ensure it is set
            if (!string.IsNullOrWhiteSpace(warehouseName))
            {
                var trimmedName = warehouseName.Trim();
                if (existingDefault.Name != trimmedName)
                {
                    _logger.LogInformation("Updating default warehouse name from '{OldName}' to '{NewName}'", existingDefault.Name, trimmedName);
                    existingDefault.Update(trimmedName, existingDefault.Address);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
            
            _logger.LogDebug("Default warehouse already exists");
            return;
        }

        // Create default warehouse with user-provided name or default
        var defaultName = string.IsNullOrWhiteSpace(warehouseName) 
            ? "Entrepôt Principal" 
            : warehouseName.Trim();

        var warehouseResult = Domain.Entities.Warehouse.Create(
            code: "PRINCIPAL",
            name: defaultName,
            address: null,
            isDefault: true);

        if (warehouseResult.IsFailure)
        {
            _logger.LogError("Failed to create default warehouse: {Error}", warehouseResult.Error.Description);
            return;
        }

        context.Warehouses.Add(warehouseResult.Value);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default warehouse '{WarehouseName}' for new tenant", defaultName);
    }

    private async Task SeedDefaultPassengerClientAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TenantDbContext(options);

        var passengerEmail = DefaultPassengerClient.Email.Trim().ToLowerInvariant();
        var alreadyExists = await context.Clients
            .AnyAsync(c => EF.Property<string>(c.Email, "Value") == passengerEmail, cancellationToken);
        if (alreadyExists)
        {
            _logger.LogDebug("Default passenger client already exists");
            return;
        }

        var addressResult = Address.Create("Non renseigné", "Tunis", "Tunis");
        if (addressResult.IsFailure)
        {
            _logger.LogError("Failed to create address for default passenger client: {Error}", addressResult.Error.Description);
            return;
        }

        var emailResult = Email.Create(DefaultPassengerClient.Email);
        if (emailResult.IsFailure)
        {
            _logger.LogError("Failed to create email for default passenger client: {Error}", emailResult.Error.Description);
            return;
        }

        var clientResult = Client.Create(
            DefaultPassengerClient.Name,
            ClientType.Individual,
            addressResult.Value,
            emailResult.Value,
            nif: null,
            phone: null,
            contactPerson: null,
            notes: null);

        if (clientResult.IsFailure)
        {
            _logger.LogError("Failed to create default passenger client: {Error}", clientResult.Error.Description);
            return;
        }

        var client = clientResult.Value;
        client.SetAuditInfo("system");
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default passenger client '{ClientName}' for new tenant", DefaultPassengerClient.Name);
    }

    /// <summary>
    /// Applique les modèles de données sectoriels correspondant à la classification du tenant
    /// (plan §WP-B6). Additif uniquement, jamais bloquant : un échec est journalisé (sans PII) et
    /// n'empêche pas la création du tenant — le modèle pourra être réappliqué plus tard (WP-B7).
    /// Aucun effet quand <see cref="_sectorTemplateApplier"/> est absent (DI non configurée, ex.
    /// tests instanciant <see cref="TenantService"/> directement) ou quand le tenant n'a pas encore
    /// de classification sectorielle (dossiers comptables : <see cref="CreateAccountingFirmDatabaseAsync"/>
    /// ne l'appelle pas du tout).
    /// </summary>
    private async Task ApplySectorDataTemplatesAsync(Guid tenantId, string connectionString, CancellationToken cancellationToken)
    {
        if (_sectorTemplateApplier is null)
            return;

        try
        {
            var tenant = await _masterContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.CompanySegment, t.BusinessDomain })
                .FirstOrDefaultAsync(cancellationToken);

            if (tenant is null)
                return;

            await _sectorTemplateApplier.ApplyAsync(
                tenantId,
                connectionString,
                tenant.CompanySegment,
                tenant.BusinessDomain,
                dryRun: false,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sector template application failed for tenant {TenantId}", tenantId);
        }
    }

    private async Task SeedWithholdingTaxSystemTypesAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var context = new TenantDbContext(options);
        await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context, cancellationToken);
        await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
    }
}

