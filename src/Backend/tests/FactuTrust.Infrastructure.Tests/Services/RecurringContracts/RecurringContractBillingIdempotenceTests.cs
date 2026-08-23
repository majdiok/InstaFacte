using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>
/// Verifies billing run idempotence: duplicate period runs must not create a second draft.
/// </summary>
public sealed class RecurringContractBillingIdempotenceTests
{
    [Fact]
    public void BillingRun_SamePeriod_HasSameKey()
    {
        var contractId = Guid.NewGuid();
        var from = new DateTime(2026, 3, 1);
        var to = new DateTime(2026, 3, 31);

        var run1 = RecurringContractBillingRun.CreatePending(contractId, from, to);
        var run2 = RecurringContractBillingRun.CreatePending(contractId, from, to);

        Assert.Equal(run1.RecurringContractId, run2.RecurringContractId);
        Assert.Equal(run1.PeriodFrom, run2.PeriodFrom);
        Assert.Equal(run1.PeriodTo, run2.PeriodTo);
        Assert.Equal(RecurringContractBillingRunStatus.Pending, run1.Status);
    }

    [Fact]
    public void BillingRun_AfterDraftCreated_CannotRebillSamePeriod()
    {
        var run = RecurringContractBillingRun.CreatePending(
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        run.MarkDraftCreated(Guid.NewGuid(), 100m, 0m, 0m);

        Assert.Equal(RecurringContractBillingRunStatus.DraftCreated, run.Status);
        Assert.NotNull(run.InvoiceDraftId);
    }
}
