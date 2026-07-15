using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class SupplierWithholdingDefaultsResolverTests
{
    private static readonly Guid Rs7Normal25Id = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid Rs7Reduced15Id = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid Rs9AssistanceId = Guid.Parse("22222222-2222-2222-2222-222222222204");

    private static WithholdingTaxType Rs7Type(string code, Guid id)
    {
        var type = WithholdingTaxType.CreateSystem(
            code,
            WithholdingCategory.Achats,
            $"Label {code}",
            1m);
        typeof(WithholdingTaxType).GetProperty(nameof(WithholdingTaxType.Id))!
            .SetValue(type, id);
        return type;
    }

    private static Mock<IWithholdingTaxRepository> CreateRepository(
        WithholdingTaxType? rs7Reduced15 = null,
        WithholdingTaxType? explicitType = null)
    {
        var mock = new Mock<IWithholdingTaxRepository>();

        mock.Setup(r => r.GetTypeByCodeAsync("RS7_000002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rs7Reduced15);

        mock.Setup(r => r.GetTypeByCodeAsync(It.Is<string>(c => c != "RS7_000002"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WithholdingTaxType?)null);

        if (explicitType is not null)
        {
            mock.Setup(r => r.GetTypeByIdAsync(explicitType.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(explicitType);
        }

        mock.Setup(r => r.GetTypeByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                explicitType is not null && explicitType.Id == id ? explicitType : null);

        return mock;
    }

    [Fact]
    public async Task ResolveWithResult_BracketOnly_Reduced15_ReturnsRs7CatalogId()
    {
        var rs7 = Rs7Type("RS7_000002", Rs7Reduced15Id);
        var repo = CreateRepository(rs7Reduced15: rs7);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            null,
            SupplierRs7IsBracket.Reduced15,
            repo.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal(Rs7Reduced15Id, result.Value);
    }

    [Fact]
    public async Task ResolveWithResult_UnspecifiedBracket_ReturnsNull()
    {
        var repo = CreateRepository();

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            null,
            SupplierRs7IsBracket.Unspecified,
            repo.Object);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ResolveWithResult_ConsistentRs7ExplicitAndBracket_ReturnsExplicitId()
    {
        var rs7 = Rs7Type("RS7_000002", Rs7Reduced15Id);
        var repo = CreateRepository(explicitType: rs7);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            Rs7Reduced15Id,
            SupplierRs7IsBracket.Reduced15,
            repo.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal(Rs7Reduced15Id, result.Value);
    }

    [Fact]
    public async Task ResolveWithResult_InconsistentRs7ExplicitAndBracket_Fails()
    {
        var rs7 = Rs7Type("RS7_000002", Rs7Reduced15Id);
        var repo = CreateRepository(explicitType: rs7);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            Rs7Reduced15Id,
            SupplierRs7IsBracket.Normal25,
            repo.Object);

        Assert.True(result.IsFailure);
        Assert.Contains("ne correspond pas", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveWithResult_NonRs7ExplicitWithBracket_Fails()
    {
        var rs9 = Rs7Type("RS9_000004", Rs9AssistanceId);
        var repo = CreateRepository(explicitType: rs9);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            Rs9AssistanceId,
            SupplierRs7IsBracket.Reduced15,
            repo.Object);

        Assert.True(result.IsFailure);
        Assert.Contains("simultanément", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveWithResult_BracketOnly_MissingCatalog_Fails()
    {
        var repo = CreateRepository(rs7Reduced15: null);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            null,
            SupplierRs7IsBracket.Reduced15,
            repo.Object);

        Assert.True(result.IsFailure);
        Assert.Contains("RS7_000002", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveWithResult_GuidEmptyWithBracket_ResolvesViaBracket()
    {
        var rs7 = Rs7Type("RS7_000002", Rs7Reduced15Id);
        var repo = CreateRepository(rs7Reduced15: rs7);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            Guid.Empty,
            SupplierRs7IsBracket.Reduced15,
            repo.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal(Rs7Reduced15Id, result.Value);
    }

    [Fact]
    public async Task ResolveWithResult_ExplicitOnly_ReturnsExplicitId()
    {
        var rs9 = Rs7Type("RS9_000004", Rs9AssistanceId);
        var repo = CreateRepository(explicitType: rs9);

        var result = await SupplierWithholdingDefaultsResolver.ResolveWithResultAsync(
            Rs9AssistanceId,
            null,
            repo.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal(Rs9AssistanceId, result.Value);
    }

    [Theory]
    [InlineData("RS7_000001", SupplierRs7IsBracket.Normal25)]
    [InlineData("RS7_000002", SupplierRs7IsBracket.Reduced15)]
    [InlineData("RS7_000003", SupplierRs7IsBracket.Reduced10)]
    [InlineData("RS9_000004", null)]
    public void InferRs7BracketFromTypeCode_MapsKnownCodes(string code, SupplierRs7IsBracket? expected)
    {
        Assert.Equal(expected, SupplierWithholdingDefaultsResolver.InferRs7BracketFromTypeCode(code));
    }
}
