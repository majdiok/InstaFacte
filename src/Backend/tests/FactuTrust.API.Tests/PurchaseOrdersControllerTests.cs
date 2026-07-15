using FactuTrust.API.Controllers;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class PurchaseOrdersControllerTests
{
    [Fact]
    public async Task ConfirmPurchaseOrder_WhenNotFound_ShouldReturn404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ConfirmPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("PurchaseOrder", Guid.NewGuid())));

        var controller = new PurchaseOrdersController(mediator.Object, NullLogger<PurchaseOrdersController>.Instance);

        var result = await controller.ConfirmPurchaseOrder(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ConfirmPurchaseOrder_WhenConflict_ShouldReturn409()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ConfirmPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict("Conflit de confirmation")));

        var controller = new PurchaseOrdersController(mediator.Object, NullLogger<PurchaseOrdersController>.Instance);

        var result = await controller.ConfirmPurchaseOrder(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
