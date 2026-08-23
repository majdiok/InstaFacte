using FactuTrust.Infrastructure.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Accounting;

public sealed class CoaRemapTableTests
{
    private static CoaRemapTable Load() => SceChartCatalog.LoadRemap();

    [Fact]
    public void MoveEntries_HaveUniqueFromAndNoSelfMaps()
    {
        var remap = Load();
        var moves = remap.Entries
            .Where(e => (e.Mode == "move" || e.Mode == "merge-into") && e.To is not null)
            .ToList();

        Assert.NotEmpty(moves);
        Assert.Equal(moves.Count, moves.Select(e => e.From).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(moves, e => string.Equals(e.From, e.To, StringComparison.Ordinal));
    }

    [Fact]
    public void Rewrite_HonorsEveryDeclaredMoveAndMerge()
    {
        var remap = Load();
        foreach (var entry in remap.Entries.Where(e =>
                     (e.Mode == "move" || e.Mode == "merge-into") && e.To is not null))
        {
            Assert.Equal(entry.To, remap.Rewrite(entry.From));
        }
    }

    [Fact]
    public void Rewrite_FinalNumbersDoNotCollide_ExceptExplicitConsolidations()
    {
        var remap = Load();
        var declared = remap.Entries
            .Where(e => (e.Mode == "move" || e.Mode == "merge-into") && e.To is not null)
            .ToList();

        var allowedSharedTargets = declared
            .GroupBy(e => e.To!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var collisions = declared
            .Select(e => (e.From, To: remap.Rewrite(e.From)))
            .GroupBy(x => x.To, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.From).Distinct(StringComparer.Ordinal).Count() > 1)
            .Where(g => !allowedSharedTargets.Contains(g.Key))
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "Collisions finales non déclarées : " + string.Join(", ", collisions));
    }

    [Theory]
    [InlineData("4477", "43652")]
    [InlineData("4478", "4371")]
    [InlineData("6371", "6654")]
    [InlineData("412", "413")]
    [InlineData("413", "4197")]
    [InlineData("211", "221")]
    [InlineData("218", "228")]
    [InlineData("2818", "2828")]
    [InlineData("6818", "68112")]
    [InlineData("105", "117")]
    [InlineData("421", "425")]
    [InlineData("425", "421")]
    [InlineData("425.1", "421.1")]
    [InlineData("4210001", "4250001")]
    [InlineData("231", "232")]
    [InlineData("232", "231")]
    [InlineData("44561", "4321")]
    [InlineData("675", "636")]
    [InlineData("2119", "2219")]
    [InlineData("28181", "28281")]
    public void Rewrite_MapsCriticalAccounts(string from, string to)
    {
        Assert.Equal(to, Load().Rewrite(from));
    }

    [Theory]
    [InlineData("436711")]
    [InlineData("43651")]
    [InlineData("43666")]
    [InlineData("43662")]
    [InlineData("4011")]
    [InlineData("4111")]
    [InlineData("705")]
    [InlineData("707")]
    [InlineData("607")]
    [InlineData("5321")]
    [InlineData("5411")]
    [InlineData("6611")]
    [InlineData("6612")]
    [InlineData("453")]
    [InlineData("45311")]
    [InlineData("432")]
    public void Rewrite_LeavesProtectedIslandsUnchanged(string number)
    {
        Assert.Equal(number, Load().Rewrite(number));
    }

    [Fact]
    public void Rewrite_DoesNotSmashVatWhenMovingClass44()
    {
        var remap = Load();
        Assert.Equal("43", remap.Rewrite("44"));
        Assert.Equal("436711", remap.Rewrite("436711"));
        Assert.Equal("43652", remap.Rewrite("4477"));
    }

    [Fact]
    public void Rewrite_OldSeedMiniSet_FollowsBalances()
    {
        var remap = Load();
        var old = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["105"] = 100m,
            ["211"] = 5000m,
            ["4477"] = 50m,
            ["421"] = -2000m,
            ["412"] = 300m,
            ["436711"] = 190m
        };

        var migrated = old
            .GroupBy(kv => remap.Rewrite(kv.Key), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value), StringComparer.Ordinal);

        Assert.Equal(100m, migrated["117"]);
        Assert.Equal(5000m, migrated["221"]);
        Assert.Equal(50m, migrated["43652"]);
        Assert.Equal(-2000m, migrated["425"]);
        Assert.Equal(300m, migrated["413"]);
        Assert.Equal(190m, migrated["436711"]);
        Assert.False(migrated.ContainsKey("4477"));
        Assert.False(migrated.ContainsKey("211"));
    }

    [Fact]
    public void Rewrite_IsNotIdempotent_OnPayrollSwap_SoLogGuardIsRequired()
    {
        var remap = Load();
        Assert.Equal("425", remap.Rewrite("421"));
        Assert.Equal("421", remap.Rewrite("425"));
        Assert.Equal("421", remap.Rewrite(remap.Rewrite("421")));
    }
}
