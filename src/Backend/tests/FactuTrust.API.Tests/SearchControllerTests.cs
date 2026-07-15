using FactuTrust.API.Controllers;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Search.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class SearchControllerTests
{
    [Fact]
    public async Task Search_ReturnsOkWithResults()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GlobalSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalSearchResponseDto { Query = "factures", Results = [] });
        var controller = new SearchController(mediator.Object);
        var result = await controller.Search("factures", cancellationToken: CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }
}
