namespace FactuTrust.Domain.Common;

/// <summary>
/// Arrondi monétaire au millime tunisien (3 décimales, arrondi commercial).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Math.Round(decimal, int)"/> applique par défaut l'arrondi bancaire
/// (<see cref="MidpointRounding.ToEven"/>) : 0,0005 devient 0,000 et 0,0015 devient 0,002.
/// Ce comportement est incompatible avec la convention comptable tunisienne, où le demi-millime
/// s'arrondit toujours au millime supérieur en valeur absolue.
/// </para>
/// <para>
/// C'est la convention déjà retenue par le domaine paie
/// (<c>PayrollCalculator</c>, <c>OvertimeAmountCalculator</c>, <c>PayrollRun</c>) et par
/// <c>TunisianValidationRules.RoundToMillimes</c> côté Application. Ce helper la rend disponible
/// au domaine, qui ne peut pas référencer la couche Application.
/// </para>
/// </remarks>
public static class MillimeRounding
{
    /// <summary>Nombre de décimales du dinar tunisien (1 TND = 1 000 millimes).</summary>
    public const int Decimals = 3;

    /// <summary>Arrondit un montant au millime le plus proche, le demi-millime s'écartant de zéro.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>Arrondit à un nombre de décimales donné, avec la même règle de demi-unité.</summary>
    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
