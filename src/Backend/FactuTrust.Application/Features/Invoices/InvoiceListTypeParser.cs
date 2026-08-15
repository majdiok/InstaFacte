using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Invoices;

/// <summary>
/// Parses the optional invoice-list <c>type</c> query string
/// (<c>INVOICE</c> / <c>CREDIT_NOTE</c>, plus domain aliases).
/// Omitted or blank means "all document types" (no filter).
/// </summary>
public static class InvoiceListTypeParser
{
    public const string InvalidTypeMessage = "Le paramètre type doit être INVOICE ou CREDIT_NOTE.";

    public static bool TryParse(string? value, out InvoiceType? type, out string? error)
    {
        type = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        switch (value.Trim())
        {
            case "INVOICE":
            case "invoice":
            case "Standard":
            case "standard":
            case "0":
                type = InvoiceType.Standard;
                return true;
            case "CREDIT_NOTE":
            case "credit_note":
            case "CreditNote":
            case "creditNote":
            case "1":
                type = InvoiceType.CreditNote;
                return true;
            default:
                error = InvalidTypeMessage;
                return false;
        }
    }
}
