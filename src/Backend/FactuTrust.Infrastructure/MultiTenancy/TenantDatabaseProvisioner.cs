using System.Diagnostics;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Creates tenant databases via template RESTORE or full EF migrations.
/// </summary>
public sealed class TenantDatabaseProvisioner
{
    private readonly IOptions<TenantProvisioningOptions> _options;
    private readonly ILogger<TenantDatabaseProvisioner> _logger;

    public TenantDatabaseProvisioner(
        IOptions<TenantProvisioningOptions> options,
        ILogger<TenantDatabaseProvisioner> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task ProvisionNewTenantDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (opts.Strategy == TenantProvisioningStrategy.TemplateClone)
        {
            try
            {
                await ProvisionFromTemplateAsync(masterConnectionString, databaseName, tenantId, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Template clone failed for {DatabaseName}; falling back to Migrate. TenantId={TenantId}",
                    databaseName,
                    tenantId);
            }
        }

        await ProvisionWithMigrationsAsync(masterConnectionString, databaseName, tenantId, cancellationToken);
    }

    public async Task EnsureTenantTemplateAsync(string masterConnectionString, CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var templateName = opts.TemplateDatabaseName;
        var backupPath = ResolveBackupPath(masterConnectionString);

        if (!await DatabaseExistsAsync(masterConnectionString, templateName, cancellationToken))
        {
            _logger.LogInformation("Creating tenant template database {TemplateName}", templateName);
            await CreateEmptyDatabaseAsync(masterConnectionString, templateName, cancellationToken);
            var templateConn = BuildConnectionString(masterConnectionString, templateName);
            await ApplyMigrationsAsync(templateConn, templateName, null, cancellationToken);
        }
        else
        {
            var templateConn = BuildConnectionString(masterConnectionString, templateName);
            await ApplyMigrationsAsync(templateConn, templateName, null, cancellationToken);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        await BackupDatabaseAsync(masterConnectionString, templateName, backupPath, cancellationToken);
        _logger.LogInformation("Tenant template backup refreshed at {BackupPath}", backupPath);
    }

    public async Task TryDropDatabaseAsync(string masterConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        if (!_options.Value.CleanupOrphanDatabasesOnFailure)
            return;

        try
        {
            var sql = $@"
IF EXISTS (SELECT * FROM sys.databases WHERE name = N'{EscapeSqlIdentifier(databaseName)}')
BEGIN
    ALTER DATABASE [{EscapeSqlIdentifier(databaseName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{EscapeSqlIdentifier(databaseName)}];
END";

            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Dropped orphan tenant database {DatabaseName}", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to drop orphan database {DatabaseName}", databaseName);
        }
    }

    private async Task ProvisionFromTemplateAsync(
        string masterConnectionString,
        string databaseName,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await EnsureTenantTemplateAsync(masterConnectionString, cancellationToken);

        var backupPath = ResolveBackupPath(masterConnectionString);
        if (!File.Exists(backupPath))
            throw new InvalidOperationException($"Template backup not found at {backupPath}");

        if (await DatabaseExistsAsync(masterConnectionString, databaseName, cancellationToken))
            return;

        var (dataLogical, logLogical) = await GetBackupFileListAsync(masterConnectionString, backupPath, cancellationToken);
        var dataDir = ResolveDataFileDirectory(masterConnectionString);
        Directory.CreateDirectory(dataDir);

        var dataFile = Path.Combine(dataDir, $"{databaseName}.mdf");
        var logFile = Path.Combine(dataDir, $"{databaseName}_log.ldf");

        var restoreSql = $@"
RESTORE DATABASE [{EscapeSqlIdentifier(databaseName)}]
FROM DISK = @backupPath
WITH
    MOVE @dataLogical TO @dataFile,
    MOVE @logLogical TO @logFile,
    REPLACE,
    STATS = 10";

        await using (var connection = new SqlConnection(masterConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(restoreSql, connection) { CommandTimeout = 300 };
            command.Parameters.AddWithValue("@backupPath", backupPath);
            command.Parameters.AddWithValue("@dataLogical", dataLogical);
            command.Parameters.AddWithValue("@logLogical", logLogical);
            command.Parameters.AddWithValue("@dataFile", dataFile);
            command.Parameters.AddWithValue("@logFile", logFile);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        LogStep("RestoreFromTemplate", sw.ElapsedMilliseconds, tenantId);

        var tenantConn = BuildConnectionString(masterConnectionString, databaseName);
        await ApplyMigrationsAsync(tenantConn, databaseName, tenantId, cancellationToken);
    }

    private async Task ProvisionWithMigrationsAsync(
        string masterConnectionString,
        string databaseName,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await CreateEmptyDatabaseAsync(masterConnectionString, databaseName, cancellationToken);
        LogStep("CreateDatabase", sw.ElapsedMilliseconds, tenantId);

        sw.Restart();
        var tenantConn = BuildConnectionString(masterConnectionString, databaseName);
        await ApplyMigrationsAsync(tenantConn, databaseName, tenantId, cancellationToken);
        LogStep("MigrateAsync", sw.ElapsedMilliseconds, tenantId);
    }

    private async Task ApplyMigrationsAsync(
        string connectionString,
        string databaseName,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;

        await using var context = new TenantDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
        LogStep("ApplyMigrations", sw.ElapsedMilliseconds, tenantId, databaseName);
    }

    private async Task CreateEmptyDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (await DatabaseExistsAsync(masterConnectionString, databaseName, cancellationToken))
            return;

        var sql = $@"
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'{EscapeSqlIdentifier(databaseName)}')
BEGIN
    CREATE DATABASE [{EscapeSqlIdentifier(databaseName)}]
END";

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> DatabaseExistsAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT COUNT(1) FROM sys.databases WHERE name = @name",
            connection);
        command.Parameters.AddWithValue("@name", databaseName);
        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    private async Task BackupDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        string backupPath,
        CancellationToken cancellationToken)
    {
        var sql = $@"
BACKUP DATABASE [{EscapeSqlIdentifier(databaseName)}]
TO DISK = @path
WITH INIT, COPY_ONLY, COMPRESSION, STATS = 10";

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
        command.Parameters.AddWithValue("@path", backupPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string DataLogical, string LogLogical)> GetBackupFileListAsync(
        string masterConnectionString,
        string backupPath,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @path", connection);
        command.Parameters.AddWithValue("@path", backupPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        string? dataLogical = null;
        string? logLogical = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var logical = reader.GetString(0);
            var type = reader.GetString(2);
            if (type == "D" && dataLogical is null)
                dataLogical = logical;
            else if (type == "L" && logLogical is null)
                logLogical = logical;
        }

        if (dataLogical is null || logLogical is null)
            throw new InvalidOperationException("Could not read logical file names from template backup.");

        return (dataLogical, logLogical);
    }

    private string ResolveBackupPath(string masterConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(_options.Value.TemplateBackupPath))
            return _options.Value.TemplateBackupPath!;

        var builder = new SqlConnectionStringBuilder(masterConnectionString);
        var instance = string.IsNullOrWhiteSpace(builder.DataSource) ? "default" : builder.DataSource;
        var safeInstance = string.Concat(instance.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactuTrust",
            "provisioning",
            $"{safeInstance}-tenant-template.bak");
    }

    private string ResolveDataFileDirectory(string masterConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(_options.Value.DataFileDirectory))
            return _options.Value.DataFileDirectory!;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactuTrust",
            "tenant-data");
    }

    private static string BuildConnectionString(string masterConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static string EscapeSqlIdentifier(string name) => name.Replace("]", "]]");

    private void LogStep(string step, long durationMs, Guid? tenantId, string? databaseName = null)
    {
        _logger.LogInformation(
            "FirmRegistration.Step={Step} DurationMs={DurationMs} TenantId={TenantId} DatabaseName={DatabaseName}",
            step,
            durationMs,
            tenantId,
            databaseName);
    }
}
