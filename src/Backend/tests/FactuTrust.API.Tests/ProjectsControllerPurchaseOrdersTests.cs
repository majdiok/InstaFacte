using FactuTrust.API.Controllers.Projects;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

// ProjectsController lives in FactuTrust.API.Controllers.Projects, so unqualified ApiResponse<T>
// resolves to FactuTrust.API.Controllers.ApiResponse<T> (defined in ApiResponse.cs) — not
// FactuTrust.Application.DTOs.ApiResponse<T>. Assert against the real runtime type.
public sealed class ProjectsControllerPurchaseOrdersTests
{
    [Fact]
    public async Task ListPurchaseOrders_WhenEmpty_ReturnsOkEmptyList()
    {
        var projectId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service
            .Setup(s => s.ListPurchaseOrdersAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProjectPurchaseOrderDto>());

        var controller = CreateController(service.Object);

        var result = await controller.ListPurchaseOrders(projectId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<Controllers.ApiResponse<IReadOnlyList<ProjectPurchaseOrderDto>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Empty(body.Data!);
    }

    [Fact]
    public async Task ListPurchaseOrders_WhenLinked_ReturnsMappedItems()
    {
        var projectId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service
            .Setup(s => s.ListPurchaseOrdersAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProjectPurchaseOrderDto
                {
                    Id = poId,
                    Number = "BC-2026-0001",
                    SupplierName = "Acme",
                    OrderDate = new DateTime(2026, 9, 1),
                    Status = PurchaseOrderStatus.Confirmed,
                    StatusDisplay = "Confirmé",
                    StatusCss = "confirmed",
                    TotalHt = 1500.500m
                }
            });

        var controller = CreateController(service.Object);

        var result = await controller.ListPurchaseOrders(projectId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<Controllers.ApiResponse<IReadOnlyList<ProjectPurchaseOrderDto>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Single(body.Data!);
        Assert.Equal(poId, body.Data![0].Id);
        Assert.Equal("BC-2026-0001", body.Data[0].Number);
        Assert.Equal("Acme", body.Data[0].SupplierName);
        Assert.Equal(1500.500m, body.Data[0].TotalHt);
    }

    [Fact]
    public async Task AssignPo_WhenSuccess_ReturnsOk()
    {
        var projectId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var service = new Mock<IProjectService>();
        service
            .Setup(s => s.AssignPurchaseOrderAsync(
                projectId,
                It.Is<AssignPurchaseOrderDto>(d => d.PurchaseOrderId == poId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(service.Object);

        var result = await controller.AssignPo(
            projectId,
            new AssignPurchaseOrderDto { PurchaseOrderId = poId },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<Controllers.ApiResponse<bool>>(ok.Value);
        Assert.True(body.Success);
        Assert.True(body.Data);
    }

    [Fact]
    public async Task AssignPo_ThenList_ShowsAssignedPurchaseOrder()
    {
        var projectId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var linked = new List<ProjectPurchaseOrderDto>();
        var service = new Mock<IProjectService>();

        service
            .Setup(s => s.AssignPurchaseOrderAsync(
                projectId,
                It.Is<AssignPurchaseOrderDto>(d => d.PurchaseOrderId == poId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                linked.Add(new ProjectPurchaseOrderDto
                {
                    Id = poId,
                    Number = "BC-2026-0042",
                    SupplierName = "Fournisseur",
                    OrderDate = DateTime.UtcNow.Date,
                    Status = PurchaseOrderStatus.Draft,
                    StatusDisplay = "Brouillon",
                    StatusCss = "draft",
                    TotalHt = 80m
                });
                return Result.Success();
            });

        service
            .Setup(s => s.ListPurchaseOrdersAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => linked.ToArray());

        var controller = CreateController(service.Object);

        await controller.AssignPo(
            projectId,
            new AssignPurchaseOrderDto { PurchaseOrderId = poId },
            CancellationToken.None);

        var listResult = await controller.ListPurchaseOrders(projectId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(listResult.Result);
        var body = Assert.IsType<Controllers.ApiResponse<IReadOnlyList<ProjectPurchaseOrderDto>>>(ok.Value);
        Assert.Contains(body.Data!, p => p.Id == poId && p.Number == "BC-2026-0042");
    }

    private static ProjectsController CreateController(IProjectService service)
    {
        var options = Options.Create(new ProjectsOptions { Enabled = true });
        return new ProjectsController(service, options);
    }
}
