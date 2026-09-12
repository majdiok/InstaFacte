using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioComputedFieldReaderTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid ParentEid = Guid.NewGuid();

    private static CustomFieldDefinition Field(string key, CustomFieldType type, string? optionsJson = null) =>
        CustomFieldDefinition.Create(Tid, ParentEid, key, key, type, false, false, 0, null, optionsJson, null, null);

    // ---- Minimal fakes ----

    private sealed class FakeEntityRepo : ICustomEntityRepository
    {
        private readonly Dictionary<string, CustomEntityDefinition> _byKey = new(StringComparer.Ordinal);
        public void Add(CustomEntityDefinition e) => _byKey[e.Key] = e;
        public Task<CustomEntityDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken ct = default)
            => Task.FromResult(_byKey.GetValueOrDefault(key));
        public Task<CustomEntityDefinition?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CustomEntityDefinition>> ListAsync(Guid t, bool inc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CustomEntityDefinition>> ListBySystemIdAsync(Guid t, Guid systemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CustomEntityDefinition>>(Array.Empty<CustomEntityDefinition>());
        public Task<bool> KeyExistsAsync(Guid t, string k, bool includeDeleted = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(CustomEntityDefinition e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(CustomEntityDefinition e, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeRecordRepo : ICustomRecordRepository
    {
        private readonly Dictionary<Guid, IReadOnlyList<CustomRecord>> _byEntity = new();
        public void Seed(Guid entityId, IReadOnlyList<CustomRecord> rows) => _byEntity[entityId] = rows;
        public Task<IReadOnlyList<CustomRecord>> GetAllForReportAsync(Guid t, Guid entityId, int max, CancellationToken ct = default)
            => Task.FromResult(_byEntity.GetValueOrDefault(entityId, Array.Empty<CustomRecord>()));
        public Task<CustomRecord?> GetAsync(Guid t, Guid e, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(IReadOnlyList<CustomRecord> Items, int TotalCount)> ListAsync(Guid t, Guid e, string? s, int p, int ps, string? ff = null, string? fv = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsWithFieldPairAsync(Guid t, Guid e, string ka, string va, string kb, string vb, Guid? ex, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(Guid t, Guid e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsWithFieldValueAsync(Guid t, Guid e, string k, string v, Guid? ex, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(CustomRecord r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(CustomRecord r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateWithConcurrencyAsync(CustomRecord r, byte[]? rv, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(IReadOnlyList<CustomRecord> Items, int Total)> QueryAsync(FactuTrust.Application.Features.Studio.RecordViews.RecordQuerySpec spec, IReadOnlyDictionary<string, CustomFieldType> fieldTypes, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeExistingProvider : IExistingDataSourceProvider
    {
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Records;
        public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?> GetRelationRecordsByIdsAsync(
            Guid t, string sourceKey, IReadOnlyCollection<string> ids, CancellationToken ct = default)
            => Task.FromResult(Records);
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> GetRowsAsync(Guid t, string k, int m, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SelectOptionDto>?> GetRelationOptionsAsync(Guid t, string k, int m, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static StudioComputedFieldReader Reader(FakeEntityRepo e, FakeRecordRepo r, FakeExistingProvider p) =>
        new(e, r, p, NullLogger<StudioComputedFieldReader>.Instance);

    // ---- Tests ----

    [Fact]
    public async Task Lookup_over_custom_relation_injects_target_value()
    {
        var contacts = CustomEntityDefinition.Create(Tid, "contacts", "Contact", "Contacts", null, null, null);
        var contact = CustomRecord.Create(Tid, contacts.Id, """{"name":"Alice"}""", null);

        var entities = new FakeEntityRepo(); entities.Add(contacts);
        var records = new FakeRecordRepo(); records.Seed(contacts.Id, new[] { contact });

        var fields = new[]
        {
            Field("client", CustomFieldType.RelationCustom, StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "contacts"))),
            Field("contact_name", CustomFieldType.Lookup, StudioLookupRollup.SerializeLookup("client", "name"))
        };
        var dto = new CustomRecordDto(Guid.NewGuid(), new JsonObject { ["client"] = contact.Id.ToString() }, DateTime.UtcNow, DateTime.UtcNow, null);

        await Reader(entities, records, new FakeExistingProvider()).EnrichAsync(Tid, fields, new[] { dto });

        Assert.Equal("Alice", (string?)((JsonObject)dto.Data!)["contact_name"]);
    }

    [Fact]
    public async Task Lookup_over_existing_relation_injects_target_value()
    {
        var clientId = Guid.NewGuid().ToString();
        var provider = new FakeExistingProvider
        {
            Records = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                [clientId] = new Dictionary<string, object?> { ["city"] = "Tunis" }
            }
        };
        var fields = new[]
        {
            Field("client", CustomFieldType.RelationExisting, StudioFieldJson.SerializeRelation(new RelationRefDto("existing", "clients"))),
            Field("client_city", CustomFieldType.Lookup, StudioLookupRollup.SerializeLookup("client", "city"))
        };
        var dto = new CustomRecordDto(Guid.NewGuid(), new JsonObject { ["client"] = clientId }, DateTime.UtcNow, DateTime.UtcNow, null);

        await Reader(new FakeEntityRepo(), new FakeRecordRepo(), provider).EnrichAsync(Tid, fields, new[] { dto });

        Assert.Equal("Tunis", (string?)((JsonObject)dto.Data!)["client_city"]);
    }

    [Fact]
    public async Task Rollup_aggregates_matching_children()
    {
        var lines = CustomEntityDefinition.Create(Tid, "lines", "Ligne", "Lignes", null, null, null);
        var parentId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var children = new[]
        {
            CustomRecord.Create(Tid, lines.Id, $$"""{"order":"{{parentId}}","amount":10}""", null),
            CustomRecord.Create(Tid, lines.Id, $$"""{"order":"{{parentId}}","amount":5}""", null),
            CustomRecord.Create(Tid, lines.Id, $$"""{"order":"{{other}}","amount":99}""", null)
        };

        var entities = new FakeEntityRepo(); entities.Add(lines);
        var records = new FakeRecordRepo(); records.Seed(lines.Id, children);

        var fields = new[]
        {
            Field("total", CustomFieldType.Rollup, StudioLookupRollup.SerializeRollup("lines", "order", "sum", "amount")),
            Field("count", CustomFieldType.Rollup, StudioLookupRollup.SerializeRollup("lines", "order", "count", null))
        };
        var dto = new CustomRecordDto(parentId, new JsonObject(), DateTime.UtcNow, DateTime.UtcNow, null);

        await Reader(entities, records, new FakeExistingProvider()).EnrichAsync(Tid, fields, new[] { dto });

        var data = (JsonObject)dto.Data!;
        Assert.Equal(15m, (decimal)data["total"]!);
        Assert.Equal(2m, (decimal)data["count"]!);
    }

    [Fact]
    public async Task No_computed_fields_is_a_noop()
    {
        var fields = new[] { Field("name", CustomFieldType.Text) };
        var dto = new CustomRecordDto(Guid.NewGuid(), new JsonObject { ["name"] = "X" }, DateTime.UtcNow, DateTime.UtcNow, null);

        await Reader(new FakeEntityRepo(), new FakeRecordRepo(), new FakeExistingProvider()).EnrichAsync(Tid, fields, new[] { dto });

        Assert.Equal("X", (string?)((JsonObject)dto.Data!)["name"]);
        Assert.Single((JsonObject)dto.Data!);
    }
}
