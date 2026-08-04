using System.Text.RegularExpressions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Filet statique anti-régression pour le bug « A second operation was started on
/// this context instance… » corrigé dans le bootstrap Exchange (voir plan
/// <c>fix-bootstrap-dbcontext-concurrency</c>). Toute utilisation de
/// <c>Task.WhenAll(...)</c> qui passe un membre <c>_db</c> / <c>_master</c> /
/// <c>_masterContext</c> à l'intérieur des services Infrastructure ré-ouvrirait
/// la fenêtre de concurrence EF Core (DbContext scoped + accès parallèle). Ce
/// test échoue avant même que la régression ne parte en runtime.
/// </summary>
public sealed class DbContextConcurrencyGuardrailTests
{
    private static readonly string[] ForbiddenMembers = ["_db", "_master", "_masterContext"];

    [Fact]
    public void No_TaskWhenAll_on_scoped_MasterDbContext_in_infrastructure_services()
    {
        var servicesDir = LocateInfrastructureServicesDirectory();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(servicesDir, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var idx = 0;
            while ((idx = content.IndexOf("Task.WhenAll", idx, StringComparison.Ordinal)) >= 0)
            {
                var openParen = content.IndexOf('(', idx);
                if (openParen < 0)
                    break;

                var closeParen = FindMatchingParen(content, openParen);
                if (closeParen < 0)
                {
                    idx = openParen + 1;
                    continue;
                }

                var argSlice = content.Substring(openParen, closeParen - openParen + 1);
                if (ContainsForbiddenMemberAccess(argSlice))
                {
                    var lineNumber = LineNumberOf(content, idx);
                    var preview = Truncate(argSlice, 160);
                    violations.Add($"{Path.GetFileName(file)}:{lineNumber} → Task.WhenAll{preview}");
                }

                idx = closeParen + 1;
            }
        }

        Assert.True(
            violations.Count == 0,
            "Task.WhenAll détecté sur DbContext scoped (source du bug 'second operation started'):\n"
                + string.Join("\n", violations));
    }

    private static bool ContainsForbiddenMemberAccess(string slice)
    {
        foreach (var member in ForbiddenMembers)
        {
            // Match \b_member\. — accès à un membre du DbContext scoped.
            var pattern = new Regex($@"\b{Regex.Escape(member)}\.", RegexOptions.CultureInvariant);
            if (pattern.IsMatch(slice))
                return true;
        }
        return false;
    }

    private static int FindMatchingParen(string content, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < content.Length; i++)
        {
            switch (content[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                        return i;
                    break;
            }
        }
        return -1;
    }

    private static int LineNumberOf(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
                line++;
        }
        return line;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max) + "…";

    private static string LocateInfrastructureServicesDirectory()
    {
        // Remonte depuis le dossier du binaire de test jusqu'à trouver
        // src/Backend/FactuTrust.Infrastructure/Services.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "src", "Backend", "FactuTrust.Infrastructure", "Services");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Répertoire src/Backend/FactuTrust.Infrastructure/Services introuvable "
            + "(remontée depuis AppContext.BaseDirectory).");
    }
}
