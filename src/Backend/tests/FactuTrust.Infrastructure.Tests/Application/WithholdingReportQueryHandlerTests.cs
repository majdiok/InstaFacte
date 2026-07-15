using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class WithholdingReportQueryHandlerTests
{
    [Fact]
    public async Task GetClientWithholdingsReportQuery_ShouldReturnRepositoryRows()
    {
        var clientId = Guid.NewGuid();
        var rows = new List<ClientWithholdingReportRowDto>
        {
            new()
            {
                ClientId = clientId,
                ClientName = "Client A",
                PaymentCount = 2,
                TotalWithholding = 15.500m,
                Currency = "TND"
            }
        };

        var paymentRepository = new Mock<IPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetClientWithholdingReportRowsAsync(
                new DateTime(2026, 1, 1),
                new DateTime(2026, 1, 31),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var handler = new GetClientWithholdingsReportQueryHandler(paymentRepository.Object);
        var result = await handler.Handle(
            new GetClientWithholdingsReportQuery(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(clientId, result.Value[0].ClientId);
        Assert.Equal(15.500m, result.Value[0].TotalWithholding);
    }

    [Fact]
    public async Task GetSupplierWithholdingsReportQuery_ShouldReturnRepositoryRows()
    {
        var supplierId = Guid.NewGuid();
        var rows = new List<SupplierWithholdingReportRowDto>
        {
            new()
            {
                SupplierId = supplierId,
                SupplierName = "Fournisseur B",
                InvoiceCount = 1,
                TotalHT = 1000m,
                TotalWithholding = 15m,
                TotalNetPaid = 985m,
                Currency = "TND"
            }
        };

        var invoiceRepository = new Mock<ISupplierInvoiceRepository>();
        invoiceRepository
            .Setup(x => x.GetSupplierWithholdingReportRowsAsync(
                new DateTime(2026, 3, 1),
                new DateTime(2026, 3, 31),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var handler = new GetSupplierWithholdingsReportQueryHandler(invoiceRepository.Object);
        var result = await handler.Handle(
            new GetSupplierWithholdingsReportQuery(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(supplierId, result.Value[0].SupplierId);
        Assert.Equal(15m, result.Value[0].TotalWithholding);
    }
}