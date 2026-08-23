using System.Text;

namespace FactuTrust.Application.Common;

/// <summary>
/// Normalisation des numéros / références de facture fournisseur pour la détection de doublons.
/// Canon : majuscules, alphanumériques seuls, zéros de tête du bloc numérique final retirés.
/// </summary>
public static class SupplierInvoiceNumberNormalizer
{
    public static string Normalize(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return string.Empty;

        var kept = new StringBuilder(invoiceNumber.Length);
        foreach (var ch in invoiceNumber)
        {
            if (char.IsLetterOrDigit(ch))
                kept.Append(char.ToUpperInvariant(ch));
        }

        var canonical = kept.ToString();
        if (canonical.Length == 0) return string.Empty;

        var digitsStart = canonical.Length;
        while (digitsStart > 0 && char.IsDigit(canonical[digitsStart - 1]))
            digitsStart--;

        if (digitsStart == canonical.Length) return canonical;

        var prefix = canonical[..digitsStart];
        var digits = canonical[digitsStart..].TrimStart('0');
        return prefix + (digits.Length == 0 ? "0" : digits);
    }
}
