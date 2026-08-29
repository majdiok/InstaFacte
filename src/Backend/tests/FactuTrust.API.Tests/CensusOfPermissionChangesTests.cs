using System.Text;
using System.Text.Json;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace FactuTrust.API.Tests;

/// <summary>
/// Plan §7.2.1 — read-only census utility (NOT a regression test in the usual sense): lists exactly
/// the ACTIVE users whose effective permissions actually change (added keys only) between the
/// pre-plan calculator (<see cref="LegacyEffectivePermissionsCalculator"/>) and the current one
/// (<see cref="EffectivePermissionsCalculator"/>, hybrid ceiling §5.3), for pre-deployment review by
/// tenant admins. No data is written anywhere; the target database is read via
/// <see cref="MasterDbContext"/> with <c>AsNoTracking()</c> queries only.
///
/// Manual-only by design: marked <c>[Trait("Category","Census")]</c> so it is excluded from any
/// normal `dotnet test` run and must be invoked explicitly, e.g.
/// <c>dotnet test --filter "FullyQualifiedName~CensusOfPermissionChangesTests"</c>. It also no-ops
/// (does not fail) when its dedicated connection-string env var is absent, so it can never
/// accidentally break CI or a broader local run even if someone forgets the filter.
///
/// Configuration (env vars):
/// - <c>CENSUS_MASTER_CONNECTION</c>: connection string to the MASTER database to audit (staging or
///   production, run just before deployment — see plan §7.2). Deliberately a DIFFERENT env var name
///   than <c>ConnectionStrings__MasterConnection</c> used by the rest of the real-SQL test suite, so
///   that running the normal test suite against a real SQL Server never accidentally triggers this
///   utility against the same database.
/// - <c>CENSUS_OUTPUT_CSV_PATH</c> (optional): output CSV path. Defaults to
///   <c>census-permission-changes.csv</c> in the current directory.
///
/// A delta of REMOVED permission keys is a bug (plan §7.1.C guarantees zero regression) — this
/// utility asserts loudly (test failure) if it ever detects one, instead of silently reporting it.
/// </summary>
public sealed class CensusOfPermissionChangesTests
{
    private readonly ITestOutputHelper _output;

    public CensusOfPermissionChangesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Census")]
    public async Task Census_of_users_whose_effective_permissions_change_under_the_new_hybrid_ceiling()
    {
        var connectionString = Environment.GetEnvironmentVariable("CENSUS_MASTER_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine(
                "CENSUS_MASTER_CONNECTION is not set — this is a manual, read-only pre-deployment " +
                "utility (plan §7.2.1), so it no-ops rather than failing. Set the env var to the " +
                "target master DB connection string and re-run this single test to produce the CSV.");
            return;
        }

        var outputPath = Environment.GetEnvironmentVariable("CENSUS_OUTPUT_CSV_PATH")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "census-permission-changes.csv");

        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new MasterDbContext(options);

        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        var allGrants = await db.UserModuleGrants.AsNoTracking()
            .Where(g => userIds.Contains(g.UserId))
            .ToListAsync();
        var grantsByUser = allGrants
            .GroupBy(g => g.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<UserModuleGrant>)g.OrderBy(x => x.Module).ToList());

        var userRoleEntities = await db.Set<IdentityUserRole<Guid>>().AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync();
        var rolesById = await db.Roles.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? UserRole.Accountant.ToString());
        var userRoleMap = userRoleEntities
            .GroupBy(ur => ur.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ur => rolesById.GetValueOrDefault(ur.RoleId, UserRole.Accountant.ToString())).FirstOrDefault()
                    ?? UserRole.Accountant.ToString());

        var rows = new List<CensusRow>();
        var removalViolations = new List<string>();

        foreach (var user in users)
        {
            var roleName = userRoleMap.GetValueOrDefault(user.Id, UserRole.Accountant.ToString());
            var role = Enum.TryParse<UserRole>(roleName, out var parsedRole) ? parsedRole : UserRole.Accountant;

            var userGrants = grantsByUser.GetValueOrDefault(user.Id) ?? Array.Empty<UserModuleGrant>();
            IReadOnlyDictionary<AppModule, bool>? grantDict = null;
            if (userGrants.Count > 0)
                grantDict = userGrants.ToDictionary(g => g.Module, g => g.IsEnabled);
            var featureMap = BuildFeatureKeysByModule(userGrants);

            var oldPermissions = LegacyEffectivePermissionsCalculator.Compute(role, grantDict, featureMap);
            var newPermissions = EffectivePermissionsCalculator.Compute(role, grantDict, featureMap);

            var added = newPermissions.Except(oldPermissions).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var removed = oldPermissions.Except(newPermissions).OrderBy(k => k, StringComparer.Ordinal).ToList();

            if (removed.Count > 0)
            {
                removalViolations.Add(
                    $"User {user.Id} ({user.Email}, role={role}) LOSES permission key(s): {string.Join(", ", removed)}");
            }

            if (added.Count > 0)
            {
                rows.Add(new CensusRow(user.TenantId, user.Id, user.Email ?? "", role, added));
            }
        }

        Assert.True(
            removalViolations.Count == 0,
            "Census detected a REMOVAL delta (contradicts the plan §7.1.C zero-regression guarantee " +
            "— this is a bug in the new calculator, not something to report as an accepted change):\n" +
            string.Join("\n", removalViolations));

        WriteCsv(outputPath, rows);

        _output.WriteLine($"Active users scanned: {users.Count}");
        _output.WriteLine($"Users with an added-permissions delta: {rows.Count}");
        _output.WriteLine($"CSV written to: {outputPath}");
    }

    private static IReadOnlyDictionary<AppModule, IReadOnlyList<string>>? BuildFeatureKeysByModule(
        IReadOnlyList<UserModuleGrant> grants)
    {
        Dictionary<AppModule, IReadOnlyList<string>>? map = null;
        foreach (var g in grants)
        {
            if (!g.IsEnabled || string.IsNullOrWhiteSpace(g.EnabledFeatureKeys))
                continue;

            List<string>? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<List<string>>(g.EnabledFeatureKeys);
            }
            catch (JsonException)
            {
                continue;
            }

            if (parsed is null)
                continue;

            map ??= new Dictionary<AppModule, IReadOnlyList<string>>();
            map[g.Module] = parsed;
        }

        return map;
    }

    private static void WriteCsv(string path, IReadOnlyList<CensusRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TenantId,UserId,Email,Role,AddedPermissionKeys");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                CsvField(row.TenantId.ToString()),
                CsvField(row.UserId.ToString()),
                CsvField(row.Email),
                CsvField(row.Role.ToString()),
                CsvField(string.Join(";", row.AddedPermissionKeys))));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string CsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private sealed record CensusRow(Guid TenantId, Guid UserId, string Email, UserRole Role, IReadOnlyList<string> AddedPermissionKeys);
}
