using System.Reflection;

using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using Xunit;

// FactuTrust.Infrastructure.Tests.Migration (assistant de reprise de données) masque le type EF
// Migration depuis n'importe quel namespace enfant de FactuTrust.Infrastructure.Tests.
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Non-régression de la dérive de schéma de <c>PayrollAccountingSettings</c>.
///
/// <para>
/// <c>20260901160000_AddPayrollAccountingSettings_Tenant</c> a été appliquée sur une partie du parc
/// dans un état antérieur à l'ajout de <c>EmployeeAuxiliaryEnabled</c>, puis complétée sur place.
/// EF ne rejoue pas une migration inscrite dans <c>__EFMigrationsHistory</c> : ces bases portaient
/// la table sans la colonne, et toute lecture du réglage échouait en SQL 207 — validation d'un cycle
/// de paie, écran Paramètres paie, journal de paie. Le rattrapage est
/// <c>20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant</c>.
/// </para>
///
/// <para>
/// Les trois premiers tests tournent partout (aucun SQL Server requis) : ils figent la présence du
/// rattrapage, ses gardes, son défaut, et le fait que son <c>Down()</c> ne touche à rien. Le dernier
/// rejoue la panne réelle contre un vrai SQL Server et n'est actif que sur demande explicite.
/// </para>
/// </summary>
public sealed class PayrollAccountingSettingsSchemaDriftTests
{
    private const string RepairMigrationId =
        "20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant";

    private const string OriginMigrationId =
        "20260901160000_AddPayrollAccountingSettings_Tenant";

    private const string Table = "PayrollAccountingSettings";
    private const string Column = "EmployeeAuxiliaryEnabled";

    // ── Modèle EF ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Model_maps_the_column_with_default_true()
    {
        // Le défaut doit rester vrai : c'est la valeur qu'appliquait la configuration globale
        // (Accounting:PayrollEmployeeAuxiliaryEnabled) que le réglage par dossier remplace. Un
        // défaut à faux ramènerait l'OD de paie à une ligne 425 unique, sans que personne ne l'ait
        // demandé.
        using var context = NewModelOnlyContext();

        var property = context.Model
            .FindEntityType(typeof(PayrollAccountingSettings))!
            .FindProperty(nameof(PayrollAccountingSettings.EmployeeAuxiliaryEnabled))!;

        Assert.False(property.IsNullable);
        Assert.Equal(true, property.GetDefaultValue());
    }

    // ── Migration de rattrapage ─────────────────────────────────────────────────────────────

    [Fact]
    public void Repair_migration_is_registered_after_the_migration_it_fixes()
    {
        var tenantMigrationIds = TenantMigrationIds();

        Assert.Contains(RepairMigrationId, tenantMigrationIds);
        Assert.Contains(OriginMigrationId, tenantMigrationIds);

        // EF ordonne les migrations par identifiant : le rattrapage doit passer après la création
        // de la table, sinon il s'exécuterait avant qu'elle existe et resterait sans effet.
        Assert.True(string.CompareOrdinal(RepairMigrationId, OriginMigrationId) > 0);
    }

    [Fact]
    public void Repair_migration_is_guarded_and_restores_the_documented_default()
    {
        var sql = RepairMigrationSql();

        // Double garde : no-op sur une base saine et sur toute base neuve issue de la migration
        // d'origine — le rattrapage doit pouvoir traverser tout le parc sans rien y changer.
        Assert.Contains("OBJECT_ID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"COL_LENGTH(N'dbo.{Table}', N'{Column}') IS NULL", sql, StringComparison.OrdinalIgnoreCase);

        Assert.Contains($"ADD [{Column}] bit NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFAULT CONVERT(bit, 1)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repair_migration_down_is_intentionally_a_no_op()
    {
        // La colonne appartient à la migration d'origine. La supprimer ici casserait toute base
        // créée normalement, qui la tient de sa propre création de table.
        Assert.Empty(RepairMigration().DownOperations);
    }

    // ── Rejeu de la panne contre un vrai SQL Server (opt-in) ────────────────────────────────

    /// <summary>
    /// Exige un SQL Server joignable et <c>RUN_PAYROLL_SCHEMA_DRIFT_SQL_TESTS=1</c> (défaut :
    /// ignoré) — même portail d'activation que <c>SectorRuleTablesMigrationTests</c>, pour que la
    /// suite ne dépende jamais de la présence de SQL Server. Reproduit exactement l'état des bases
    /// touchées (table présente, colonne absente, migration inscrite), applique le SQL réellement
    /// livré par le rattrapage, puis vérifie que la lecture EF qui échouait repasse — et qu'un
    /// second passage ne fait rien.
    /// </summary>
    [Fact]
    public async Task Repair_restores_a_drifted_database_and_is_idempotent()
    {
        if (Environment.GetEnvironmentVariable("RUN_PAYROLL_SCHEMA_DRIFT_SQL_TESTS") != "1")
            return;

        var connectionString = BuildThrowawayConnectionString();
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TenantDbContext(options);
        try
        {
            await context.Database.MigrateAsync();
            Assert.True(await ColumnExistsAsync(context));

            // Reproduction de la dérive : la colonne disparaît, la migration reste inscrite.
            await DropColumnAsync(context);
            Assert.False(await ColumnExistsAsync(context));
            await Assert.ThrowsAnyAsync<SqlException>(
                () => context.PayrollAccountingSettings.AsNoTracking().FirstOrDefaultAsync());

            var repairSql = RepairMigrationSql();
            await context.Database.ExecuteSqlRawAsync(repairSql);

            Assert.True(await ColumnExistsAsync(context));
            Assert.Equal(1, await DefaultValueAsync(context));

            // La lecture qui faisait échouer la validation du cycle de paie repasse.
            Assert.Null(await context.PayrollAccountingSettings.AsNoTracking().FirstOrDefaultAsync());

            // Second passage : garde active, aucune erreur, aucun doublon de colonne.
            await context.Database.ExecuteSqlRawAsync(repairSql);
            Assert.True(await ColumnExistsAsync(context));
            Assert.Equal(1, await DefaultValueAsync(context));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    // ── Utilitaires ─────────────────────────────────────────────────────────────────────────

    private static TenantDbContext NewModelOnlyContext() =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FactuTrust_ModelOnly;Trusted_Connection=True")
            .Options);

    private static IReadOnlyList<string> TenantMigrationIds() =>
        typeof(TenantDbContext).Assembly
            .GetTypes()
            .Where(t => typeof(EfMigration).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(TenantDbContext))
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToList();

    private static EfMigration RepairMigration()
    {
        var type = typeof(TenantDbContext).Assembly
            .GetTypes()
            .Single(t => t.GetCustomAttribute<MigrationAttribute>()?.Id == RepairMigrationId);

        return (EfMigration)Activator.CreateInstance(type)!;
    }

    private static string RepairMigrationSql() =>
        string.Join(
            Environment.NewLine,
            RepairMigration().UpOperations.OfType<SqlOperation>().Select(op => op.Sql));

    private static string BuildThrowawayConnectionString()
    {
        var server = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        var catalog = $"FT_Test_PayrollSchemaDrift_{Guid.NewGuid():N}";

        if (string.IsNullOrWhiteSpace(server))
        {
            return $"Server=(localdb)\\MSSQLLocalDB;Database={catalog};"
                + "Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        return new SqlConnectionStringBuilder(server) { InitialCatalog = catalog }.ConnectionString;
    }

    private static async Task<bool> ColumnExistsAsync(TenantDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT CASE WHEN COL_LENGTH('dbo.{Table}', '{Column}') IS NULL THEN 0 ELSE 1 END;";
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Valeur du DEFAULT porté par la colonne, telle que SQL Server l'applique.</summary>
    private static async Task<int?> DefaultValueAsync(TenantDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT CASE WHEN dc.definition LIKE '%1%' THEN 1 ELSE 0 END
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.{Table}') AND c.name = '{Column}';
                """;
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : Convert.ToInt32(value);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Supprime la colonne et la contrainte de défaut qui la porte — EF nomme cette dernière
    /// automatiquement lors du <c>CreateTable</c>, d'où la recherche dans <c>sys</c>.
    /// </summary>
    private static async Task DropColumnAsync(TenantDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync($"""
            DECLARE @constraint sysname = (
                SELECT dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID('dbo.{Table}') AND c.name = '{Column}');

            IF @constraint IS NOT NULL
                EXEC('ALTER TABLE [dbo].[{Table}] DROP CONSTRAINT [' + @constraint + ']');

            ALTER TABLE [dbo].[{Table}] DROP COLUMN [{Column}];
            """);
    }
}
