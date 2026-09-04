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

    private static ProjectsController CreateController(IProjectService service)
    {
        var options = Options.Create(new ProjectsOptions { Enabled = true });
        return new ProjectsController(service, options);
    }
}
