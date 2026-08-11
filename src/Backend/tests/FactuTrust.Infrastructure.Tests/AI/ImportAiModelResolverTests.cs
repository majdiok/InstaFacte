using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ImportAiModelResolverTests
{
    [Fact]
    public void TryResolvePlatformImportModel_rejects_embedding_model()
    {
        var ok = ImportAiModelResolver.TryResolvePlatformImportModel(
            "ollama:nomic-embed-text:latest",
            NullLogger.Instance,
            out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolvePlatformImportModel_accepts_chat_model()
    {
        var ok = ImportAiModelResolver.TryResolvePlatformImportModel(
            "qwen2.5:7b-instruct",
            NullLogger.Instance,
            out var modelRef);

        Assert.True(ok);
        Assert.Contains("qwen2.5", modelRef.ProviderModelId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveServerImportModel_uses_invoice_import_then_default()
    {
        var settings = new OllamaSettings
        {
            InvoiceImportModel = "qwen2.5:7b-instruct",
            DefaultModel = "mistral"
        };

        var resolved = ImportAiModelResolver.ResolveServerImportModel(settings);
        Assert.Contains("qwen2.5", resolved.ProviderModelId, StringComparison.OrdinalIgnoreCase);
    }
}
