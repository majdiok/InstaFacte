using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Treasury;

/// <summary>Solde d'un compte de trésorerie à une date.</summary>
public sealed record CashPositionAccountDto
{
    public string AccountNumber { get; init; } = null!;
    public string Label { get; init; } = null!;

    /// <summary>Solde débiteur net (débit − crédit). Négatif = compte à découvert.</summary>
    public decimal Balance { get; init; }

    /// <summary>Compte bancaire du référentiel correspondant, quand la correspondance existe.</summary>
    public Guid? BankAccountId { get; init; }

    /// <summary>Vrai pour un compte de caisse (54x), faux pour une banque (532x).</summary>
    public bool IsCash { get; init; }
}

/// <summary>Position de trésorerie consolidée à une date.</summary>
public sealed record CashPositionDto
{
    public DateTime AsOf { get; init; }

    /// <summary>Somme des soldes de classe 5 retenus. Peut être négative.</summary>
    public decimal TotalBalance { get; init; }

    public string Currency { get; init; } = "TND";

    public IReadOnlyList<CashPositionAccountDto> Accounts { get; init; } =
        Array.Empty<CashPositionAccountDto>();
}

/// <summary>
/// Solde de trésorerie réel, ancré en comptabilité, à une date donnée.
/// </summary>
/// <remarks>
/// <para>
/// Point d'entrée unique pour « combien y a-t-il en caisse et en banque à telle date ». Le calcul
/// existait jusqu'ici enfoui dans <c>BankReconciliationService</c>, pour un seul compte à la fois.
/// </para>
/// <para>
/// Ne pas confondre avec <c>AccountingDashboardDto.AvailableCash</c>, qui somme toute la classe 5
/// sans borne de date : inutilisable comme solde d'ouverture d'une projection.
/// </para>
/// </remarks>
public interface ITreasuryPositionService
{
    Task<CashPositionDto> GetCashPositionAsync(DateTime asOf, CancellationToken cancellationToken = default);
}

/// <summary>
/// Racines de comptes du plan comptable tunisien utilisées par le prévisionnel de trésorerie.
/// </summary>
public static class TreasuryAccountRoots
{
    /// <summary>Classe 5 — comptes financiers.</summary>
    public const string FinancialClass = "5";

    /// <summary>Banques (le référentiel impose ce préfixe à <c>BankAccount.ChartOfAccountNumber</c>).</summary>
    public const string Bank = "532";

    /// <summary>Caisse.</summary>
    public const string Cash = "54";

    /// <summary>
    /// Virements internes. Exclus du solde : un transfert caisse → banque y transite et serait
    /// compté deux fois.
    /// </summary>
    public const string InternalTransfer = "58";

    /// <summary>Vrai si le compte doit entrer dans la position de trésorerie.</summary>
    public static bool IsTreasuryAccount(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return false;

        var account = accountNumber.Trim();
        return account.StartsWith(FinancialClass, StringComparison.Ordinal)
               && !account.StartsWith(InternalTransfer, StringComparison.Ordinal);
    }

    /// <summary>Vrai pour un compte de caisse.</summary>
    public static bool IsCashAccount(string? accountNumber) =>
        !string.IsNullOrWhiteSpace(accountNumber)
        && accountNumber.Trim().StartsWith(Cash, StringComparison.Ordinal);

    /// <summary>Arrondit un solde au millime.</summary>
    public static decimal RoundBalance(decimal value) => MillimeRounding.Round(value);
}
