using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Tests.Fixtures;

/// <summary>
/// Base de données SQL Server dédiée à UNE classe de test (cycle de vie par instance : une
/// instance = un test run xUnit d'une classe = une base). Résout le problème de la connection
/// string SQL partagée en CI (tâche 5, plan §6) : avec le parallélisme xUnit par défaut (classes
/// exécutées en parallèle), 6+ classes qui font chacune <c>EnsureCreated</c>/<c>EnsureDeleted</c>
/// sur LA MÊME base se détruiraient mutuellement leurs données.
///
/// Sémantique :
/// - <c>FACTUTRUST_TEST_SQL_CONNECTION</c> DÉFINIE : traitée comme une connection string
///   NIVEAU SERVEUR (tout <c>InitialCatalog</c> fourni est écrasé) ; une base dédiée
///   <c>FT_Test_{ClassName}_{Guid:N}</c> (tronquée sous 128 caractères, limite SQL Server) est
///   dérivée et créée (<c>EnsureCreated</c>) à la construction. Si la connexion échoue, l'exception
///   REMONTE (échec explicite) : une variable configurée en CI qui ne peut pas joindre le serveur
///   est une panne d'infrastructure, pas une absence de SQL sur poste dev.
/// - <c>FACTUTRUST_TEST_SQL_CONNECTION</c> ABSENTE : repli LocalDB éphémère (même convention que
///   l'existant — voir <c>LetteringServiceAmbientTransactionTests</c> avant sa migration vers ce
///   helper) ; si LocalDB est indisponible (poste dev sans SQL), <see cref="CanRun"/> est
///   <c>false</c> silencieusement — les classes appelantes font `if (!_canRun) return;` dans chaque
///   test, comme avant.
///
/// Cycle de vie : <c>EnsureCreated</c> à la construction, <c>EnsureDeleted</c> au <see cref="Dispose"/>
/// — identique au patron précédent, désormais sur une base isolée par classe.
///
/// IMPORTANT — <c>READ_COMMITTED_SNAPSHOT</c> forcé à OFF après <c>EnsureCreated</c> : le
/// provider SQL Server d'EF Core active cette option sur toute base qu'il crée lui-même via
/// <c>EnsureCreated</c> (confirmé empiriquement : une base créée par <c>CREATE DATABASE</c> brut
/// a l'option à OFF — réglage par défaut hérité de <c>model</c> — alors qu'une base créée par
/// <c>EnsureCreated</c> l'a à ON). En PRODUCTION, les bases tenant sont provisionnées via
/// <c>Database.MigrateAsync</c> (<c>TenantDatabaseProvisioner</c>), qui NE touche PAS à cette
/// option ; elles restent donc en ReadCommitted verrouillant, comme documenté par
/// <c>IJournalEntryRepository.GetByIdForUpdateAsync</c> (plan §D7). Sans ce correctif, les tests
/// de concurrence SQL tourneraient en mode snapshot (lecteurs jamais bloqués par le XLOCK,
/// HOLDLOCK) — un environnement qui NE reproduit PAS le comportement réel de production, faussant
/// silencieusement l'hypothèse même que ces tests doivent vérifier.
/// </summary>
public sealed class SqlTestDatabase : IDisposable
{
    /// <summary>Connection string effective (base dédiée) — <c>null</c> si non déterminable.</summary>
    public string? ConnectionString { get; }

    /// <summary>
    /// <c>true</c> si la base a été créée avec succès et que les tests peuvent s'exécuter contre
    /// SQL réel. <c>false</c> uniquement quand <c>FACTUTRUST_TEST_SQL_CONNECTION</c> est ABSENTE et
    /// que le repli LocalDB est indisponible (repli silencieux existant).
    /// </summary>
    public bool CanRun { get; }

    public SqlTestDatabase(string className)
    {
        var serverConnectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");

        if (!string.IsNullOrWhiteSpace(serverConnectionString))
        {
            // Variable définie (poste CI) : connexion niveau serveur attendue — le catalogue
            // fourni (s'il y en a un) est ignoré/écrasé. Échec de connexion = échec loud (throw),
            // volontairement NON avalé.
            ConnectionString = BuildPerClassConnectionString(serverConnectionString, className);
            using var context = NewRawContext(ConnectionString);
            context.Database.EnsureCreated();
            DisableReadCommittedSnapshot(context);
            CanRun = true;
            return;
        }

        // Variable absente : repli LocalDB éphémère, silencieux si indisponible (poste dev sans
        // SQL) — sémantique identique à celle de chaque classe avant migration.
        var localDbName = $"FactuTrust_{SafeSegment(className, 100)}_{Guid.NewGuid():N}";
        ConnectionString = $"Server=(localdb)\\mssqllocaldb;Database={localDbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        CanRun = TryEnsureCreated(ConnectionString);
    }

    /// <summary>
    /// Dérive une connection string niveau serveur vers une base dédiée
    /// <c>FT_Test_{ClassName}_{Guid:N}</c>, tronquée pour rester sous 128 caractères (limite du
    /// nom de base SQL Server).
    /// </summary>
    private static string BuildPerClassConnectionString(string serverConnectionString, string className)
    {
        var builder = new SqlConnectionStringBuilder(serverConnectionString);
        var suffix = Guid.NewGuid().ToString("N");
        const string prefix = "FT_Test_";
        const int maxDatabaseNameLength = 128;
        var maxClassNameLength = maxDatabaseNameLength - prefix.Length - 1 /* underscore */ - suffix.Length;
        var safeClassName = SafeSegment(className, Math.Max(1, maxClassNameLength));
        builder.InitialCatalog = $"{prefix}{safeClassName}_{suffix}";
        return builder.ConnectionString;
    }

    private static string SafeSegment(string value, int maxLength)
        => value.Length > maxLength ? value[..maxLength] : value;

    private static bool TryEnsureCreated(string connectionString)
    {
        try
        {
            using var context = NewRawContext(connectionString);
            context.Database.EnsureCreated();
            DisableReadCommittedSnapshot(context);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ramène l'option base <c>READ_COMMITTED_SNAPSHOT</c> à OFF (voir doc de classe) : EF Core
    /// l'active par défaut pour toute base créée via <c>EnsureCreated</c>, ce qui ferait tourner
    /// les tests en mode versionné (snapshot) au lieu du ReadCommitted verrouillant réel de
    /// production. <c>WITH ROLLBACK IMMEDIATE</c> est sûr ici : aucune autre connexion n'existe
    /// encore vers une base qu'on vient de créer.
    /// </summary>
    private static void DisableReadCommittedSnapshot(TenantDbContext context)
    {
        if (!context.Database.IsRelational())
            return;

        // Le nom de base est dérivé par ce fichier même (préfixe fixe + GUID, jamais une entrée
        // utilisateur) et ALTER DATABASE n'admet pas de paramètre sur l'identifiant de base ;
        // l'injection SQL n'est pas applicable ici (avertissement EF1002 supprimé volontairement).
        var databaseName = context.Database.GetDbConnection().Database;
#pragma warning disable EF1002
        context.Database.ExecuteSqlRaw(
            $"ALTER DATABASE [{databaseName}] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;");
#pragma warning restore EF1002
    }

    private static TenantDbContext NewRawContext(string connectionString)
        => new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    public void Dispose()
    {
        if (!CanRun || string.IsNullOrWhiteSpace(ConnectionString))
            return;

        try
        {
            using var context = NewRawContext(ConnectionString);
            context.Database.EnsureDeleted();
        }
        catch
        {
            // Nettoyage best-effort (base éphémère de test).
        }
    }
}
