using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class InvoiceImportNumCtxTests
{
    [Fact]
    public void ResolveImportNumCtx_ShortPrompt_UsesReducedContext()
    {
        var result = InvoiceImportParsing.ResolveImportNumCtx(16384, 1000, 1536, hasImages: false);
        Assert.NotNull(result);
        Assert.True(result < 16384);
        Assert.True(result >= 4096);
    }

    [Fact]
    public void ResolveImportNumCtx_WithImages_UsesFullConfiguredContext()
    {
        Assert.Equal(16384, InvoiceImportParsing.ResolveImportNumCtx(16384, 1000, 1536, hasImages: true));
    }

    [Fact]
    public void ResolveImportNumCtx_ZeroConfigured_ReturnsNull()
    {
        Assert.Null(InvoiceImportParsing.ResolveImportNumCtx(0, 5000, 1536, hasImages: false));
    }
}
