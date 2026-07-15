using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ChatStreamEventPhaseTests
{
    [Fact]
    public void PhaseEvent_populates_extended_progress_payload()
    {
        var evt = ChatStreamEvent.PhaseEvent(
            "llm_stream_round",
            "completed",
            elapsedMs: 2450,
            round: 2,
            firstTokenMs: 830,
            hadToolCalls: true,
            detail: "ollama:glm-4.7-flash");

        Assert.Equal("phase", evt.Type);
        Assert.Equal("llm_stream_round", evt.Phase);
        Assert.Equal("completed", evt.PhaseStatus);
        Assert.Equal(2450, evt.ElapsedMs);
        Assert.Equal(2, evt.Round);
        Assert.Equal(830, evt.FirstTokenMs);
        Assert.True(evt.HadToolCalls);
        Assert.Equal("ollama:glm-4.7-flash", evt.Detail);
    }
}
