using FactuTrust.API.Controllers.Projects;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class ProjectsControllerTaskBillingTests
{
    [Fact]
    public async Task BillableTasks_WhenProjectMissing_ReturnsNotFound()
    {
        var projectId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service.Setup(s => s.GetAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectDto?)null);

        var controller = CreateController(service.Object);
        var result = await controller.BillableTasks(projectId, "hourly", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task InvoiceTasks_WhenFailure_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service.Setup(s => s.InvoiceTasksAsync(projectId, It.IsAny<InvoiceTasksDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ProjectInvoiceResultDto>(Error.Validation("Tasks", "Sélectionnez au moins une tâche")));

        var controller = CreateController(service.Object);
        var result = await controller.InvoiceTasks(projectId, new InvoiceTasksDto(), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<Controllers.ApiResponse<ProjectInvoiceResultDto>>(bad.Value);
        Assert.False(body.Success);
    }

    [Fact]
    public async Task LinkedInvoices_WhenProjectMissing_ReturnsNotFound()
    {
        var projectId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service.Setup(s => s.GetLinkedInvoicesAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectLinkedInvoiceDto>?)null);

        var controller = CreateController(service.Object);
        var result = await controller.LinkedInvoices(projectId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task LinkedInvoices_WhenProjectExists_ReturnsOk()
    {
        var projectId = Guid.NewGuid();
        var items = new List<ProjectLinkedInvoiceDto>
        {
            new()
            {
                InvoiceId = Guid.NewGuid(),
                Number = "FAC-2026-00001",
                IssueDate = new DateTime(2026, 2, 1),
                ClientName = "Client",
                AmountHT = 100m,
                AmountVat = 19m,
                AmountTTC = 119m,
                Currency = "TND",
                Status = Domain.Enums.InvoiceStatus.Validated,
                StatusDisplay = "Validée",
                CreatedAt = DateTime.UtcNow
            }
        };
        var service = new Mock<IProjectService>();
        service.Setup(s => s.GetLinkedInvoicesAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var controller = CreateController(service.Object);
        var result = await controller.LinkedInvoices(projectId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<Controllers.ApiResponse<IReadOnlyList<ProjectLinkedInvoiceDto>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Single(body.Data!);
    }

    private static ProjectsController CreateController(IProjectService service)
    {
        var options = Options.Create(new ProjectsOptions { Enabled = true });
        return new ProjectsController(service, options);
    }
}
