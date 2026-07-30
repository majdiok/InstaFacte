using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class ExchangeDomainTests
{
    [Fact]
    public void Thread_Close_and_Reopen_succeed()
    {
        var thread = ExchangeThread.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.True(thread.Close(Guid.NewGuid()).IsSuccess);
        Assert.Equal(ExchangeThreadStatus.Closed, thread.Status);
        Assert.True(thread.Reopen().IsSuccess);
        Assert.Equal(ExchangeThreadStatus.Open, thread.Status);
        Assert.Null(thread.ClosedAt);
    }

    [Fact]
    public void Message_rejects_empty_body()
    {
        var result = ExchangeMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "  ", ExchangeMessageVisibility.ClientVisible);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Request_status_transitions()
    {
        var req = ExchangeRequest.Create(Guid.NewGuid(), 1, "Réclamation TVA", "desc",
            ExchangeRequestCategory.Reclamation, ExchangeRequestPriority.High, Guid.NewGuid(), Guid.NewGuid()).Value;

        Assert.True(req.ChangeStatus(ExchangeRequestStatus.InProgress).IsSuccess);
        Assert.True(req.ChangeStatus(ExchangeRequestStatus.Resolved).IsSuccess);
        Assert.NotNull(req.ResolvedAt);
        Assert.True(req.ChangeStatus(ExchangeRequestStatus.Closed).IsSuccess);
        Assert.True(req.ChangeStatus(ExchangeRequestStatus.Open).IsFailure);
    }

    [Fact]
    public void Task_complete_sets_CompletedAt()
    {
        var task = ExchangeTask.Create(Guid.NewGuid(), "Préparer bilan", null, DateTime.UtcNow.Date,
            null, null, Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.True(task.ChangeStatus(ExchangeTaskStatus.Done).IsSuccess);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public void Document_rejects_zero_size()
    {
        var result = ExchangeDocument.Create(Guid.NewGuid(), "a.pdf", "path", "application/pdf", 0,
            Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result.IsFailure);
    }
}
