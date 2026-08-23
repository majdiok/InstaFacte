using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Products.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GenerateProductVariantsCommandHandlerTests
{
    [Fact]
    public async Task Handle_CartesianMatrix_CreatesChildSkusAndExcludesTemplateFromStock()
    {
        var parent = Product.Create(
            "TSHIRT",
            "T-shirt",
            ProductType.Product,
            Money.Create(20m, Money.DefaultCurrency),
            VatRate.Standard,
            Guid.NewGuid(),
            isStockManaged: true).Value;

        var size = ProductAttributeDefinition.Create("SIZE", "Taille").Value;
        Assert.True(size.AddValue("M", "M", 0).IsSuccess);
        Assert.True(size.AddValue("L", "L", 1).IsSuccess);
        var sizeIds = size.Values.Select(v => v.Id).ToList();

        var color = ProductAttributeDefinition.Create("COLOR", "Couleur").Value;
        Assert.True(color.AddValue("BLU", "Bleu", 0).IsSuccess);
        Assert.True(color.AddValue("RED", "Rouge", 1).IsSuccess);
        var colorIds = color.Values.Select(v => v.Id).ToList();

        var products = new Mock<IProductRepository>();
        products.Setup(p => p.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        products.Setup(p => p.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        var created = new List<Product>();
        products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => created.Add(p))
            .Returns<Product, CancellationToken>((p, _) => Task.FromResult(p));
        products.Setup(p => p.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var attributes = new Mock<IProductAttributeRepository>();
        attributes.Setup(a => a.GetByIdWithValuesAsync(size.Id, It.IsAny<CancellationToken>())).ReturnsAsync(size);
        attributes.Setup(a => a.GetByIdWithValuesAsync(color.Id, It.IsAny<CancellationToken>())).ReturnsAsync(color);
        attributes.Setup(a => a.ListAxesAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProductVariantAxis>());
        attributes.Setup(a => a.AddAxisAsync(It.IsAny<ProductVariantAxis>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        attributes.Setup(a => a.AddVariantLinkAsync(It.IsAny<ProductVariantAttributeValue>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new GenerateProductVariantsCommandHandler(products.Object, attributes.Object, currentUser.Object);
        var result = await handler.Handle(
            new GenerateProductVariantsCommand(
                parent.Id,
                new[]
                {
                    new GenerateProductVariantAxis(size.Id, sizeIds),
                    new GenerateProductVariantAxis(color.Id, colorIds)
                }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(4, result.Value.Count);
        Assert.True(parent.IsVariantTemplate);
        Assert.False(parent.IsStockManaged);
        Assert.Equal(4, created.Count);
        Assert.All(created, child =>
        {
            Assert.Equal(parent.Id, child.ParentProductId);
            Assert.False(child.IsVariantTemplate);
            Assert.True(child.IsStockManaged);
            Assert.StartsWith("TSHIRT-", child.Code);
        });
        Assert.Contains(created, c => c.Code == "TSHIRT-M-BLU");
        Assert.Contains(created, c => c.Code == "TSHIRT-L-RED");
    }

    [Fact]
    public async Task Handle_ExistingChildSku_IsSkippedOnRegenerate()
    {
        var parent = Product.Create(
            "TSHIRT",
            "T-shirt",
            ProductType.Product,
            Money.Create(20m, Money.DefaultCurrency),
            VatRate.Standard,
            Guid.NewGuid()).Value;
        Assert.True(parent.MarkAsVariantTemplate().IsSuccess);

        var existing = Product.Create(
            "TSHIRT-M",
            "T-shirt (M)",
            ProductType.Product,
            Money.Create(20m, Money.DefaultCurrency),
            VatRate.Standard,
            parent.CategoryId,
            isStockManaged: true).Value;
        Assert.True(existing.AttachToParent(parent.Id).IsSuccess);

        var size = ProductAttributeDefinition.Create("SIZE", "Taille").Value;
        Assert.True(size.AddValue("M", "M").IsSuccess);

        var products = new Mock<IProductRepository>();
        products.Setup(p => p.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        products.Setup(p => p.GetByCodeAsync("TSHIRT-M", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var attributes = new Mock<IProductAttributeRepository>();
        attributes.Setup(a => a.GetByIdWithValuesAsync(size.Id, It.IsAny<CancellationToken>())).ReturnsAsync(size);
        attributes.Setup(a => a.ListAxesAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProductVariantAxis>());
        attributes.Setup(a => a.AddAxisAsync(It.IsAny<ProductVariantAxis>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GenerateProductVariantsCommandHandler(
            products.Object,
            attributes.Object,
            new Mock<ICurrentUser>().Object);

        var result = await handler.Handle(
            new GenerateProductVariantsCommand(
                parent.Id,
                new[] { new GenerateProductVariantAxis(size.Id, size.Values.Select(v => v.Id).ToList()) }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        products.Verify(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
