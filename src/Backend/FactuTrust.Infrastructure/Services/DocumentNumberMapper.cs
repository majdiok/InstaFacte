using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services;

public static class DocumentNumberMapper
{
    public static NumberingDocumentType ToNumberingType(string legacyPrefix) => legacyPrefix.ToUpperInvariant() switch
    {
        "FAC" => NumberingDocumentType.Invoice,
        "AVO" => NumberingDocumentType.CreditNote,
        "DEV" => NumberingDocumentType.Quote,
        "BL" => NumberingDocumentType.DeliveryNote,
        "BC" => NumberingDocumentType.PurchaseOrder,
        "TR" => NumberingDocumentType.StockTransfer,
        "INVE" => NumberingDocumentType.PhysicalInventory,
        "ENC" => NumberingDocumentType.CashReceipt,
        "DEP" => NumberingDocumentType.CashExpense,
        "REM" => NumberingDocumentType.BankDeposit,
        "CDE" => NumberingDocumentType.SalesOrder,
        _ => throw new ArgumentException($"Unknown legacy prefix: {legacyPrefix}", nameof(legacyPrefix))
    };

    public static InvoiceNumber ToInvoiceNumber(DocumentNumberResult result) => InvoiceNumber.Create(result.Prefix ?? "FAC", result.Year, result.Sequence);
    public static QuoteNumber ToQuoteNumber(DocumentNumberResult result) => QuoteNumber.Create(result.Prefix ?? "DEV", result.Year, result.Sequence);
    public static DeliveryNoteNumber ToDeliveryNoteNumber(DocumentNumberResult result) => DeliveryNoteNumber.FromRendered(result.Value, result.Year, result.Sequence);
    public static PurchaseOrderNumber ToPurchaseOrderNumber(DocumentNumberResult result) => PurchaseOrderNumber.Create(result.Prefix ?? "BC", result.Year, result.Sequence);
    public static SalesOrderNumber ToSalesOrderNumber(DocumentNumberResult result) => SalesOrderNumber.Create(result.Prefix ?? "CDE", result.Year, result.Sequence);
    public static StockTransferNumber ToStockTransferNumber(DocumentNumberResult result) => StockTransferNumber.Create(result.Prefix ?? "TR", result.Year, result.Sequence);
    public static Result<CashOperationNumber> ToCashOperationNumber(DocumentNumberResult result) => CashOperationNumber.Create(result.Prefix ?? "DEP", result.Year, result.Sequence);
    public static Result<BankDepositNumber> ToBankDepositNumber(DocumentNumberResult result) => BankDepositNumber.Create(result.Year, result.Sequence);
}
