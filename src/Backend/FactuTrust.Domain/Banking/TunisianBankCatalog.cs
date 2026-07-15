namespace FactuTrust.Domain.Banking;

/// <summary>
/// Reference data for Tunisian banks (codes, display names, typical SWIFT/BIC).
/// BIC values are indicative; users can override per account.
/// </summary>
public static class TunisianBankCatalog
{
    public sealed record BankEntry(string Code, string Name, string? DefaultSwiftBic);

    /// <summary>
    /// Ordered list of major Tunisian banking institutions.
    /// </summary>
    public static readonly IReadOnlyList<BankEntry> Banks = new List<BankEntry>
    {
        new("BIAT", "Banque Internationale Arabe de Tunisie", "BIATTNTT"),
        new("STB", "Société Tunisienne de Banque", "STBKTNTT"),
        new("BNA", "Banque Nationale Agricole", "BNAATNTT"),
        new("BH", "Banque de l'Habitat", "BHBTNTT1"),
        new("ATTIJARI", "Attijari Bank Tunisie", "ATTJTNTT"),
        new("UIB", "Union Internationale de Banques", "UIBKTNTT"),
        new("AMEN", "Amen Bank", "AMENKNTT"),
        new("BT", "Banque de Tunisie", "BTEXTNTT"),
        new("UBCI", "Union Bancaire pour le Commerce et l'Industrie", "UBCITNTT"),
        new("ABC", "Arab Banking Corporation", "ABCOBHBH"),
        new("QNB", "QNB Tunisie", "QNBAQAQA"),
        new("ZITOUNA", "Banque Zitouna", "BZITTNTT"),
        new("ALBARAKA", "Al Baraka Bank", "BARBTNTT"),
        new("BTK", "Banque Tuniso-Koweïtienne", "BTKOTNTT"),
        new("WIFAK", "Banque de Financement des Petites et Moyennes Entreprises (Wifak Bank)", "WIFAKNTT"),
        new("TSB", "Tunisian Saudi Bank", "TSBKTNTT"),
        new("BTL", "Banque Tuniso-Libyenne", "BTLNTNTT"),
        new("BTS", "Banque Tuniso-Saoudienne", "BTSATNTT"),
        new("BTE", "Banque de Tunisie et des Émirats", "BTEKTNTT"),
        new("CITI", "Citibank Tunisie", "CITITNTT"),
        new("BFPME", "Banque de Financement des Petites et Moyennes Entreprises", "BFPMETNT"),
        new("BFT", "Banque Franco-Tunisienne", "BFTNTNTT"),
        new("AUTRE", "Autre banque", null)
    }.AsReadOnly();

    public static BankEntry? FindByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var c = code.Trim();
        foreach (var b in Banks)
        {
            if (string.Equals(b.Code, c, StringComparison.OrdinalIgnoreCase))
                return b;
        }

        return null;
    }
}
