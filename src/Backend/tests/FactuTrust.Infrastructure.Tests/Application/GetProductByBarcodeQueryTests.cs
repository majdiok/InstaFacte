using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GetProductByBarcodeQueryTests
{
    [Fact]
    public async Task Handle_VariantTemplate_IsRefused()
    {
        var product = Product.Create(
            "TSHIRT",
            "T-shirt",
            ProductType.Product,
            Money.Create(20m, Money.DefaultCurrency),
            VatRate.Standard,
            Guid.NewGuid()).Value;
        Assert.True(product.MarkAsVariantTemplate().IsSuccess);

        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.GetByBarcodeAsync("4006381333931", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = new GetProductByBarcodeQueryHandler(repo.Object);
        var result = await handler.Handle(new GetProductByBarcodeQuery("4006381333931"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("variante", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}
