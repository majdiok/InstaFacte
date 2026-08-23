using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Snapshot of a catalog line used to create a standalone supplier invoice (no PO / receipt).
/// Discount is baked into <c>SubTotal</c> at creation; it is not persisted as its own column.
/// </summary>
public sealed record StandaloneSupplierInvoiceLineInput(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string? ProductDescription,
    decimal Quantity,
    string? Unit,
    Money UnitPrice,
    VatRate VatRate,
    decimal DiscountPercent = 0m);
