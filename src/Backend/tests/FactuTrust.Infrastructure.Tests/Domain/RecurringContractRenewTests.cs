using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Tests.Services.RecurringContracts;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Tests de la méthode domaine <see cref="RecurringContract.Renew"/> (T4/D7) : prolongation
/// d'une durée identique à la durée initiale, calendaire quand la période initiale est un
/// nombre entier de mois inclusifs, sinon span exact en jours.
/// </summary>
public sealed class RecurringContractRenewTests
{
    private static readonly DateTime AsOf = new(2026, 8, 26);

    private static RecurringContract NewActiveContract(
        DateTime start, DateTime? end, int billingDay = 1, BillingFrequency frequency = BillingFrequency.Monthly)
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), frequency, billingDay, start, end).Value;
        contract.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m,
            RecurringContractTestHarness.DefaultProductId);
        Assert.True(contract.Activate().IsSuccess);
        return contract;
    }

    [Fact]
    public void Renew_Active_ExtendsEndDateByInitialDuration()
    {
        // Période initiale 15/01/2026 → 14/07/2026 (6 mois inclusifs, début en cours de mois).
        var contract = NewActiveContract(new DateTime(2026, 1, 15), new DateTime(2026, 7, 14));

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2027, 1, 14), contract.EndDate);
        Assert.Equal(RecurringContractStatus.Active, contract.Status);
    }

    [Fact]
    public void Renew_Active_CalendricalYear_ExtendsToSameInclusiveDate()
    {
        // 01/01/2026 → 31/12/2026 : N = 12 mois → prolongé au 31/12/2027 (et non au 30/12/2027).
        var contract = NewActiveContract(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2027, 12, 31), contract.EndDate);
    }

    [Fact]
    public void Renew_MidMonthCalendricalDuration_ExtendsCalendrically()
    {
        // 15/03/2026 → 14/09/2026 : N = 6 (deltaMois exact, début en cours de mois) → 14/03/2027.
        var contract = NewActiveContract(new DateTime(2026, 3, 15), new DateTime(2026, 9, 14));

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2027, 3, 14), contract.EndDate);
    }

    [Fact]
    public void Renew_NonCalendricalDuration_ExtendsByExactDaySpan()
    {
        // 10/01/2026 → 25/04/2026 : période non calendaire → 105 jours exacts → 08/08/2026.
        var contract = NewActiveContract(new DateTime(2026, 1, 10), new DateTime(2026, 4, 25));

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(105, (new DateTime(2026, 4, 25) - new DateTime(2026, 1, 10)).Days);
        Assert.Equal(new DateTime(2026, 8, 8), contract.EndDate);
    }

    [Fact]
    public void Renew_Expired_ReactivatesAndRecomputesNextBillingDate()
    {
        // Contrat expiré (01/07/2025 → 30/06/2026, 12 mois calendaires), NextBillingDate dépassée.
        var contract = NewActiveContract(new DateTime(2025, 7, 1), new DateTime(2026, 6, 30));
        Assert.Equal(new DateTime(2025, 7, 1), contract.NextBillingDate); // initiale, dans le passé
        contract.MarkExpired();

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(RecurringContractStatus.Active, contract.Status);
        // baseDate = max(30/06/2026, 26/08/2026) = 26/08/2026 → +12 mois.
        Assert.Equal(new DateTime(2027, 8, 26), contract.EndDate);
        // Jour 1 déjà passé en août → premier du mois suivant.
        Assert.Equal(new DateTime(2026, 9, 1), contract.NextBillingDate);
    }

    [Fact]
    public void Renew_Expired_NextBillingDateInFuture_KeepsIt()
    {
        var contract = NewActiveContract(new DateTime(2026, 9, 1), new DateTime(2027, 8, 31));
        Assert.Equal(new DateTime(2026, 9, 1), contract.NextBillingDate);
        contract.MarkExpired();

        var result = contract.Renew(AsOf);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2026, 9, 1), contract.NextBillingDate); // conservée
        Assert.Equal(new DateTime(2028, 8, 31), contract.EndDate);
    }

    [Fact]
    public void Renew_DayOfMonth31_ClampedInFebruary()
    {
        // Expiré, jour de facturation 31, renouvelé en février → NextBillingDate clampée au 28/02.
        // Durée initiale calendaire (D7) : 31/03/2025 + 6 mois − 1 jour = 29/09/2025.
        var contract = NewActiveContract(
            new DateTime(2025, 3, 31), new DateTime(2025, 9, 29), billingDay: 31);
        contract.MarkExpired();

        var result = contract.Renew(new DateTime(2026, 2, 10));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2026, 2, 28), contract.NextBillingDate);
        // baseDate = max(29/09/2025, 10/02/2026) = 10/02/2026 ; durée 6 mois calendaires.
        Assert.Equal(new DateTime(2026, 8, 10), contract.EndDate);
    }

    [Fact]
    public void Renew_Cancelled_Fails()
    {
        var contract = NewActiveContract(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        contract.Cancel();

        var result = contract.Renew(AsOf);

        Assert.True(result.IsFailure);
        Assert.Equal(RecurringContractStatus.Cancelled, contract.Status);
    }

    [Fact]
    public void Renew_Draft_Fails()
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)).Value;

        var result = contract.Renew(AsOf);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Renew_Suspended_Fails()
    {
        var contract = NewActiveContract(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        contract.Suspend();

        var result = contract.Renew(AsOf);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Renew_WithoutEndDate_Fails()
    {
        var contract = NewActiveContract(new DateTime(2026, 1, 1), end: null);

        var result = contract.Renew(AsOf);

        Assert.True(result.IsFailure);
        Assert.Contains("date de fin", result.Error.Description);
    }
}
