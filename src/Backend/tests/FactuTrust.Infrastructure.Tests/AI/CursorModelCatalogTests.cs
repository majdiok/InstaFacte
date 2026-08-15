using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class CursorModelCatalogTests
{
    [Fact]
    public void ToUnifiedModels_ExplodesVariants_WithCursorProviderKey()
    {
        var models = new[]
        {
            new CursorRemoteModelInfo(
                "composer-2.5",
                "Composer 2.5",
                "desc",
                Array.Empty<string>(),
                Array.Empty<CursorRemoteModelParameter>(),
                [
                    new CursorRemoteModelVariant(
                        [new CursorModelParam("fast", "true")],
                        "Composer 2.5 Fast",
                        null,
                        false),
                    new CursorRemoteModelVariant(
                        Array.Empty<CursorModelParam>(),
                        "Composer 2.5",
                        null,
                        true)
                ])
        };

        var unified = CursorModelCatalog.ToUnifiedModels(models);

        Assert.Equal(2, unified.Count);
        Assert.All(unified, m => Assert.Equal("cursor", m.ProviderKey));
        Assert.All(unified, m => Assert.True(m.SupportsVision));
        Assert.All(unified, m => Assert.True(m.SupportsChat));
        Assert.Contains(unified, m => m.ModelRef == "cursor:composer-2.5|fast=true");
        Assert.Contains(unified, m => m.ModelRef == "cursor:composer-2.5");
        Assert.Contains(unified, m => m.DisplayLabel == "Composer 2.5 Fast");
    }
}
