namespace FactuTrust.Application.Features.BankAccounts;

internal static class BankAccountNormalization
{
    public static string NormalizeIban(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
