using System.Net;
using System.Text;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class OpenAiChatCompletionsClientTests
{
    [Fact]
    public async Task Stream_OpenRouterDefault_AddsRefererAndOmitsModalSession()
    {
        var handler = new RecordingHandler();
        handler.EnqueueSse("""{"choices":[{"delta":{"content":"Hi"}}]}""");
        var client = CreateSut(handler, lastName: out var lastName);

        var chunks = new List<OllamaChatChunk>();
        await foreach (var chunk in client.StreamChatAsOllamaCompatibleAsync(
                           "https://openrouter.ai/api/v1",
                           "sk-or-test",
                           "anthropic/claude",
                           [new OpenAiChatMessagePayload { Role = "user", Content = "q" }],
                           Array.Empty<OllamaToolDefinition>(),
                           0.2,
                           128))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Hi", string.Concat(chunks.Select(c => c.Message?.Content)));
        Assert.Equal(OpenAiCompatibleCallOptions.OpenAiCompatibleClientName, lastName.Value);
        Assert.True(handler.LastHadReferer);
        Assert.Null(handler.LastSessionId);
        Assert.Contains("\"tools\":[]", handler.LastBody);
        Assert.DoesNotContain("reasoning_effort", handler.LastBody);
    }

    [Fact]
    public async Task Stream_ModalOptions_SetsSessionReasoningOmitsEmptyToolsAndSkipsOpenRouterHeaders()
    {
        var handler = new RecordingHandler();
        handler.EnqueueSse("""{"choices":[{"delta":{"content":[{"type":"text","text":"Kimi"}]}}]}""");
        var client = CreateSut(handler, lastName: out var lastName);
        var options = OpenAiCompatibleCallOptions.ForModal(
            new ModalSettings { ReasoningEffort = "none", ColdStartRetries = 0 },
            "abc123session");

        var chunks = new List<OllamaChatChunk>();
        await foreach (var chunk in client.StreamChatAsOllamaCompatibleAsync(
                           "https://example--ep-kimi-k3-server.us-west.modal.direct/v1",
                           "wk-id.ws-secret",
                           "moonshotai/Kimi-K3",
                           [new OpenAiChatMessagePayload { Role = "user", Content = "q" }],
                           Array.Empty<OllamaToolDefinition>(),
                           0.3,
                           256,
                           options: options))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Kimi", string.Concat(chunks.Select(c => c.Message?.Content)));
        Assert.Equal(OpenAiCompatibleCallOptions.ModalClientName, lastName.Value);
        Assert.False(handler.LastHadReferer);
        Assert.Equal("abc123session", handler.LastSessionId);
        Assert.DoesNotContain("\"tools\"", handler.LastBody);
        Assert.Contains("\"reasoning_effort\":\"none\"", handler.LastBody);
        Assert.DoesNotContain("reasoning_content", string.Concat(chunks.Select(c => c.Message?.Content)));
    }

    [Fact]
    public async Task Stream_IgnoresReasoningContentField()
    {
        var handler = new RecordingHandler();
        handler.EnqueueSse("""{"choices":[{"delta":{"reasoning_content":"secret-cot","content":"ok"}}]}""");
        var client = CreateSut(handler, lastName: out _);

        var chunks = new List<OllamaChatChunk>();
        await foreach (var chunk in client.StreamChatAsOllamaCompatibleAsync(
                           "https://example/v1",
                           "key",
                           "model",
                           [new OpenAiChatMessagePayload { Role = "user", Content = "q" }],
                           Array.Empty<OllamaToolDefinition>(),
                           0,
                           32))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("ok", string.Concat(chunks.Select(c => c.Message?.Content)));
    }

    [Fact]
    public async Task Stream_RetriesServiceUnavailable_WhenConfigured()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("cold")
        });
        handler.EnqueueSse("""{"choices":[{"delta":{"content":"ready"}}]}""");
        var client = CreateSut(handler, lastName: out _);
        var options = new OpenAiCompatibleCallOptions
        {
            AddOpenRouterHeaders = false,
            RetryOnServiceUnavailable = true,
            ColdStartRetries = 2,
            HttpClientName = OpenAiCompatibleCallOptions.ModalClientName
        };

        var text = new StringBuilder();
        await foreach (var chunk in client.StreamChatAsOllamaCompatibleAsync(
                           "https://example/v1",
                           "key",
                           "model",
                           [new OpenAiChatMessagePayload { Role = "user", Content = "q" }],
                           Array.Empty<OllamaToolDefinition>(),
                           0,
                           32,
                           options: options))
        {
            text.Append(chunk.Message?.Content);
        }

        Assert.Equal("ready", text.ToString());
        Assert.Equal(2, handler.SendCount);
    }

    private static OpenAiChatCompletionsClient CreateSut(RecordingHandler handler, out StrongBox<string> lastName)
    {
        lastName = new StrongBox<string>(OpenAiCompatibleCallOptions.OpenAiCompatibleClientName);
        var captured = lastName;
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns((string name) =>
            {
                captured.Value = name;
                return new HttpClient(handler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
            });

        return new OpenAiChatCompletionsClient(
            factory.Object,
            Options.Create(new OpenRouterSettings
            {
                HttpReferer = "https://app.example",
                AppTitle = "InstaFact",
                ModelListCacheSeconds = 30
            }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<OpenAiChatCompletionsClient>.Instance);
    }

    private sealed class StrongBox<T>(T value)
    {
        public T Value { get; set; } = value;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastBody { get; private set; } = "";
        public int SendCount { get; private set; }
        public bool LastHadReferer { get; private set; }
        public string? LastSessionId { get; private set; }

        public void EnqueueSse(string dataLine)
        {
            var body = $"data: {dataLine}\n\ndata: [DONE]\n\n";
            Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            LastRequest = request;
            LastHadReferer = request.Headers.Contains("HTTP-Referer");
            LastSessionId = request.Headers.TryGetValues("Modal-Session-ID", out var values)
                ? values.FirstOrDefault()
                : null;
            LastBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Responses.Dequeue();
        }
    }
}
