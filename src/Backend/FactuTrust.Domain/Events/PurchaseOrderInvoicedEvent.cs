using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class PurchaseOrderInvoicedEvent : DomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string PurchaseOrderNumber { get; }
    public Guid SupplierInvoiceId { get; }

    public PurchaseOrderInvoicedEvent(Guid purchaseOrderId, string purchaseOrderNumber, Guid supplierInvoiceId)
    {
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderNumber = purchaseOrderNumber;
        SupplierInvoiceId = supplierInvoiceId;
    }
}
