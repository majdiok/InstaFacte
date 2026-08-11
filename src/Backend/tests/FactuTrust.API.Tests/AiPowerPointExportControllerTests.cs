using System.Security.Claims;
using FactuTrust.API.Controllers;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Commands;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class AiPowerPointExportControllerTests
{
    [Fact]
    public async Task Generate_WhenMessageNotFound_ShouldReturn404WithCode()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GeneratePowerPointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<GeneratePowerPointCommandResult>(
                new Error("PowerPoint.MessageNotFound", "Une des réponses sélectionnées n'existe plus.")));

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var storage = new Mock<IExportStorageService>();

        var currentUser = new Mock<ICurrentUser>();

        var controller = new AiPowerPointExportController(
            mediator.Object,
            storage.Object,
            tenantContext.Object,
            currentUser.Object,
            NullLogger<AiPowerPointExportController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "Test"))
            }
        };

        var request = new PowerPointExportRequestDto
        {
            Title = "Synthèse",
            Responses = new[]
            {
                new ResponseSelectionDto
                {
                    ConversationId = Guid.NewGuid(),
                    MessageId = Guid.NewGuid()
                }
            }
        };

        var result = await controller.Generate(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var body = Assert.IsAssignableFrom<object>(notFound.Value);
        var successProp = body!.GetType().GetProperty("Success")?.GetValue(body);
        var messageProp =
            (body.GetType().GetProperty("Message")?.GetValue(body) as string)
            ?? (body.GetType().GetProperty("Error")?.GetValue(body) as string);
        Assert.False((bool)successProp!);
        Assert.Contains("réponses sélectionnées", messageProp ?? string.Empty);
    }
}
