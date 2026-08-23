using FactuTrust.API.Controllers;
using FactuTrust.Application.Features.SalesReturnNotes.Commands;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class SalesReturnNotesControllerTests
{
    [Fact]
    public async Task Confirm_WhenNotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ConfirmSalesReturnNoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("SalesReturnNote", Guid.NewGuid())));

        var controller = new SalesReturnNotesController(mediator.Object, NullLogger<SalesReturnNotesController>.Instance);
        var result = await controller.ConfirmSalesReturnNote(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Confirm_WhenConflict_Returns409()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ConfirmSalesReturnNoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict("Le bon de livraison a été facturé entre-temps. Rechargez la page.")));

        var controller = new SalesReturnNotesController(mediator.Object, NullLogger<SalesReturnNotesController>.Instance);
        var result = await controller.ConfirmSalesReturnNote(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_WhenConfirmed_Returns400()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<DeleteSalesReturnNoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être supprimés.")));

        var controller = new SalesReturnNotesController(mediator.Object, NullLogger<SalesReturnNotesController>.Instance);
        var result = await controller.DeleteSalesReturnNote(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
