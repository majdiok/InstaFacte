using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class CursorToolCallbackServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Unauthorized_WhenTokenMismatch()
    {
        var registry = new CursorToolRunRegistry();
        var service = new CursorToolCallbackService(registry, NullLogger<CursorToolCallbackService>.Instance);
        var runId = Guid.NewGuid();
        registry.Register(CreateContext(runId, "token-a", Mock.Of<IAiToolExecutor>(), Mock.Of<IAiToolExecutorScopeFactory>()));

        var (status, _) = await service.ExecuteAsync(
            runId,
            "token-b",
            new CursorToolCallbackRequest { Name = "list_invoices" },
            CancellationToken.None);

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesScopeFactory_AndEnqueuesSse()
    {
        var registry = new CursorToolRunRegistry();
        var service = new CursorToolCallbackService(registry, NullLogger<CursorToolCallbackService>.Instance);
        var runId = Guid.NewGuid();
        var token = CursorToolRunContext.CreateToken();
        var scope = new Mock<IAiToolExecutorScopeFactory>();
        scope.Setup(x => x.ExecuteAsync(
                "list_invoices",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"ok":true}"""));
        var ctx = CreateContext(runId, token, Mock.Of<IAiToolExecutor>(), scope.Object);
        registry.Register(ctx);

        var (status, _) = await service.ExecuteAsync(
            runId,
            token,
            new CursorToolCallbackRequest
            {
                Name = "list_invoices",
                CallId = "c1",
                Arguments = JsonDocument.Parse("""{"limit":5}""").RootElement
            },
            CancellationToken.None);

        Assert.Equal(200, status);
        Assert.Equal(1, ctx.ToolsExecuted);
        Assert.Equal(2, ctx.ExtraEvents.Count);
        Assert.Contains(ctx.Conversation.Messages, m => m.Role == MessageRole.Tool && m.ToolName == "list_invoices");
        scope.Verify(x => x.ExecuteAsync(
            "list_invoices",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CursorToolRunContext CreateContext(
        Guid runId,
        string token,
        IAiToolExecutor executor,
        IAiToolExecutorScopeFactory scope) =>
        new()
        {
            RunId = runId,
            Token = token,
            Conversation = Conversation.Create(Guid.NewGuid(), "test"),
            CorrelationId = "corr",
            ToolExecutor = executor,
            ScopeFactory = scope,
            TenantId = Guid.NewGuid(),
            ConnectionString = "Server=.;Database=t",
            UserId = Guid.NewGuid(),
            Role = UserRole.Accountant,
            Permissions = new HashSet<string>(StringComparer.Ordinal)
        };
}
