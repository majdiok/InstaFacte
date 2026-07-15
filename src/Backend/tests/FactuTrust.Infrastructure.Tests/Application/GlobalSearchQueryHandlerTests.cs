using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Search;
using FactuTrust.Application.Features.Search.Queries;
using FactuTrust.Domain.Enums;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GlobalSearchQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenQueryTooShort_ReturnsEmptyResults()
    {
        var handler = CreateHandler(Array.Empty<IGlobalSearchProvider>(), [Permissions.Invoices.Read]);
        var result = await handler.Handle(new GlobalSearchQuery("a"), CancellationToken.None);
        Assert.Equal("a", result.Query);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task Handle_WhenUserLacksInvoicePermission_SkipsInvoiceProvider()
    {
        var invoiceProvider = new Mock<IGlobalSearchProvider>();
        invoiceProvider.SetupGet(p => p.EntityType).Returns(SearchEntityType.Invoice);
        invoiceProvider.SetupGet(p => p.RequiredPermission).Returns(Permissions.Invoices.Read);
        invoiceProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GlobalSearchProviderResult>());

        var handler = CreateHandler([invoiceProvider.Object], [Permissions.Clients.Read]);
        var result = await handler.Handle(new GlobalSearchQuery("FAC"), CancellationToken.None);
        Assert.Empty(result.Results);
        invoiceProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GlobalSearchQueryHandler CreateHandler(IReadOnlyList<IGlobalSearchProvider> providers, IReadOnlyList<string> permissions)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.HasPermission(It.IsAny<string>())).Returns<string>(permissions.Contains);
        return new GlobalSearchQueryHandler(providers, currentUser.Object);
    }
}
