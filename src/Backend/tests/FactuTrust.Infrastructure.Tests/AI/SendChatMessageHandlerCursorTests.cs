using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class SendChatMessageHandlerCursorTests
{
    [Fact]
    public async Task HandleAsync_Cursor_StreamsContent_AndDoesNotCallOpenRouter()
    {
        var cursor = new Mock<ICursorAgentClient>();
        cursor.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cursor.Setup(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Stream(
                new CursorAgentStreamEvent("started", AgentId: "ag-1", RunId: "run-1"),
                new CursorAgentStreamEvent("assistant_text", Text: "Le chiffre d'affaires d'avril 2026 s'élève à 12 500 TND hors taxes pour l'ensemble des factures."),
                new CursorAgentStreamEvent("done", Status: "finished")));

        var openAi = new Mock<IOpenAiChatCompletionsClient>(MockBehavior.Strict);
        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("cursor:composer-2.5");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetCursorCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCursorCredentials(true, "cursor_test_key"));

        var conversations = new Mock<IConversationRepository>();
        conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("a@b.c");
        currentUser.SetupGet(x => x.Role).Returns(UserRole.Accountant);
        currentUser.SetupGet(x => x.IsAccountingFirmDelegatedContext).Returns(false);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(x => x.ConnectionString).Returns("Server=.;Database=t");

        var contextBuilder = new Mock<IAiContextBuilder>();
        contextBuilder.Setup(x => x.BuildSystemPromptAsync(
                It.IsAny<AssistantMode>(),
                It.IsAny<string?>(),
                It.IsAny<AssistantAgentScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tu es l'assistant InstaFact.");

        var toolExecutor = new Mock<IAiToolExecutor>();
        var enricher = new AiScreenAnalysisEnricher(
            toolExecutor.Object,
            Options.Create(new ScreenAnalysisOptions()),
            NullLogger<AiScreenAnalysisEnricher>.Instance);

        var handler = new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            openAi.Object,
            cursor.Object,
            new CursorToolRunRegistry(),
            tenant.Object,
            platform.Object,
            Mock.Of<IOllamaInferenceProfileResolver>(),
            toolExecutor.Object,
            Mock.Of<IAiToolExecutorScopeFactory>(),
            contextBuilder.Object,
            AiVolatileContextFormatter.CreateDefault(),
            enricher,
            conversations.Object,
            currentUser.Object,
            NullLogger<SendChatMessageHandler>.Instance,
            Options.Create(new OllamaSettings { DefaultModel = "mistral", ConversationalFastPathEnabled = false }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true, ToolCallbackBaseUrl = "http://127.0.0.1:7000" }));

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(
            new SendChatMessageCommand(null, "Quel est le chiffre d'affaires d'avril 2026 ?"),
            Guid.NewGuid()))
        {
            events.Add(ev);
        }

        Assert.Contains(events, e => e.Type == "content" && (e.Content ?? "").Contains("12 500"));
        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "error");
        cursor.Verify(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        openAi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_Cursor_MapsDisallowedToolsErrorToUserFriendlyMessage()
    {
        var cursor = new Mock<ICursorAgentClient>();
        cursor.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cursor.Setup(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Stream(new CursorAgentStreamEvent(
                "error",
                Error: "Unknown tool name(s) in disallowedTools: write. Valid tool names: edit")));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("cursor:composer-2.5");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetCursorCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCursorCredentials(true, "cursor_test_key"));

        var conversations = new Mock<IConversationRepository>();
        conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("a@b.c");
        currentUser.SetupGet(x => x.Role).Returns(UserRole.Accountant);
        currentUser.SetupGet(x => x.IsAccountingFirmDelegatedContext).Returns(false);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(x => x.ConnectionString).Returns("Server=.;Database=t");

        var contextBuilder = new Mock<IAiContextBuilder>();
        contextBuilder.Setup(x => x.BuildSystemPromptAsync(
                It.IsAny<AssistantMode>(),
                It.IsAny<string?>(),
                It.IsAny<AssistantAgentScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tu es l'assistant InstaFact.");

        var handler = new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            Mock.Of<IOpenAiChatCompletionsClient>(),
            cursor.Object,
            new CursorToolRunRegistry(),
            tenant.Object,
            platform.Object,
            Mock.Of<IOllamaInferenceProfileResolver>(),
            Mock.Of<IAiToolExecutor>(),
            Mock.Of<IAiToolExecutorScopeFactory>(),
            contextBuilder.Object,
            AiVolatileContextFormatter.CreateDefault(),
            new AiScreenAnalysisEnricher(
                Mock.Of<IAiToolExecutor>(),
                Options.Create(new ScreenAnalysisOptions()),
                NullLogger<AiScreenAnalysisEnricher>.Instance),
            conversations.Object,
            currentUser.Object,
            NullLogger<SendChatMessageHandler>.Instance,
            Options.Create(new OllamaSettings { DefaultModel = "mistral", ConversationalFastPathEnabled = false }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true, ToolCallbackBaseUrl = "http://127.0.0.1:7000" }));

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(
            new SendChatMessageCommand(null, "CA du mois ?"),
            Guid.NewGuid()))
        {
            events.Add(ev);
        }

        var error = events.FirstOrDefault(e => e.Type == "error");
        Assert.NotNull(error);
        Assert.Contains("administrateur", error.Error ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("disallowedTools", error.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_Cursor_ToolCallbackContext_InvokesExecutor()
    {
        var registry = new CursorToolRunRegistry();
        var toolExecutor = new Mock<IAiToolExecutor>();
        toolExecutor.Setup(x => x.ExecuteAsync(
                "list_invoices",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiToolResult.Ok("""{"count":1}"""));

        var cursor = new Mock<ICursorAgentClient>();
        cursor.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cursor.Setup(x => x.RunChatAsync(It.IsAny<CursorChatRunRequest>(), It.IsAny<CancellationToken>()))
            .Returns((CursorChatRunRequest request, CancellationToken _) => SimulateTool(registry, toolExecutor.Object, request));

        var platform = new Mock<IPlatformAiSettingsService>();
        platform.Setup(x => x.GetDefaultModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("cursor:composer-2.5");
        platform.Setup(x => x.GetStudioAiModelRefAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        platform.Setup(x => x.GetCursorCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCursorCredentials(true, "cursor_test_key"));

        var conversations = new Mock<IConversationRepository>();
        conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Role).Returns(UserRole.Accountant);
        currentUser.SetupGet(x => x.IsAccountingFirmDelegatedContext).Returns(false);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(x => x.ConnectionString).Returns("Server=.;Database=t");

        var contextBuilder = new Mock<IAiContextBuilder>();
        contextBuilder.Setup(x => x.BuildSystemPromptAsync(
                It.IsAny<AssistantMode>(),
                It.IsAny<string?>(),
                It.IsAny<AssistantAgentScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tu es l'assistant InstaFact.");

        var enricher = new AiScreenAnalysisEnricher(
            toolExecutor.Object,
            Options.Create(new ScreenAnalysisOptions()),
            NullLogger<AiScreenAnalysisEnricher>.Instance);

        var handler = new SendChatMessageHandler(
            Mock.Of<IOllamaClient>(),
            Mock.Of<IOllamaGenerationGate>(),
            Mock.Of<IOpenAiChatCompletionsClient>(),
            cursor.Object,
            registry,
            tenant.Object,
            platform.Object,
            Mock.Of<IOllamaInferenceProfileResolver>(),
            toolExecutor.Object,
            Mock.Of<IAiToolExecutorScopeFactory>(),
            contextBuilder.Object,
            AiVolatileContextFormatter.CreateDefault(),
            enricher,
            conversations.Object,
            currentUser.Object,
            NullLogger<SendChatMessageHandler>.Instance,
            Options.Create(new OllamaSettings { DefaultModel = "mistral", ConversationalFastPathEnabled = false }),
            Options.Create(new ScreenAnalysisOptions()),
            Options.Create(new CursorSdkSettings { Enabled = true }));

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in handler.HandleAsync(
            new SendChatMessageCommand(null, "Liste les dernières factures du mois d'avril 2026"),
            Guid.NewGuid()))
        {
            events.Add(ev);
        }

        Assert.Contains(events, e => e.Type == "tool_call_start" && e.ToolName == "list_invoices");
        Assert.Contains(events, e => e.Type == "tool_call_end");
        toolExecutor.Verify(x => x.ExecuteAsync(
            "list_invoices",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<AiToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<CursorAgentStreamEvent> Stream(
        params CursorAgentStreamEvent[] events)
    {
        await Task.Yield();
        foreach (var ev in events)
            yield return ev;
    }

    private static async IAsyncEnumerable<CursorAgentStreamEvent> SimulateTool(
        ICursorToolRunRegistry registry,
        IAiToolExecutor executor,
        CursorChatRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (registry.TryGet(request.RunId, out var ctx))
        {
            ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallStart("list_invoices", "c1"));
            var result = await executor.ExecuteAsync(
                "list_invoices",
                new Dictionary<string, object?>(),
                new AiToolExecutionContext(ctx.CorrelationId, ctx.Conversation.Id),
                cancellationToken);
            ctx.ToolsExecuted++;
            ctx.ToolSources.Add(("list_invoices", "c1"));
            ctx.ExtraEvents.Enqueue(ChatStreamEvent.ToolCallEnd("list_invoices", "c1", 1));
            _ = result;
        }

        yield return new CursorAgentStreamEvent(
            "assistant_text",
            Text: "Voici les factures d'avril 2026 : une facture a été trouvée pour un total de 1 190 TND.");
        yield return new CursorAgentStreamEvent("done", Status: "finished");
    }
}
