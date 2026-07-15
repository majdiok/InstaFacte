using System.Text.Json.Nodes;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Studio.Automations;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioBridgeMapperTests
{
    private static AiToolDefinition Tool(params string[] required) => new()
    {
        Name = "generate_invoice",
        Description = "x",
        Parameters = new()
        {
            ["client_id"] = new() { Type = "string", Description = "" },
            ["quantity"] = new() { Type = "number", Description = "" },
            ["reference"] = new() { Type = "string", Description = "" }
        },
        RequiredParameters = required.ToList()
    };

    [Fact]
    public void Build_maps_fields_and_consts()
    {
        var data = new JsonObject { ["client"] = "11111111-1111-1111-1111-111111111111", ["qte"] = 2 };
        var mappings = new[]
        {
            new BridgeParamMapping("client_id", "field", "client"),
            new BridgeParamMapping("quantity", "field", "qte"),
            new BridgeParamMapping("reference", "const", "ABO-2026")
        };

        var (args, err) = StudioBridgeMapper.Build(mappings, data, Tool("client_id", "quantity"));

        Assert.Null(err);
        Assert.Equal("11111111-1111-1111-1111-111111111111", args["client_id"]);
        Assert.Equal("2", args["quantity"]); // numbers are read as their string form for the tool helpers
        Assert.Equal("ABO-2026", args["reference"]);
    }

    [Fact]
    public void Build_fails_when_required_param_missing_or_empty()
    {
        var data = new JsonObject(); // no "client" field
        var (_, err) = StudioBridgeMapper.Build(
            new[] { new BridgeParamMapping("client_id", "field", "client") }, data, Tool("client_id"));
        Assert.NotNull(err);
        Assert.Contains("client_id", err);
    }

    [Fact]
    public void Build_skips_absent_optional_fields()
    {
        var data = new JsonObject { ["client"] = "g" };
        var (args, err) = StudioBridgeMapper.Build(
            new[]
            {
                new BridgeParamMapping("client_id", "field", "client"),
                new BridgeParamMapping("reference", "field", "absent_field")
            },
            data, Tool("client_id"));
        Assert.Null(err);
        Assert.True(args.ContainsKey("client_id"));
        Assert.False(args.ContainsKey("reference"));
    }

    [Fact]
    public void ParseMappings_roundtrips_camelCase()
    {
        const string json = """[{"param":"client_id","source":"field","value":"client"},{"param":"reference","source":"const","value":"X"}]""";
        var m = StudioBridgeMapper.ParseMappings(json);
        Assert.Equal(2, m.Count);
        Assert.Equal("client_id", m[0].Param);
        Assert.Equal("field", m[0].Source);
        Assert.Equal("client", m[0].Value);
        Assert.Equal("const", m[1].Source);
    }

    [Fact]
    public void ParseMappings_tolerates_garbage()
    {
        Assert.Empty(StudioBridgeMapper.ParseMappings(null));
        Assert.Empty(StudioBridgeMapper.ParseMappings("not json"));
        Assert.Empty(StudioBridgeMapper.ParseMappings("{}"));
    }
}
