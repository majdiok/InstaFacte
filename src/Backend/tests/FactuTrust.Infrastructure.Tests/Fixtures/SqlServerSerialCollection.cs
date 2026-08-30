using Xunit;

namespace FactuTrust.Infrastructure.Tests.Fixtures;

/// <summary>
/// SQL Server concurrency tests intentionally create competing connections. Running them alongside
/// the rest of the LocalDB-heavy suite can exhaust the single runner instance and turn the applock
/// assertion into an unrelated command timeout.
/// </summary>
[CollectionDefinition(SqlServerSerialCollection.Name, DisableParallelization = true)]
public sealed class SqlServerSerialCollection
{
    public const string Name = "SQL Server serial";
}
