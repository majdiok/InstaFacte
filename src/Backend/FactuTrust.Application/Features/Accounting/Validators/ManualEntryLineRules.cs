using FactuTrust.Application.DTOs;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Features.Accounting.Validators;

/// <summary>
/// Règles de forme communes à la création et à la modification d'une écriture manuelle.
///
/// <para>
/// <b>Les montants qui font foi dépendent de la devise.</b> Pour une écriture en devise de tenue,
/// ce sont <c>Debit</c> / <c>Credit</c>. Pour une écriture en devise, ce sont
/// <c>DebitInCurrency</c> / <c>CreditInCurrency</c> : les montants en dinar sont recalculés par le
/// serveur et valent 0 dans la requête. Contrôler l'équilibre sur les mauvais champs refuserait
/// toute écriture en devise avant même qu'elle n'atteigne le handler.
/// </para>
///
/// <para>
/// L'égalité est exigée <b>exacte après arrondi au millime</b> — la même règle que
/// <c>JournalEntry.BuildLines</c>. L'ancienne tolérance <c>&lt; 0,001</c> du validateur de création
/// divergeait du domaine : un écart strictement inférieur au millime passait le validateur pour se
/// faire refuser ensuite par le domaine, avec un message différent.
/// </para>
/// </summary>
internal static class ManualEntryLineRules
{
    public static bool IsForeignCurrency(string? currencyCode)
    {
        var code = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        return code.Length > 0 && code != Money.DefaultCurrency;
    }

    public static bool IsBalanced(IReadOnlyList<ManualJournalLineRequest> lines, string? currencyCode)
    {
        var foreign = IsForeignCurrency(currencyCode);
        var debit = lines.Sum(l => foreign ? l.DebitInCurrency : l.Debit);
        var credit = lines.Sum(l => foreign ? l.CreditInCurrency : l.Credit);

        return Math.Round(debit, 3) == Math.Round(credit, 3);
    }

    public static bool HasSingleSide(ManualJournalLineRequest line, string? currencyCode)
    {
        var (debit, credit) = IsForeignCurrency(currencyCode)
            ? (line.DebitInCurrency, line.CreditInCurrency)
            : (line.Debit, line.Credit);

        return !(debit > 0 && credit > 0);
    }
}
