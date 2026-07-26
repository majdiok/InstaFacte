using System.Text;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Table de correspondance de comptes (reprise depuis un autre progiciel). Exigences : traduction
/// exacte, compte non listé laissé INCHANGÉ, détection des tables incohérentes (cible manquante,
/// source associée à deux cibles) et suivi des correspondances inutilisées.
/// </summary>
public sealed class AccountMappingTableTests
{
    private static byte[] Csv(string content) => Encoding.UTF8.GetBytes(content);

    private static AccountMappingTable Parse(string csv)
    {
        var (table, issues) = AccountMappingTable.Parse(Csv(csv), JournalImportFormat.Csv);
        Assert.DoesNotContain(issues, i => i.IsBlocking);
        Assert.NotNull(table);
        return table!;
    }

    // ── Traduction ─────────────────────────────────────────────────────────────

    [Fact]
    public void Translate_MapsListedAccount()
    {
        var table = Parse("source;cible\n401000;401\n411000;4111\n");

        Assert.Equal("401", table.Translate("401000"));
        Assert.Equal("4111", table.Translate("411000"));
        Assert.Equal(2, table.AppliedCount);
    }

    [Fact]
    public void Translate_LeavesUnlistedAccountUnchanged()
    {
        var table = Parse("source;cible\n401000;401\n");

        // Un compte absent de la table passe tel quel — la table est un correctif ciblé.
        Assert.Equal("607", table.Translate("607"));
        Assert.Equal(0, table.AppliedCount);
    }

    [Fact]
    public void Translate_TrimsSourceBeforeLookup()
    {
        var table = Parse("source;cible\n401000;401\n");
        Assert.Equal("401", table.Translate("  401000  "));
    }

    [Fact]
    public void UnusedSources_ReportsNeverEncounteredMappings()
    {
        var table = Parse("source;cible\n401000;401\n999999;607\n");
        table.Translate("401000");

        var unused = table.UnusedSources;
        Assert.Equal(new[] { "999999" }, unused);
    }

    [Fact]
    public void TargetAccounts_ExposesDistinctTargets()
    {
        var table = Parse("source;cible\n401000;401\n401100;401\n411000;4111\n");

        Assert.Equal(2, table.TargetAccounts.Count);
        Assert.Contains("401", table.TargetAccounts);
        Assert.Contains("4111", table.TargetAccounts);
    }

    // ── Synonymes de colonnes ──────────────────────────────────────────────────

    [Theory]
    [InlineData("source;cible")]
    [InlineData("origine;destination")]
    [InlineData("ancien;nouveau")]
    [InlineData("Source;Cible")]
    public void Parse_AcceptsColumnSynonyms(string header)
    {
        var table = Parse($"{header}\n401000;401\n");
        Assert.Equal("401", table.Translate("401000"));
    }

    // ── Tables incohérentes ────────────────────────────────────────────────────

    [Fact]
    public void Parse_MissingColumn_IsBlocking()
    {
        var (table, issues) = AccountMappingTable.Parse(Csv("source\n401000\n"), JournalImportFormat.Csv);

        Assert.Null(table);
        Assert.Contains(issues, i => i.IsBlocking && i.Message.Contains("Colonne obligatoire"));
    }

    [Fact]
    public void Parse_EmptyTarget_IsBlocking()
    {
        var (table, issues) = AccountMappingTable.Parse(Csv("source;cible\n401000;\n"), JournalImportFormat.Csv);

        Assert.Null(table);
        Assert.Contains(issues, i => i.IsBlocking);
    }

    [Fact]
    public void Parse_ConflictingTargetsForSameSource_IsBlocking()
    {
        var (table, issues) = AccountMappingTable.Parse(
            Csv("source;cible\n401000;401\n401000;402\n"), JournalImportFormat.Csv);

        Assert.Null(table);
        Assert.Contains(issues, i => i.IsBlocking && i.Message.Contains("deux cibles"));
    }

    [Fact]
    public void Parse_DuplicateIdenticalMapping_IsAccepted()
    {
        // Le même couple répété n'est pas une incohérence.
        var table = Parse("source;cible\n401000;401\n401000;401\n");
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void Parse_EmptyTable_IsBlocking()
    {
        var (table, issues) = AccountMappingTable.Parse(Csv("source;cible\n"), JournalImportFormat.Csv);

        Assert.Null(table);
        Assert.Contains(issues, i => i.IsBlocking);
    }
}
