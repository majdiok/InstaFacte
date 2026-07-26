using FactuTrust.Application.Accounting;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class NctDetailedNotesBuilderTests
{
    [Fact]
    public void Build_BuildsAccountLevelLines_ForNotes1_3_4()
    {
        var cur = new Dictionary<string, decimal>
        {
            ["214"] = 30000m,
            ["224"] = 45000m,
            ["281"] = -41592.317m,
            ["101"] = -20000m
        };
        var prev = new Dictionary<string, decimal>
        {
            ["214"] = 30000m,
            ["224"] = 45000m,
            ["281"] = -30000m
        };
        var labels = new Dictionary<string, string>
        {
            ["214"] = "FONDS COMMERCIAL",
            ["224"] = "MATERIEL DE TRANSPORT",
            ["281"] = "AMORTISSEMENTS"
        };

        var notes = NctDetailedNotesBuilder.Build(cur, prev, labels);

        var n1 = notes.Single(n => n.Number == 1);
        Assert.Equal("Immobilisations incorporelles", n1.Title);
        Assert.Equal(NctAnnexFamily.Actif, n1.Family);
        Assert.Single(n1.Lines);
        Assert.Equal("214", n1.Lines[0].AccountNumber);
        Assert.Equal("FONDS COMMERCIAL", n1.Lines[0].Label);
        Assert.Equal(30000m, n1.Lines[0].Amount);
        Assert.Equal(30000m, n1.Total);

        var n3 = notes.Single(n => n.Number == 3);
        Assert.Equal(45000m, n3.Total);

        var n4 = notes.Single(n => n.Number == 4);
        Assert.Equal(-41592.317m, n4.Total);
        Assert.Equal(-30000m, n4.PreviousTotal);
    }

    [Fact]
    public void Build_SkipsZeroBothYears()
    {
        var cur = new Dictionary<string, decimal> { ["214"] = 0m, ["224"] = 1000m };
        var prev = new Dictionary<string, decimal> { ["214"] = 0m };

        var notes = NctDetailedNotesBuilder.Build(cur, prev);

        Assert.DoesNotContain(notes, n => n.Number == 1);
        Assert.Contains(notes, n => n.Number == 3);
    }

    [Fact]
    public void Build_AccountBelongsToSingleNote()
    {
        var sampleAccounts = new[]
        {
            "101", "111", "121", "16", "214", "224", "25", "281", "291", "31",
            "401", "411", "421", "532", "601", "70"
        };

        foreach (var account in sampleAccounts)
        {
            var matches = NctDetailedNoteCatalog.All.Count(d => d.AccountPredicate(account));
            Assert.True(matches <= 1, $"Account {account} matched {matches} notes.");
        }
    }

    [Fact]
    public void Build_PassifUsesNegatedNet()
    {
        var cur = new Dictionary<string, decimal> { ["101"] = -15000m };
        var notes = NctDetailedNotesBuilder.Build(cur, new Dictionary<string, decimal>());

        var capital = notes.Single(n => n.Number == 10);
        Assert.Equal(15000m, capital.Total);
    }
}
