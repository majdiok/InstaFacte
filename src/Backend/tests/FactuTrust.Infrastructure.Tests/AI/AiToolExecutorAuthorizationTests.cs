using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolExecutorAuthorizationTests
{
    private static AiToolExecutor CreateExecutor(
        OllamaSettings ollama,
        ICurrentUser currentUser)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        return new AiToolExecutor(
            mediator.Object,
            NullLogger<AiToolExecutor>.Instance,
            TimeProvider.System,
            currentUser,
            Options.Create(ollama));
    }

    [Fact]
    public async Task Create_product_blocked_when_mutation_tools_disabled()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.HasPermission(Permissions.Products.Create)).Returns(true);

        var executor = CreateExecutor(new OllamaSettings { EnableMutationTools = false }, user.Object);
        var result = await executor.ExecuteAsync(
            "create_product",
            new Dictionary<string, object?> { ["code"] = "X", ["name"] = "Y", ["unit_price"] = 1m, ["vat_rate_percent"] = 19 },
            new AiToolExecutionContext("trace", Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Equal(AiToolExecutor.MutationToolsDisabledMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task Create_product_blocked_without_permission()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.HasPermission(Permissions.Products.Create)).Returns(false);

        var executor = CreateExecutor(new OllamaSettings { EnableMutationTools = true }, user.Object);
        var result = await executor.ExecuteAsync(
            "create_product",
            new Dictionary<string, object?> { ["code"] = "X", ["name"] = "Y", ["unit_price"] = 1m, ["vat_rate_percent"] = 19 },
            AiToolExecutionContext.Empty);

        Assert.False(result.Success);
        Assert.Equal(AiToolExecutor.PermissionDeniedMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task Get_product_by_id_blocked_without_read_permission()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.HasPermission(Permissions.Products.Read)).Returns(false);

        var executor = CreateExecutor(new OllamaSettings { EnableMutationTools = false }, user.Object);
        var result = await executor.ExecuteAsync(
            "get_product_by_id",
            new Dictionary<string, object?> { ["product_id"] = Guid.NewGuid().ToString() },
            AiToolExecutionContext.Empty);

        Assert.False(result.Success);
        Assert.Equal(AiToolExecutor.PermissionDeniedMessage, result.ErrorMessage);
    }
}
