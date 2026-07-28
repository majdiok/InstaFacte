using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1 — commande client : l'engagement contractuel qui manquait au cycle de vente.
///
/// Ces tests verrouillent ce que ni le devis, ni le bon de livraison, ni la facture ne savaient
/// porter : le reste à livrer ET le reste à facturer dans la durée, et une machine à états qui
/// ne peut pas déclarer une commande soldée tant qu'un reliquat subsiste.
/// </summary>
public sealed class SalesOrderTests
{
    // ───────────────────────────── Reliquats ─────────────────────────────

    [Fact]
    public void PartialDelivery_LeavesABacklog_AndKeepsTheOrderOpen()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 6m) }).IsSuccess);

        Assert.Equal(6m, line.DeliveredQuantity);
        Assert.Equal(4m, line.PendingDeliveryQuantity);
        Assert.Equal(SalesOrderStatus.PartiallyDelivered, order.Status);
        Assert.True(order.Status.IsOpen());
    }

    [Fact]
    public void SuccessiveDeliveries_ConsumeTheBacklogUntilItCloses()
    {
        // C'est précisément ce que le bon de livraison seul ne savait pas faire : rattacher
        // une seconde livraison au même engagement.
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 6m) }).IsSuccess);
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 4m) }).IsSuccess);

        Assert.Equal(10m, line.DeliveredQuantity);
        Assert.Equal(0m, line.PendingDeliveryQuantity);
        Assert.Equal(SalesOrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void DeliveringMoreThanOrdered_IsRejected()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 8m) }).IsSuccess);
        var excess = order.RecordDeliveries(new[] { (line.Id, 3m) });

        Assert.True(excess.IsFailure);
        Assert.Equal(8m, line.DeliveredQuantity); // inchangé
    }

    [Fact]
    public void DeliveredAndInvoicedAdvanceIndependently()
    {
        // Facturation périodique : on livre trois fois, on facture une fois en fin de mois.
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 4m) }).IsSuccess);
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 3m) }).IsSuccess);

        Assert.Equal(7m, line.DeliveredQuantity);
        Assert.Equal(0m, line.InvoicedQuantity);
        Assert.Equal(7m, line.DeliveredNotInvoicedQuantity); // assiette de la facture périodique

        Assert.True(order.RecordInvoiced(new[] { (line.Id, 7m) }).IsSuccess);
        Assert.Equal(0m, line.DeliveredNotInvoicedQuantity);
        Assert.Equal(3m, line.PendingInvoiceQuantity);
    }

    // ───────────────────────── Machine à états ─────────────────────────

    [Fact]
    public void OrderIsCompleted_OnlyWhenBothDeliveredAndInvoiced()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.Equal(SalesOrderStatus.Delivered, order.Status); // livré mais pas facturé

        Assert.True(order.RecordInvoiced(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.Equal(SalesOrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void FullyInvoicedButPartiallyDelivered_IsNeverCompleted()
    {
        // Le point clé : facturer d'avance ne doit jamais faire disparaître un reste à livrer.
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.True(order.RecordInvoiced(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 6m) }).IsSuccess);

        Assert.True(line.IsFullyInvoiced);
        Assert.False(line.IsFullyDelivered);
        Assert.Equal(SalesOrderStatus.PartiallyDelivered, order.Status);
        Assert.NotEqual(SalesOrderStatus.Completed, order.Status);
        Assert.Equal(4m, order.TotalPendingDeliveryQuantity);
    }

    [Fact]
    public void MultiLineOrder_IsCompletedOnlyWhenEveryLineIs()
    {
        var order = DraftOrder();
        Assert.True(order.AddLine(NewProduct(100m), 10m).IsSuccess);
        Assert.True(order.AddLine(NewProduct(50m, code: "P-2"), 5m).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);

        var l1 = order.Lines.First();
        var l2 = order.Lines.Last();

        Assert.True(order.RecordDeliveries(new[] { (l1.Id, 10m), (l2.Id, 5m) }).IsSuccess);
        Assert.True(order.RecordInvoiced(new[] { (l1.Id, 10m) }).IsSuccess);

        Assert.Equal(SalesOrderStatus.Delivered, order.Status); // l2 pas encore facturée

        Assert.True(order.RecordInvoiced(new[] { (l2.Id, 5m) }).IsSuccess);
        Assert.Equal(SalesOrderStatus.Completed, order.Status);
    }

    [Fact]
    public void DraftCannotReceiveDeliveries()
    {
        var order = DraftOrder();
        Assert.True(order.AddLine(NewProduct(100m), 10m).IsSuccess);
        var line = order.Lines.Single();

        var result = order.RecordDeliveries(new[] { (line.Id, 1m) });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ConfirmedOrderIsNoLongerEditable()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);

        Assert.True(order.AddLine(NewProduct(50m, "P-3"), 1m).IsFailure);
        Assert.True(order.RemoveLine(order.Lines.First().Id).IsFailure);
        Assert.Single(order.Lines);
    }

    [Fact]
    public void EmptyOrderCannotBeConfirmed()
    {
        var order = DraftOrder();

        Assert.True(order.Confirm().IsFailure);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
    }

    // ───────────────────── Annulation et clôture ─────────────────────

    [Fact]
    public void StartedOrder_CannotBeCancelled_ButCanBeClosed()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 6m) }).IsSuccess);

        // Annuler effacerait une livraison réelle : refusé.
        Assert.True(order.Cancel("changement d'avis").IsFailure);

        // La clôture assume l'abandon du reliquat.
        Assert.True(order.Close("reliquat abandonné par le client").IsSuccess);
        Assert.Equal(SalesOrderStatus.Closed, order.Status);
        Assert.True(order.Status.IsFinalized());
        Assert.Equal(6m, line.DeliveredQuantity); // le livré reste acquis
    }

    [Fact]
    public void UntouchedOrder_CanBeCancelled()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);

        Assert.True(order.Cancel("commande annulée par le client").IsSuccess);
        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
        Assert.False(order.Status.IsOpen());
    }

    [Fact]
    public void ClosedOrder_StopsAdvancing()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.True(order.Close("solde commercial").IsSuccess);

        Assert.True(order.RecordInvoiced(new[] { (line.Id, 10m) }).IsFailure);
        Assert.Equal(SalesOrderStatus.Closed, order.Status);
    }

    // ───────────────────── Reprise sur annulation aval ─────────────────────

    [Fact]
    public void ReversingADelivery_ReopensTheBacklog()
    {
        // Bon de livraison annulé : le reliquat doit se rouvrir, pas disparaître.
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.Equal(SalesOrderStatus.Delivered, order.Status);

        Assert.True(order.ReverseDeliveries(new[] { (line.Id, 4m) }).IsSuccess);

        Assert.Equal(6m, line.DeliveredQuantity);
        Assert.Equal(4m, line.PendingDeliveryQuantity);
        Assert.Equal(SalesOrderStatus.PartiallyDelivered, order.Status);
    }

    [Fact]
    public void ReversingAnInvoice_ReopensTheAmountToInvoice_AndUncompletesTheOrder()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.True(order.RecordInvoiced(new[] { (line.Id, 10m) }).IsSuccess);
        Assert.Equal(SalesOrderStatus.Completed, order.Status);

        Assert.True(order.ReverseInvoiced(new[] { (line.Id, 10m) }).IsSuccess);

        Assert.Equal(SalesOrderStatus.Delivered, order.Status);
        Assert.Null(order.CompletedAt);
        Assert.Equal(10m, line.PendingInvoiceQuantity);
    }

    [Fact]
    public void ReversingMoreThanRecorded_IsRejected()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 3m) }).IsSuccess);

        Assert.True(order.ReverseDeliveries(new[] { (line.Id, 5m) }).IsFailure);
        Assert.Equal(3m, line.DeliveredQuantity);
    }

    // ───────────────────── Totaux et carnet de commandes ─────────────────────

    [Fact]
    public void Totals_MatchTheInvoiceEngine_ToTheMillime()
    {
        // Devis → Commande → BL → Facture doivent afficher le même montant.
        const decimal unitHt = 33.333m;
        const decimal quantity = 7m;
        const decimal discount = 12.5m;
        var product = NewProduct(unitHt, fodecApplicable: true);

        var order = DraftOrder();
        Assert.True(order.AddLine(product, quantity, Money.Create(unitHt), discount).IsSuccess);
        Assert.True(order.SetFiscalStampAmount(Money.Create(1m)).IsSuccess);

        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 1), NewClient(), new DateTime(2026, 7, 20)).Value;
        Assert.True(invoice.AddLine(product, quantity, Money.Create(unitHt), discount).IsSuccess);
        Assert.True(invoice.SetFiscalStampAmount(Money.Create(1m)).IsSuccess);

        Assert.Equal(invoice.SubTotal.Amount, order.SubTotal.Amount);
        Assert.Equal(invoice.FodecAmount.Amount, order.FodecAmount.Amount);
        Assert.Equal(invoice.TotalVat.Amount, order.TotalVat.Amount);
        Assert.Equal(invoice.TotalAmount.Amount, order.TotalAmount.Amount);
    }

    [Fact]
    public void BacklogAmount_ReflectsWhatRemainsToDeliver()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);
        var line = order.Lines.Single();

        Assert.Equal(1000m, order.BacklogAmountHt);

        Assert.True(order.RecordDeliveries(new[] { (line.Id, 6m) }).IsSuccess);

        Assert.Equal(400m, order.BacklogAmountHt);
    }

    [Fact]
    public void StockReservation_IsHeldWhileTheOrderIsLive()
    {
        var order = ConfirmedOrder(quantity: 10m, unitHt: 100m);

        Assert.True(order.Status.HoldsStockReservation());
        Assert.True(order.MarkStockReserved().IsSuccess);
        Assert.True(order.IsStockReserved);

        order.MarkStockReleased();
        Assert.False(order.IsStockReserved);
    }

    [Fact]
    public void StockReservation_IsRefusedOnADraft()
    {
        var order = DraftOrder();

        Assert.True(order.MarkStockReserved().IsFailure);
        Assert.False(order.IsStockReserved);
    }

    // ───────────────────────────── Fabriques ─────────────────────────────

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct(decimal unitHt, string code = "P-CDE", bool fodecApplicable = false) =>
        Product.Create(
            code: code,
            name: "Produit commande",
            type: ProductType.Product,
            unitPrice: Money.Create(unitHt),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: fodecApplicable).Value;

    private static SalesOrder DraftOrder() =>
        SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;

    private static SalesOrder ConfirmedOrder(decimal quantity, decimal unitHt)
    {
        var order = DraftOrder();
        Assert.True(order.AddLine(NewProduct(unitHt), quantity).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);
        return order;
    }
}
