using System.Text.Json;
using FactuTrust.Application.Features.Studio.Ai;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioTextToolCallRecoveryTests
{
    private const string SpecJson =
        "{\"system\":{\"displayName\":\"Gestion des Congés\"},\"entities\":[{\"ref\":\"employes\",\"displayName\":\"Employés\",\"fields\":[{\"label\":\"Nom\",\"type\":\"text\"}]}]}";

    [Fact]
    public void Extracts_bare_envelope_with_object_arguments()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "studio_generate_system",
            arguments = new { spec_json = SpecJson }
        });

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal("studio_generate_system", name);
        Assert.Contains("\"system\"", spec);
        // The extracted spec is valid JSON.
        using var doc = JsonDocument.Parse(spec);
        Assert.True(doc.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public void Extracts_from_prose_and_json_fence()
    {
        var envelope = JsonSerializer.Serialize(new
        {
            name = "studio_generate_system",
            arguments = new { spec_json = SpecJson }
        });
        var content = "Voici la spécification du système :\n```json\n" + envelope + "\n```\nJe lance la création.";

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal("studio_generate_system", name);
        Assert.Contains("employes", spec);
    }

    [Fact]
    public void Extracts_when_arguments_is_a_json_string()
    {
        var argsString = JsonSerializer.Serialize(new Dictionary<string, object?> { ["spec_json"] = SpecJson });
        var content = JsonSerializer.Serialize(new { name = "studio_generate_system", arguments = argsString });

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal("studio_generate_system", name);
        Assert.Contains("\"system\"", spec);
    }

    [Fact]
    public void Extracts_openai_function_wrapper_for_generate_app()
    {
        var appSpec = "{\"entity\":{\"displayName\":\"Contrats\"},\"fields\":[{\"label\":\"Montant\",\"type\":\"money\"}]}";
        var argsString = JsonSerializer.Serialize(new Dictionary<string, object?> { ["spec_json"] = appSpec });
        var content = JsonSerializer.Serialize(new
        {
            type = "function",
            function = new { name = "studio_generate_app", arguments = argsString }
        });

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal("studio_generate_app", name);
        Assert.Contains("\"entity\"", spec);
    }

    [Fact]
    public void Extracts_when_spec_json_is_inlined_as_object()
    {
        var content = "{\"name\":\"studio_generate_system\",\"arguments\":{\"spec_json\":" + SpecJson + "}}";

        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal("studio_generate_system", name);
        using var doc = JsonDocument.Parse(spec);
        Assert.True(doc.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public void Returns_false_for_plain_prose()
    {
        const string content = "Bien sûr, je vais créer un système de gestion de congés avec plusieurs tables.";
        Assert.False(StudioTextToolCallRecovery.TryExtract(content, out var name, out var spec));
        Assert.Equal(string.Empty, name);
        Assert.Equal(string.Empty, spec);
    }

    [Fact]
    public void Returns_false_for_non_whitelisted_tool()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "studio_query_records",
            arguments = new { entity = "employes" }
        });
        Assert.False(StudioTextToolCallRecovery.TryExtract(content, out _, out _));
    }

    [Fact]
    public void Returns_false_when_spec_json_missing()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "studio_generate_system",
            arguments = new { foo = "bar" }
        });
        Assert.False(StudioTextToolCallRecovery.TryExtract(content, out _, out _));
    }

    [Fact]
    public void TryExtractBareStudioSpec_App_Complete()
    {
        const string spec =
            "{\"entity\":{\"displayName\":\"Contrats Clients\",\"displayNamePlural\":\"Contrats Clients\"},\"fields\":[{\"label\":\"Date de début\",\"type\":\"date\"},{\"label\":\"Montant\",\"type\":\"money\"}]}";

        Assert.True(StudioTextToolCallRecovery.TryExtractBareStudioSpec(spec, out var kind, out var extracted));
        Assert.Equal(BareStudioSpecKind.App, kind);
        using var doc = JsonDocument.Parse(extracted);
        Assert.True(doc.RootElement.TryGetProperty("entity", out _));
        Assert.True(doc.RootElement.TryGetProperty("fields", out _));
        Assert.False(StudioTextToolCallRecovery.TryExtract(spec, out _, out _));
    }

    [Fact]
    public void TryExtractBareStudioSpec_System_Complete()
    {
        Assert.True(StudioTextToolCallRecovery.TryExtractBareStudioSpec(SpecJson, out var kind, out var extracted));
        Assert.Equal(BareStudioSpecKind.System, kind);
        using var doc = JsonDocument.Parse(extracted);
        Assert.True(doc.RootElement.TryGetProperty("system", out _));
        Assert.True(doc.RootElement.TryGetProperty("entities", out _));
    }

    [Fact]
    public void TryExtractBareStudioSpec_Truncated_ReturnsFalse()
    {
        const string truncated =
            "{\n  \"entity\": {\n    \"displayName\": \"Contrats Clients\"\n  },\n  \"fields\": [\n    {\n      \"label\": \"Montant\"";

        Assert.False(StudioTextToolCallRecovery.TryExtractBareStudioSpec(truncated, out var kind, out var spec));
        Assert.Equal(default, kind);
        Assert.Equal(string.Empty, spec);
        Assert.True(StudioTextToolCallRecovery.LooksLikeStudioSpecText(truncated));
    }

    [Fact]
    public void TryExtractBareStudioSpec_Ignores_ExistingToolEnvelope()
    {
        var content = JsonSerializer.Serialize(new
        {
            name = "studio_generate_app",
            arguments = new { spec_json = "{\"entity\":{\"displayName\":\"X\"},\"fields\":[{\"label\":\"A\",\"type\":\"text\"}]}" }
        });

        Assert.False(StudioTextToolCallRecovery.TryExtractBareStudioSpec(content, out _, out _));
        Assert.True(StudioTextToolCallRecovery.TryExtract(content, out var name, out _));
        Assert.Equal("studio_generate_app", name);
    }

    [Fact]
    public void ResolveToolName_HonorsPlanPreview()
    {
        Assert.Equal("studio_plan_app", StudioTextToolCallRecovery.ResolveToolName(BareStudioSpecKind.App, planPreview: true));
        Assert.Equal("studio_generate_app", StudioTextToolCallRecovery.ResolveToolName(BareStudioSpecKind.App, planPreview: false));
        Assert.Equal("studio_plan_system", StudioTextToolCallRecovery.ResolveToolName(BareStudioSpecKind.System, planPreview: true));
        Assert.Equal("studio_generate_system", StudioTextToolCallRecovery.ResolveToolName(BareStudioSpecKind.System, planPreview: false));
    }
}
