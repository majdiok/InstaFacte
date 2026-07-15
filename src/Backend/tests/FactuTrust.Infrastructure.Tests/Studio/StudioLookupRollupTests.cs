using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioLookupRollupTests
{
    private static readonly Guid Tid = Guid.NewGuid();
    private static readonly Guid Eid = Guid.NewGuid();

    private static CustomFieldDefinition Field(string key, CustomFieldType type, string? optionsJson = null) =>
        CustomFieldDefinition.Create(Tid, Eid, key, key, type, false, false, 0, null, optionsJson, null, null);

    [Fact]
    public void Lookup_roundtrips()
    {
        var json = StudioLookupRollup.SerializeLookup("client", "name");
        var cfg = StudioLookupRollup.ParseLookup(json);
        Assert.NotNull(cfg);
        Assert.Equal("client", cfg!.Via);
        Assert.Equal("name", cfg.Target);
    }

    [Fact]
    public void Rollup_roundtrips()
    {
        var json = StudioLookupRollup.SerializeRollup("lines", "order", "SUM", "amount");
        var cfg = StudioLookupRollup.ParseRollup(json);
        Assert.NotNull(cfg);
        Assert.Equal("lines", cfg!.Entity);
        Assert.Equal("order", cfg.RelationField);
        Assert.Equal("sum", cfg.Agg);
        Assert.Equal("amount", cfg.Field);
    }

    [Fact]
    public void Lookup_via_must_be_a_relation_field()
    {
        var siblings = new[]
        {
            Field("client", CustomFieldType.RelationCustom, StudioFieldJson.SerializeRelation(new RelationRefDto("custom", "contacts"))),
            Field("note", CustomFieldType.Text)
        };
        Assert.Null(StudioLookupRollup.ValidateLookup("client", "name", siblings));
        Assert.NotNull(StudioLookupRollup.ValidateLookup("note", "name", siblings));   // not a relation
        Assert.NotNull(StudioLookupRollup.ValidateLookup("missing", "name", siblings)); // unknown
    }

    [Fact]
    public void Rollup_validation_checks_agg_and_field()
    {
        Assert.Null(StudioLookupRollup.ValidateRollup(new RollupConfig("lines", "order", "count", null)));
        Assert.Null(StudioLookupRollup.ValidateRollup(new RollupConfig("lines", "order", "sum", "amount")));
        Assert.NotNull(StudioLookupRollup.ValidateRollup(new RollupConfig("lines", "order", "bogus", "amount")));
        Assert.NotNull(StudioLookupRollup.ValidateRollup(new RollupConfig("lines", "order", "sum", null))); // sum needs a field
    }
}
