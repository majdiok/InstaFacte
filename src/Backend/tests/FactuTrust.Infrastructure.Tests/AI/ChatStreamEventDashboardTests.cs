using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ChatStreamEventDashboardTests
{
    [Fact]
    public void DashboardEvent_populates_dashboard_payload()
    {
        const string payload = """{"title":"Stock","sections":[]}""";

        var evt = ChatStreamEvent.DashboardEvent(payload);

        Assert.Equal("dashboard", evt.Type);
        Assert.Equal(payload, evt.Dashboard);
        Assert.Null(evt.Content);
        Assert.Null(evt.Error);
        Assert.Null(evt.ConversationId);
    }

    [Fact]
    public void ContentReplace_populates_full_body()
    {
        const string body = "Réponse complète synthétisée pour l'utilisateur.";

        var evt = ChatStreamEvent.ContentReplace(body);

        Assert.Equal("content_replace", evt.Type);
        Assert.Equal(body, evt.Content);
    }

    [Fact]
    public void DashboardEvent_does_not_interfere_with_other_factories()
    {
        var content = ChatStreamEvent.ContentChunk("hello");
        var actions = ChatStreamEvent.ClientActionsEvent("[]");

        Assert.Null(content.Dashboard);
        Assert.Null(actions.Dashboard);
    }
}
