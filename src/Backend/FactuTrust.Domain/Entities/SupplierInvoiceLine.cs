using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a line item on a supplier invoice.
/// These are typically copied from PurchaseOrderLine upon creation.
/// </summary>
public sealed class SupplierInvoiceLine : Entity
{
    public Guid SupplierInvoiceId { get; private set; }
    public SupplierInvoice SupplierInvoice { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }

    public decimal Quantity { get; private set; }
    public string? Unit { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }

    public Money SubTotal { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    public bool IsFixedAsset { get; private set; }
    public string? AssetAccountNumber { get; private set; }
    public Guid? DepreciationRateCategoryId { get; private set; }

    private SupplierInvoiceLine() { }

    internal static SupplierInvoiceLine Create(
        SupplierInvoice supplierInvoice,
        int lineNumber,
        Guid productId,
        string productCode,
        string productName,
        string? productDescription,
        decimal quantity,
        string? unit,
        Money unitPrice,
        VatRate vatRate,
        Money subTotal,
        Money vatAmount,
        Money total)
    {
        return new SupplierInvoiceLine
        {
            SupplierInvoiceId = supplierInvoice.Id,
            SupplierInvoice = supplierInvoice,
            LineNumber = lineNumber,
            ProductId = productId,
            ProductCode = productCode,
            ProductName = productName,
            ProductDescription = productDescription,
            Quantity = quantity,
            Unit = unit,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            SubTotal = subTotal,
            VatAmount = vatAmount,
            Total = total
        };
    }

    public void SetFixedAssetClassification(bool isFixedAsset, string? assetAccountNumber, Guid? depreciationRateCategoryId)
    {
        IsFixedAsset = isFixedAsset;
        if (!isFixedAsset)
        {
            AssetAccountNumber = null;
            DepreciationRateCategoryId = null;
            return;
        }

        AssetAccountNumber = string.IsNullOrWhiteSpace(assetAccountNumber) ? null : assetAccountNumber.Trim();
        DepreciationRateCategoryId = depreciationRateCategoryId == Guid.Empty ? null : depreciationRateCategoryId;
    }
}
