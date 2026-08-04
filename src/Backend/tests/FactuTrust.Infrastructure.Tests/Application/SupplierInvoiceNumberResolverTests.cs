using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.SupplierInvoices.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class SupplierInvoiceNumberResolverTests
{
    [Fact]
    public async Task ResolveAsync_UseSuggestedNumber_ReservesFromService()
    {
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-000001");

        var result = await SupplierInvoiceNumberResolver.ResolveAsync(
            "FS-OLD",
            useSuggestedNumber: true,
            new DateTime(2026, 4, 5),
            numberService.Object,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FS-2026-000001", result.Value);
        numberService.Verify(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_EmptyNumber_ReservesFromService()
    {
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-000002");

        var result = await SupplierInvoiceNumberResolver.ResolveAsync(
            "",
            useSuggestedNumber: false,
            new DateTime(2026, 4, 5),
            numberService.Object,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FS-2026-000002", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_ManualUniqueNumber_UsesProvidedValue()
    {
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.IsAvailableAsync("FS-CUSTOM-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await SupplierInvoiceNumberResolver.ResolveAsync(
            "FS-CUSTOM-1",
            useSuggestedNumber: false,
            new DateTime(2026, 4, 5),
            numberService.Object,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FS-CUSTOM-1", result.Value);
        numberService.Verify(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_ManualDuplicate_ReturnsConflictWithSuggestedNumberMetadata()
    {
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.IsAvailableAsync("FS-2026-W8F6P", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        numberService
            .Setup(s => s.PreviewNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-000100");

        var result = await SupplierInvoiceNumberResolver.ResolveAsync(
            "FS-2026-W8F6P",
            useSuggestedNumber: false,
            new DateTime(2026, 4, 5),
            numberService.Object,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Contains("FS-2026-W8F6P", result.Error.Description);
        Assert.NotNull(result.Error.Metadata);
        Assert.Equal("FS-2026-000100", result.Error.Metadata!["suggestedInvoiceNumber"]);
        Assert.Equal("FS-2026-W8F6P", result.Error.Metadata!["conflictingInvoiceNumber"]);
    }

    [Fact]
    public async Task ResolveAsync_ManualDuplicate_PreviewFailure_ReturnsConflictWithNullSuggested()
    {
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.IsAvailableAsync("FS-DUP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        numberService
            .Setup(s => s.PreviewNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unreachable"));

        var result = await SupplierInvoiceNumberResolver.ResolveAsync(
            "FS-DUP",
            useSuggestedNumber: false,
            new DateTime(2026, 4, 5),
            numberService.Object,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.NotNull(result.Error.Metadata);
        Assert.Null(result.Error.Metadata!["suggestedInvoiceNumber"]);
    }
}
