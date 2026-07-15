using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Clients.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolExecutorNewToolsTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<AiToolExecutor>> _loggerMock;
    private readonly AiToolExecutor _executor;

    public AiToolExecutorNewToolsTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<AiToolExecutor>>();

        // Simulate an authenticated user
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(x => x.Email).Returns("test@example.com");
        _currentUserMock.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var ollamaSettings = Options.Create(new OllamaSettings
        {
            EnableMutationTools = true
        });

        _executor = new AiToolExecutor(
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            _currentUserMock.Object,
            ollamaSettings);
    }

    [Fact]
    public async Task HandleCreateClient_ParsesArguments_And_DispatchesCommand()
    {
        // Arrange
        var expectedClientId = Guid.NewGuid();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(expectedClientId));

        var args = new Dictionary<string, object?>
        {
            { "name", "John Doe" },
            { "email", "john.doe@example.com" },
            { "type", "Individual" },
            { "street", "123 Main St" },
            { "city", "Tunis" },
            { "governorate", "Tunis" }
        };

        var context = AiToolExecutionContext.Empty;

        // Act
        var result = await _executor.ExecuteAsync("create_client", args, context, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"L'outil a retourné une erreur : {result.ErrorMessage}");
        Assert.Contains(expectedClientId.ToString(), result.Data);

        _mediatorMock.Verify(m => m.Send(It.Is<CreateClientCommand>(c => 
            c.Dto.Name == "John Doe" && 
            c.Dto.Email == "john.doe@example.com" &&
            c.Dto.City == "Tunis"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
