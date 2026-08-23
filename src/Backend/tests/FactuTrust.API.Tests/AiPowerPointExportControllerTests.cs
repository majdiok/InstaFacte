using System.Security.Claims;
using FactuTrust.API.Controllers;
using FactuTrust.Application.Common.Interfaces;
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

        var controller = CreateController(mediator.Object, tenantId, userId);

        var result = await controller.Generate(ValidRequest(), CancellationToken.None);

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

    [Fact]
    public async Task Generate_WhenFirmDelegatedWouldHaveBeenTrue_StillInvokesMediator()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var exportId = Guid.NewGuid();

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GeneratePowerPointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new GeneratePowerPointCommandResult(
                Content: [0x50, 0x4B],
                FileName: "synthese.pptx",
                SlideCount: 1,
                SizeBytes: 2,
                Duration: TimeSpan.FromMilliseconds(10),
                ExportId: exportId,
                DownloadUrl: $"/api/ai/exports/powerpoint/{exportId:D}?token=t",
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                GeneratedAt: DateTime.UtcNow)));

        var controller = CreateController(mediator.Object, tenantId, userId);

        var result = await controller.Generate(ValidRequest(), CancellationToken.None);

        mediator.Verify(
            m => m.Send(It.IsAny<GeneratePowerPointCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(result is ObjectResult { StatusCode: StatusCodes.Status403Forbidden });
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("synthese.pptx", file.FileDownloadName);
    }

    private static AiPowerPointExportController CreateController(
        IMediator mediator,
        Guid tenantId,
        Guid userId)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var controller = new AiPowerPointExportController(
            mediator,
            Mock.Of<IExportStorageService>(),
            tenantContext.Object,
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

        return controller;
    }

    private static PowerPointExportRequestDto ValidRequest() => new()
    {
        Title = "Synthèse",
        Responses =
        [
            new ResponseSelectionDto
            {
                ConversationId = Guid.NewGuid(),
                MessageId = Guid.NewGuid()
            }
        ]
    };
}
