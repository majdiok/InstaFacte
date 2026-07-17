using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.Templates;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class PdfServicePayslipTests
{
    [Fact]
    public async Task GeneratePayslipPdfAsync_ReturnsNonEmptyBytes()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var registry = new DocumentTemplateRegistry(new IDocumentTemplate[] { new StandardDocumentTemplate() });
        var service = new PdfService(httpFactory.Object, registry);

        var dto = new PayslipDetailDto
        {
            Id = Guid.NewGuid(),
            PayrollRunId = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            EmployeeName = "Mohamed Ben Ali",
            EmployeeNumber = "EMP-001",
            Year = 2026,
            Month = 7,
            GrossSalary = 2000m,
            NetSalary = 1541.551m,
            Lines =
            [
                new PayslipLineDto { Order = 1, Label = "Salaire de base", Kind = "Earning", Amount = 2000m },
                new PayslipLineDto { Order = 2, Label = "CNSS salarié", Kind = "Deduction", Amount = 183.6m }
            ]
        };

        var bytes = await service.GeneratePayslipPdfAsync(dto, "Société Test", CancellationToken.None);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.Equal(0x25, bytes[0]); // PDF magic '%'
        Assert.True(bytes.Length > 500, "Le PDF fiche de paie devrait contenir une mise en page complète.");
    }
}
