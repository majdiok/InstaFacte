using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Lot 1 — déterminisme : seed/top_p ne doivent être émis QUE s'ils sont renseignés, sinon la requête Ollama
/// reste identique à l'historique (rétrocompatibilité du format JSON).
/// </summary>
public sealed class OllamaOptionsSerializationTests
{
    [Fact]
    public void Omits_Seed_And_TopP_When_Null()
    {
        var json = JsonSerializer.Serialize(new OllamaOptions { Temperature = 0.2 });

        Assert.DoesNotContain("seed", json);
        Assert.DoesNotContain("top_p", json);
    }

    [Fact]
    public void Emits_Seed_When_Set()
    {
        var json = JsonSerializer.Serialize(new OllamaOptions { Temperature = 0.2, Seed = 42 });

        Assert.Contains("\"seed\":42", json);
    }

    [Fact]
    public void Emits_TopP_When_Set()
    {
        var json = JsonSerializer.Serialize(new OllamaOptions { Temperature = 0.2, TopP = 0.9 });

        Assert.Contains("\"top_p\":0.9", json);
    }

    [Fact]
    public void Emits_NumGpu_When_Zero_ForCpuOnly()
    {
        var json = JsonSerializer.Serialize(new OllamaOptions { Temperature = 0.2, NumGpu = 0 });

        Assert.Contains("\"num_gpu\":0", json);
    }

    [Fact]
    public void Omits_NumGpu_When_Null_ForGpuMode()
    {
        var json = JsonSerializer.Serialize(new OllamaOptions { Temperature = 0.2, NumGpu = null });

        Assert.DoesNotContain("num_gpu", json);
    }
}
