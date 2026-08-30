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
    public void AddLine_WithEmptyDescription_Succeeds()
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, new DateTime(2026, 1, 1)).Value;

        var result = contract.AddLine(
            RecurringContractLineType.FixedRecurring, "   ", 1, 50m, 19m);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.Description);
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

    [Fact]
    public void CanBillForPeriod_ActiveWithNextDate_IgnoresHardcodedThreeDayWindow()
    {
        var start = new DateTime(2026, 1, 1);
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, start,
            endDate: new DateTime(2027, 1, 1)).Value;
        contract.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);
        Assert.True(contract.Activate().IsSuccess);

        // NextBillingDate est le 1er du mois de début ; même 7 jours avant, le domaine
        // reste éligible — c'est le scan (BillingWindowDays) qui borne la fenêtre.
        Assert.True(contract.CanBillForPeriod());
    }

    [Fact]
    public void CanBillForPeriod_NextDatePastEndDate_Fails()
    {
        // Jour de facturation le 1er, départ le 20 → première échéance = 1er du mois suivant.
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, new DateTime(2026, 1, 20),
            endDate: new DateTime(2026, 1, 31)).Value;
        contract.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);
        Assert.True(contract.Activate().IsSuccess);
        Assert.True(contract.NextBillingDate!.Value.Date > contract.EndDate!.Value.Date);

        Assert.False(contract.CanBillForPeriod());
    }

    [Fact]
    public void CanBillForPeriod_Draft_Fails()
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, DateTime.UtcNow.Date).Value;
        Assert.False(contract.CanBillForPeriod());
    }
}
