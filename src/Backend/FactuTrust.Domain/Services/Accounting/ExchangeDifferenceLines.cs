using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Accounting;

/// <summary>
/// Construit les deux lignes de l'écriture qui apure un écart de change.
///
/// <para>
/// <b>Le signe décide du sens, et une inversion serait silencieuse.</b> <c>gap</c> vaut
/// débit − crédit, en devise de tenue, sur le compte lettré :
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>gap &gt; 0</c> — le compte est trop débité. On le <b>crédite</b> ; la perte de change
///     part au débit du compte d'imputation (classiquement 655).
///   </description></item>
///   <item><description>
///     <c>gap &lt; 0</c> — le compte est trop crédité : la dette avait été enregistrée à un taux
///     plus élevé que celui du règlement. On le <b>débite</b> ; le gain de change part au crédit
///     (classiquement 756).
///   </description></item>
/// </list>
/// </summary>
public static class ExchangeDifferenceLines
{
    public static IReadOnlyList<JournalLineInput> Build(
        string letteredAccount,
        string adjustmentAccount,
        decimal gap,
        string label)
    {
        var amount = Math.Abs(gap);
        var creditLetteredAccount = gap > 0;

        return new[]
        {
            new JournalLineInput(
                letteredAccount, label,
                creditLetteredAccount ? 0m : amount,
                creditLetteredAccount ? amount : 0m,
                null, ThirdPartyKind.None),
            new JournalLineInput(
                adjustmentAccount, label,
                creditLetteredAccount ? amount : 0m,
                creditLetteredAccount ? 0m : amount,
                null, ThirdPartyKind.None)
        };
    }
}
