using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Domain.Services.Accounting;

/// <summary>
/// Convertit les lignes d'une écriture saisie en devise étrangère vers la devise de tenue.
///
/// <para>
/// <b>Sens du taux — à ne jamais inverser.</b> <c>rate</c> est le nombre d'unités de devise de tenue
/// pour <b>une</b> unité de devise étrangère (1 EUR = 3,31420 TND ⇒ <c>rate = 3.31420</c>). La
/// contre-valeur s'obtient donc par multiplication, jamais par division.
/// </para>
///
/// <para>
/// <b>Absorption du résidu d'arrondi.</b> L'arrondi au millime se fait ligne à ligne : une écriture
/// équilibrée en devise peut laisser quelques millimes d'écart une fois convertie. Le résidu est
/// imputé sur la ligne de plus fort montant du côté déficitaire, ce qui rend un déséquilibre en
/// devise de tenue structurellement impossible tout en minimisant la distorsion relative.
/// </para>
///
/// <para>
/// L'arrondi retenu est <see cref="MillimeRounding"/> (<c>AwayFromZero</c>, convention comptable
/// tunisienne déjà employée par la paie), et non l'arrondi bancaire implicite de
/// <see cref="ValueObjects.Money"/>. Comme <c>Money.Create</c> ré-arrondit à 3 décimales une valeur
/// qui l'est déjà, la divergence entre les deux conventions est neutralisée.
/// </para>
/// </summary>
public static class ForeignCurrencyConversion
{
    /// <summary>
    /// Complète <see cref="JournalLineInput.Debit"/> / <see cref="JournalLineInput.Credit"/> en
    /// devise de tenue à partir des montants en devise portés par
    /// <see cref="JournalLineInput.DebitInCurrency"/> / <see cref="JournalLineInput.CreditInCurrency"/>.
    /// </summary>
    public static Result<IReadOnlyList<JournalLineInput>> Convert(IReadOnlyList<JournalLineInput> lines, decimal rate)
    {
        if (rate <= 0)
            return Result.Failure<IReadOnlyList<JournalLineInput>>(
                Error.Validation("ExchangeRate", "Le taux de change doit être strictement positif."));

        if (lines.Count < 2)
            return Result.Failure<IReadOnlyList<JournalLineInput>>(
                Error.Validation("Lines", "Au moins deux lignes sont requises"));

        decimal sumDebitCurrency = 0, sumCreditCurrency = 0;
        foreach (var line in lines)
        {
            if (line.DebitInCurrency < 0 || line.CreditInCurrency < 0)
                return Result.Failure<IReadOnlyList<JournalLineInput>>(
                    Error.Validation("Amount", "Les montants doivent être positifs ou nuls"));

            if (line.DebitInCurrency > 0 && line.CreditInCurrency > 0)
                return Result.Failure<IReadOnlyList<JournalLineInput>>(
                    Error.Validation("Amount", "Une ligne ne peut pas avoir débit et crédit simultanément"));

            sumDebitCurrency += line.DebitInCurrency;
            sumCreditCurrency += line.CreditInCurrency;
        }

        // L'équilibre s'apprécie d'abord dans la devise de saisie : c'est ce que l'utilisateur voit.
        if (MillimeRounding.Round(sumDebitCurrency) != MillimeRounding.Round(sumCreditCurrency))
            return Result.Failure<IReadOnlyList<JournalLineInput>>(
                Error.Validation("Balance", "L'écriture n'est pas équilibrée en devise (débit ≠ crédit)"));

        var converted = new List<JournalLineInput>(lines.Count);
        decimal sumDebit = 0, sumCredit = 0;

        foreach (var line in lines)
        {
            var debit = MillimeRounding.Round(line.DebitInCurrency * rate);
            var credit = MillimeRounding.Round(line.CreditInCurrency * rate);
            sumDebit += debit;
            sumCredit += credit;
            converted.Add(line with { Debit = debit, Credit = credit });
        }

        var residual = sumDebit - sumCredit;
        if (residual != 0)
        {
            var absorbed = AbsorbResidual(converted, residual);
            if (absorbed.IsFailure)
                return Result.Failure<IReadOnlyList<JournalLineInput>>(absorbed.Error);

            converted = absorbed.Value;
        }

        return Result.Success<IReadOnlyList<JournalLineInput>>(converted);
    }

    /// <summary>
    /// Impute le résidu sur la ligne de plus fort montant du côté déficitaire. Le montant est
    /// toujours augmenté, jamais diminué : aucune ligne ne peut devenir négative.
    /// </summary>
    private static Result<List<JournalLineInput>> AbsorbResidual(List<JournalLineInput> lines, decimal residual)
    {
        // residual > 0 : le débit l'emporte, c'est donc le crédit qu'il faut compléter.
        var creditIsShort = residual > 0;
        var amount = Math.Abs(residual);

        var index = -1;
        decimal best = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var candidate = creditIsShort ? lines[i].Credit : lines[i].Debit;
            if (candidate > best)
            {
                best = candidate;
                index = i;
            }
        }

        // Défense en profondeur : une écriture équilibrée en devise avec des montants non nuls a
        // forcément une ligne de chaque côté. Si ce n'est pas le cas, mieux vaut refuser que
        // fabriquer une écriture bancale.
        if (index < 0 || best <= 0)
            return Result.Failure<List<JournalLineInput>>(Error.Validation("Balance",
                "Le résidu de conversion ne peut être imputé sur aucune ligne."));

        lines[index] = creditIsShort
            ? lines[index] with { Credit = lines[index].Credit + amount }
            : lines[index] with { Debit = lines[index].Debit + amount };

        return Result.Success(lines);
    }
}
