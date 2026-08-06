using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.Templates;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class PdfServiceWithholdingCertificateTests
{
    [Fact]
    public async Task GeneratePayrollWithholdingCertificatePdfAsync_ReturnsNonEmptyBytes()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var registry = new DocumentTemplateRegistry(new IDocumentTemplate[] { new StandardDocumentTemplate() });
        var service = new PdfService(httpFactory.Object, registry);

        var batch = new PayrollWithholdingCertificateBatchDto
        {
            Year = 2026,
            EmployerCompanyName = "Société Test",
            EmployerNif = "1234567A",
            EmployerAddressLine = "Tunis"
        };

        var line = new PayrollWithholdingCertificateLineDto
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = "EMP-001",
            EmployeeName = "Mohamed Ben Ali",
            Cin = "12345678",
            CnssNumber = "1234567890",
            MonthsCount = 12,
            TotalGross = 24000m,
            AnnualNetTaxable = 19796.796m,
            TotalIrppWithheld = 3199.200m,
            TotalCssWithheld = 98.984m,
            TotalWithholding = 3298.184m,
            DocumentReference = "CRS-EMP-001",
            Months =
            [
                new PayrollWithholdingCertificateMonthDto
                {
                    Month = 1,
                    MonthLabel = "Janvier",
                    MonthlyNetTaxable = 1649.733m,
                    Irpp = 266.600m,
                    Css = 8.249m,
                    HasPayslip = true
                }
            ]
        };

        var bytes = await service.GeneratePayrollWithholdingCertificatePdfAsync(line, batch, CancellationToken.None);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]);
        Assert.True(bytes.Length > 500);
    }
}
