using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class UniqueFieldCheckerTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    private static CustomFieldDefinition Field(string key, bool isUnique) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Text, false, isUnique, 0, null, null, null, null);

    /// <summary>Stub repo: ExistsWithFieldValueAsync returns whatever the test configures; everything else unused.</summary>
    private sealed class StubRecords : ICustomRecordRepository
    {
        public bool ExistsResult { get; init; }
        public int ExistsCalls { get; private set; }

        public Task<bool> ExistsWithFieldValueAsync(Guid tenantId, Guid entityDefinitionId, string fieldKey, string value, Guid? excludeId, CancellationToken ct = default)
        {
            ExistsCalls++;
            return Task.FromResult(ExistsResult);
        }

        public Task<CustomRecord?> GetAsync(Guid t, Guid e, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(IReadOnlyList<CustomRecord> Items, int TotalCount)> ListAsync(Guid t, Guid e, string? s, int p, int ps, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Guid t, Guid e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CustomRecord>> GetAllForReportAsync(Guid t, Guid e, int max, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(CustomRecord r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(CustomRecord r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateWithConcurrencyAsync(CustomRecord r, byte[]? v, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Conflict_when_unique_value_already_exists()
    {
        var fields = new[] { Field("email", isUnique: true) };
        var repo = new StubRecords { ExistsResult = true };
        var error = await UniqueFieldChecker.CheckAsync(repo, Tid, Eid, fields, "{\"email\":\"a@b.io\"}", excludeId: null, CancellationToken.None);
        Assert.NotNull(error);
        Assert.Equal(1, repo.ExistsCalls);
    }

    [Fact]
    public async Task No_conflict_when_value_absent_in_db()
    {
        var fields = new[] { Field("email", isUnique: true) };
        var repo = new StubRecords { ExistsResult = false };
        var error = await UniqueFieldChecker.CheckAsync(repo, Tid, Eid, fields, "{\"email\":\"a@b.io\"}", excludeId: null, CancellationToken.None);
        Assert.Null(error);
    }

    [Fact]
    public async Task Non_unique_fields_are_skipped()
    {
        var fields = new[] { Field("name", isUnique: false) };
        var repo = new StubRecords { ExistsResult = true };
        var error = await UniqueFieldChecker.CheckAsync(repo, Tid, Eid, fields, "{\"name\":\"x\"}", excludeId: null, CancellationToken.None);
        Assert.Null(error);
        Assert.Equal(0, repo.ExistsCalls);
    }

    [Fact]
    public async Task Missing_or_empty_unique_value_is_skipped()
    {
        var fields = new[] { Field("email", isUnique: true) };
        var repo = new StubRecords { ExistsResult = true };
        var error = await UniqueFieldChecker.CheckAsync(repo, Tid, Eid, fields, "{}", excludeId: null, CancellationToken.None);
        Assert.Null(error);
        Assert.Equal(0, repo.ExistsCalls);
    }
}
