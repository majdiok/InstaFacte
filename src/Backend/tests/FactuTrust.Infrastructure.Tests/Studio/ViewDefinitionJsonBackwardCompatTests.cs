using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class ViewDefinitionJsonBackwardCompatTests
{
    [Fact]
    public void Parse_legacy_json_without_format_succeeds()
    {
        const string json = """{"columns":[{"name":"Number","label":"Numéro"}],"search":true}""";

        var def = ViewDefinitionJson.Parse(json);

        Assert.Single(def.Columns);
        Assert.Equal("Number", def.Columns[0].Name);
        Assert.Equal("Numéro", def.Columns[0].Label);
        Assert.Null(def.Columns[0].Format);
        Assert.Null(def.Columns[0].FormatOptions);
        Assert.True(def.Search);
    }

    [Fact]
    public void Parse_json_with_format_options()
    {
        const string json = """
            {"columns":[{"name":"Status","format":"status","formatOptions":{"statusMap":{"1":"Brouillon"}}}]}
            """;

        var def = ViewDefinitionJson.Parse(json);

        Assert.Equal("status", def.Columns[0].Format);
        Assert.NotNull(def.Columns[0].FormatOptions?.StatusMap);
        Assert.Equal("Brouillon", def.Columns[0].FormatOptions!.StatusMap!["1"]);
    }
}