using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Balance âgée commerciale (lot 6). Le point sensible : sur quelle date, pour quelle
/// facture, et quelle tranche. Chaque cas mérite un test.
/// </summary>
public sealed class AgingBucketsTests
{
    private static readonly DateTime AsOf = new(2026, 07, 31);
    private static readonly Guid ClientId = Guid.NewGuid();
    private static int _seq = 1;

    [Fact]
    public void NoInvoices_AllBucketsAreZero()
    {
        var buckets = AgingBuckets.Compute(Array.Empty<Invoice>(), new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(0m, buckets.NotDue);
        Assert.Equal(0m, buckets.B0To30);
        Assert.Equal(0m, buckets.B31To60);
        Assert.Equal(0m, buckets.B61To90);
        Assert.Equal(0m, buckets.BOver90);
    }

    [Fact]
    public void AFullyPaidInvoice_DropsOutOfEveryBucket()
    {
        // Une facture soldée ne pèse plus : la balance âgée serait sinon gonflée par
        // l'historique des ventes.
        var invoice = NewInvoice(1000m, dueDaysAgo: 45);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 1000m };

        var buckets = AgingBuckets.Compute(new[] { invoice }, paid, AsOf);

        Assert.Equal(0m, buckets.B31To60);
    }

    [Fact]
    public void ACreditNote_IsNeverAReceivable()
    {
        var creditNote = NewInvoice(200m, dueDaysAgo: 45, type: InvoiceType.CreditNote);

        var buckets = AgingBuckets.Compute(new[] { creditNote }, new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(0m, buckets.B31To60);
    }

    [Fact]
    public void AnInvoiceWithoutDueDate_FallsIntoNotDue()
    {
        // Sans date d'echeance, on ne peut pas la declarer exigible.
        var invoice = NewInvoice(500m, dueDaysAgo: null);

        var buckets = AgingBuckets.Compute(new[] { invoice }, new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(500m, buckets.NotDue);
        Assert.Equal(0m, buckets.B0To30);
    }

    [Fact]
    public void AnInvoiceDueToday_IsNotYetOverdue()
    {
        // Le jour meme n'est pas encore un retard.
        var invoice = NewInvoice(500m, dueDaysAgo: 0);

        var buckets = AgingBuckets.Compute(new[] { invoice }, new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(500m, buckets.NotDue);
        Assert.Equal(0m, buckets.B0To30);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(30)]
    public void OverdueBetween1And30Days_LandsInFirstBucket(int daysAgo)
    {
        var invoice = NewInvoice(400m, dueDaysAgo: daysAgo);

        var buckets = AgingBuckets.Compute(new[] { invoice }, new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(400m, buckets.B0To30);
    }

    [Fact]
    public void OverdueBoundaries_ClassifyOnTheHigherSideOfThirty()
    {
        // 30 j = encore dans 0-30, 31 j = deja 31-60.
        var thirty = NewInvoice(100m, dueDaysAgo: 30);
        var thirtyOne = NewInvoice(100m, dueDaysAgo: 31);

        var buckets = AgingBuckets.Compute(new[] { thirty, thirtyOne },
            new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(100m, buckets.B0To30);
        Assert.Equal(100m, buckets.B31To60);
    }

    [Theory]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(90, "61-90")]
    [InlineData(91, ">90")]
    [InlineData(365, ">90")]
    public void BoundariesBetweenBuckets(int daysAgo, string expected)
    {
        var invoice = NewInvoice(100m, dueDaysAgo: daysAgo);

        var buckets = AgingBuckets.Compute(new[] { invoice }, new Dictionary<Guid, decimal>(), AsOf);

        Assert.Equal(expected == "31-60" ? 100m : 0m, buckets.B31To60);
        Assert.Equal(expected == "61-90" ? 100m : 0m, buckets.B61To90);
        Assert.Equal(expected == ">90" ? 100m : 0m, buckets.BOver90);
    }

    [Fact]
    public void PartialPayment_ReducesTheBucketBySameAmount()
    {
        var invoice = NewInvoice(1000m, dueDaysAgo: 45);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 300m };

        var buckets = AgingBuckets.Compute(new[] { invoice }, paid, AsOf);

        Assert.Equal(700m, buckets.B31To60);
    }

    /// <summary>
    /// Un trop-perçu ne vient PAS en déduction des autres factures : cela relève du lettrage.
    /// Le compter minorerait la balance âgée à tort.
    /// </summary>
    [Fact]
    public void AnOverpaidInvoice_DoesNotReduceOtherBuckets()
    {
        var overpaid = NewInvoice(100m, dueDaysAgo: 45);
        var open = NewInvoice(500m, dueDaysAgo: 45);
        var paid = new Dictionary<Guid, decimal> { [overpaid.Id] = 300m };

        var buckets = AgingBuckets.Compute(new[] { overpaid, open }, paid, AsOf);

        Assert.Equal(500m, buckets.B31To60);
    }

    [Fact]
    public void SeveralInvoicesInDifferentBuckets_SumIndependently()
    {
        var notDue = NewInvoice(100m, dueDaysAgo: -10);
        var recent = NewInvoice(200m, dueDaysAgo: 20);
        var midOld = NewInvoice(300m, dueDaysAgo: 50);
        var older = NewInvoice(400m, dueDaysAgo: 75);
        var ancient = NewInvoice(500m, dueDaysAgo: 200);

        var buckets = AgingBuckets.Compute(
            new[] { notDue, recent, midOld, older, ancient },
            new Dictionary<Guid, decimal>(),
            AsOf);

        Assert.Equal(100m, buckets.NotDue);
        Assert.Equal(200m, buckets.B0To30);
        Assert.Equal(300m, buckets.B31To60);
        Assert.Equal(400m, buckets.B61To90);
        Assert.Equal(500m, buckets.BOver90);
    }

    // ─────────────────────────── Montage ───────────────────────────

    private static Invoice NewInvoice(
        decimal amountHt,
        int? dueDaysAgo,
        InvoiceType type = InvoiceType.Standard)
    {
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";

        // Si l'échéance est passée, on doit reculer la date d'émission avec elle : le domaine
        // refuse DueDate < IssueDate.
        DateTime? dueDate = dueDaysAgo.HasValue ? AsOf.AddDays(-dueDaysAgo.Value) : null;
        var issueDate = dueDate.HasValue && dueDate.Value < AsOf
            ? dueDate.Value.AddDays(-30)
            : AsOf;

        var invoice = Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, _seq++),
            NewClient(),
            issueDate,
            dueDate,
            type: type).Value;

        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(amountHt), VatRate.Exempt).IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);

        return invoice;
    }

    private static Client NewClient() =>
        Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;
}
