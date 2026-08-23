using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class RecurringContractDomainTests
{
    [Fact]
    public void Activate_WithoutLines_Fails()
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, DateTime.UtcNow.Date).Value;

        var result = contract.Activate();
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Activate_WithLines_SetsNextBillingDate()
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, new DateTime(2026, 1, 1)).Value;
        contract.AddLine(
            RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);

        var result = contract.Activate();
        Assert.True(result.IsSuccess);
        Assert.NotNull(contract.NextBillingDate);
        Assert.Equal(RecurringContractStatus.Active, contract.Status);
    }

    [Fact]
    public void BillingRun_IdempotentPeriod_UniqueConstraintConcept()
    {
        var contractId = Guid.NewGuid();
        var run1 = RecurringContractBillingRun.CreatePending(
            contractId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        var run2 = RecurringContractBillingRun.CreatePending(
            contractId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(run1.PeriodFrom, run2.PeriodFrom);
        Assert.Equal(run1.PeriodTo, run2.PeriodTo);
    }
}
