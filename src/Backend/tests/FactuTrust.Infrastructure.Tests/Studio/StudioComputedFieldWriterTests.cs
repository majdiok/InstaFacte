using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Studio;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioComputedFieldWriterTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    /// <summary>Deterministic allocator: hands out 1, 2, 3, … per field key and records call counts.</summary>
    private sealed class FakeAllocator : ICustomSequenceAllocator
    {
        private readonly Dictionary<string, long> _counters = new();
        public int Calls { get; private set; }

        public Task<long> ReserveNextAsync(Guid tenantId, Guid entityId, string fieldKey, CancellationToken ct = default)
        {
            Calls++;
            _counters.TryGetValue(fieldKey, out var v);
            v += 1;
            _counters[fieldKey] = v;
            return Task.FromResult(v);
        }
    }

    private static CustomFieldDefinition AutoField(string key, string? optionsJson = null) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.AutoNumber, false, false, 0, null, optionsJson, null, null);

    private static CustomFieldDefinition TextField(string key) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Text, false, false, 1, null, null, null, null);

    private static CustomFieldDefinition DecimalField(string key) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Decimal, false, false, 1, null, null, null, null);

    private static CustomFieldDefinition FormulaField(string key, string expr) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, CustomFieldType.Formula, false, false, 2, null, StudioFormula.Serialize(expr), null, null);

    [Fact]
    public async Task Create_allocates_autonumber_into_json()
    {
        var alloc = new FakeAllocator();
        var writer = new StudioComputedFieldWriter(alloc);
        var fields = new[] { AutoField("ref", """{"number":{"prefix":"R-","padding":3,"suffix":""}}"""), TextField("name") };

        var json = await writer.ApplyOnCreateAsync(Tid, Eid, fields, """{"name":"Acme"}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("R-001", (string?)obj["ref"]);
        Assert.Equal("Acme", (string?)obj["name"]);
        Assert.Equal(1, alloc.Calls);
    }

    [Fact]
    public async Task Create_without_autonumber_field_does_not_call_allocator()
    {
        var alloc = new FakeAllocator();
        var writer = new StudioComputedFieldWriter(alloc);
        var fields = new[] { TextField("name") };

        var json = await writer.ApplyOnCreateAsync(Tid, Eid, fields, """{"name":"Acme"}""");

        Assert.Equal("""{"name":"Acme"}""", json);
        Assert.Equal(0, alloc.Calls);
    }

    [Fact]
    public async Task Update_preserves_existing_autonumber_and_does_not_reallocate()
    {
        var alloc = new FakeAllocator();
        var writer = new StudioComputedFieldWriter(alloc);
        var fields = new[] { AutoField("ref"), TextField("name") };

        var json = await writer.ApplyOnUpdateAsync(
            Tid, Eid, fields,
            canonicalJson: """{"name":"Renamed"}""",
            existingJson: """{"ref":"R-001","name":"Acme"}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("R-001", (string?)obj["ref"]); // immutable, carried over
        Assert.Equal("Renamed", (string?)obj["name"]);
        Assert.Equal(0, alloc.Calls); // never reallocates an existing value
    }

    [Fact]
    public async Task Update_allocates_when_record_predates_the_field()
    {
        var alloc = new FakeAllocator();
        var writer = new StudioComputedFieldWriter(alloc);
        var fields = new[] { AutoField("ref"), TextField("name") };

        var json = await writer.ApplyOnUpdateAsync(
            Tid, Eid, fields,
            canonicalJson: """{"name":"Acme"}""",
            existingJson: """{"name":"Acme"}"""); // no prior ref

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("0001", (string?)obj["ref"]);
        Assert.Equal(1, alloc.Calls);
    }

    [Fact]
    public async Task Create_computes_formula_from_input()
    {
        var writer = new StudioComputedFieldWriter(new FakeAllocator());
        var fields = new[] { DecimalField("ht"), FormulaField("ttc", "ht * 1.19") };

        var json = await writer.ApplyOnCreateAsync(Tid, Eid, fields, """{"ht":100}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(119m, (decimal)obj["ttc"]!);
    }

    [Fact]
    public async Task Create_computes_dependent_formulas_in_topological_order()
    {
        var writer = new StudioComputedFieldWriter(new FakeAllocator());
        // ttc depends on tva, which depends on ht — regardless of declaration order.
        var fields = new[]
        {
            FormulaField("ttc", "ht + tva"),
            FormulaField("tva", "ht * 0.19"),
            DecimalField("ht")
        };

        var json = await writer.ApplyOnCreateAsync(Tid, Eid, fields, """{"ht":100}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(19m, (decimal)obj["tva"]!);
        Assert.Equal(119m, (decimal)obj["ttc"]!);
    }

    [Fact]
    public async Task Update_recomputes_formula_from_new_input()
    {
        var writer = new StudioComputedFieldWriter(new FakeAllocator());
        var fields = new[] { DecimalField("ht"), FormulaField("ttc", "ht * 1.19") };

        var json = await writer.ApplyOnUpdateAsync(
            Tid, Eid, fields,
            canonicalJson: """{"ht":200}""",
            existingJson: """{"ht":100,"ttc":119}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(238m, (decimal)obj["ttc"]!); // recomputed, not preserved
    }

    [Fact]
    public async Task Formula_with_unevaluable_input_is_omitted()
    {
        var writer = new StudioComputedFieldWriter(new FakeAllocator());
        var fields = new[] { DecimalField("ht"), FormulaField("ttc", "ht * 1.19") };

        // ht absent → formula cannot evaluate → ttc not stored (rather than garbage).
        var json = await writer.ApplyOnCreateAsync(Tid, Eid, fields, """{}""");

        var obj = JsonNode.Parse(json)!.AsObject();
        Assert.False(obj.ContainsKey("ttc"));
    }
}
