using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Table de correspondance de comptes pour la reprise d'un dossier venu d'un autre progiciel
/// (« Migration des données ») : traduit un numéro de compte SOURCE en compte CIBLE du plan local,
/// juste avant la validation d'import. SANS ÉTAT — fournie en fichier à chaque import, jamais
/// persistée.
/// <para>
/// Un compte non listé passe INCHANGÉ : la table est un correctif ciblé, pas un filtre.
/// </para>
/// </summary>
internal sealed class AccountMappingTable
{
    private const string ExpectedColumns = "source, cible";

    private static readonly Dictionary<string, string[]> ColumnSynonyms = new()
    {
        ["source"] = new[] { "source", "origine", "ancien", "comptesource", "ancien compte", "anciencompte", "from" },
        ["cible"] = new[] { "cible", "destination", "nouveau", "comptecible", "nouveau compte", "nouveaucompte", "to" }
    };

    private readonly Dictionary<string, string> _map;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    private AccountMappingTable(Dictionary<string, string> map) => _map = map;

    /// <summary>Nombre de correspondances déclarées.</summary>
    public int Count => _map.Count;

    /// <summary>Comptes cibles distincts — à confronter au plan comptable par l'appelant.</summary>
    public IReadOnlyCollection<string> TargetAccounts => _map.Values.Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Nombre de traductions effectivement appliquées depuis la construction.</summary>
    public int AppliedCount { get; private set; }

    /// <summary>Correspondances déclarées mais jamais rencontrées dans le fichier de données.</summary>
    public IReadOnlyList<string> UnusedSources =>
        _map.Keys.Where(k => !_used.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Analyse le fichier de correspondance. Renvoie la table (null si anomalie bloquante) et les
    /// anomalies rencontrées, au format des imports existants.
    /// </summary>
    public static (AccountMappingTable? Table, IReadOnlyList<ImportIssueDto> Issues) Parse(
        byte[] content, JournalImportFormat format)
    {
        // Le FEC est un format d'écritures : une table de correspondance est CSV ou Excel.
        var readFormat = format == JournalImportFormat.Excel ? JournalImportFormat.Excel : JournalImportFormat.Csv;
        var (rows, issues) = TabularRowReader.Read(
            content, readFormat, ColumnSynonyms, new[] { "source", "cible" }, ExpectedColumns);

        var problems = new List<ImportIssueDto>(issues);
        if (problems.Any(i => i.IsBlocking))
            return (null, problems);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var line = 1;
        foreach (var row in rows)
        {
            line++;
            var source = row.GetValueOrDefault("source", string.Empty).Trim();
            var target = row.GetValueOrDefault("cible", string.Empty).Trim();
            var reference = $"correspondance ligne {line}";

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                problems.Add(Blocking(reference, "Les colonnes source et cible sont obligatoires."));
                continue;
            }
            if (map.TryGetValue(source, out var existing) && existing != target)
            {
                problems.Add(Blocking(reference,
                    $"Le compte source {source} est associé à deux cibles différentes ({existing} et {target})."));
                continue;
            }
            map[source] = target;
        }

        if (map.Count == 0 && !problems.Any(i => i.IsBlocking))
            problems.Add(Blocking("correspondance", "La table de correspondance ne contient aucune ligne exploitable."));

        return problems.Any(i => i.IsBlocking) ? (null, problems) : (new AccountMappingTable(map), problems);
    }

    /// <summary>
    /// Traduit un compte. Un compte absent de la table est renvoyé inchangé (et n'est pas compté
    /// comme une traduction appliquée).
    /// </summary>
    public string Translate(string accountNumber)
    {
        var account = accountNumber?.Trim() ?? string.Empty;
        if (!_map.TryGetValue(account, out var target))
            return accountNumber ?? string.Empty;

        _used.Add(account);
        AppliedCount++;
        return target;
    }

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };
}
