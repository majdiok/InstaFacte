namespace FactuTrust.Domain.Entities.Fiscal;

/// <summary>
/// Valeurs par défaut des paramètres d'impôt par exercice (Tunisie), utilisées à la fois par
/// l'initialiseur de base et comme repli lorsqu'aucune ligne n'existe pour l'exercice.
///
/// IMPORTANT : ce sont des <b>valeurs de référence indicatives</b>. Le droit fiscal tunisien évolue à
/// chaque loi de finances : elles doivent être validées par l'expert-comptable pour chaque exercice via
/// l'écran de paramétrage fiscal. Une ligne modifiée par l'utilisateur
/// (<see cref="IncomeTaxYearParameter.IsUserModified"/>) n'est jamais écrasée par ces défauts.
/// </summary>
public static class IncomeTaxYearParameterDefaults
{
    public const decimal IsStandardRate = 0.15m;
    public const decimal IsReducedRate = 0.10m;
    public const decimal IsSectorRate = 0.35m;

    /// <summary>Minimum d'impôt : 0,2 % du CA local TTC, plancher 500 TND (régime de droit commun).</summary>
    public const decimal MinTaxRate = 0.002m;
    public const decimal MinTaxFloorTnd = 500m;
    /// <summary>Minimum d'impôt réduit : 0,1 % du CA local TTC, plancher 300 TND.</summary>
    public const decimal MinTaxReducedRate = 0.001m;
    public const decimal MinTaxFloorReducedTnd = 300m;

    /// <summary>
    /// Contribution sociale de solidarité : due par les sociétés soumises à l'IS.
    /// Taux de référence 1 % de l'assiette imposable (taux majorés pour certains secteurs :
    /// banques, assurances, télécoms — à paramétrer par exercice).
    /// </summary>
    public const bool CssApplies = true;
    public const decimal CssRate = 0.01m;
    public const decimal CssFloorTnd = 0m;

    public const decimal AcompteRate = 0.30m;
    public const int AcompteCount = 3;
    /// <summary>Report des déficits ordinaires : 5 exercices (les amortissements différés sont illimités).</summary>
    public const int DeficitCarryForwardYears = 5;

    /// <summary>Arrondi de l'assiette imposable au dinar inférieur (pratique déclarative).</summary>
    public const bool RoundTaxableToDinar = true;

    /// <summary>Barème IRPP progressif applicable aux exercices antérieurs à la LF 2025 (annuel, TND).</summary>
    public const string IrppBracketsJsonLegacy =
        "[{\"lower\":0,\"rate\":0},{\"lower\":5000,\"rate\":26},{\"lower\":20000,\"rate\":28},{\"lower\":30000,\"rate\":32},{\"lower\":50000,\"rate\":35}]";

    /// <summary>
    /// Barème IRPP progressif restructuré par la loi de finances 2025 (annuel, TND).
    /// Valeur de référence — à confirmer par l'expert-comptable pour l'exercice concerné.
    /// </summary>
    public const string IrppBracketsJson2025 =
        "[{\"lower\":0,\"rate\":0},{\"lower\":5000,\"rate\":15},{\"lower\":10000,\"rate\":25},{\"lower\":20000,\"rate\":30},{\"lower\":30000,\"rate\":33},{\"lower\":40000,\"rate\":36},{\"lower\":50000,\"rate\":38},{\"lower\":70000,\"rate\":40}]";

    /// <summary>Premier exercice d'application du barème IRPP issu de la LF 2025.</summary>
    public const int Lf2025FirstFiscalYear = 2025;

    /// <summary>Barème IRPP par défaut applicable à un exercice donné.</summary>
    public static string IrppBracketsJsonFor(int fiscalYear) =>
        fiscalYear >= Lf2025FirstFiscalYear ? IrppBracketsJson2025 : IrppBracketsJsonLegacy;

    /// <summary>Conservé pour compatibilité : barème de l'exercice courant.</summary>
    public static string IrppBracketsJson => IrppBracketsJsonFor(DateTime.UtcNow.Year);

    public static IncomeTaxYearParameter Create(int fiscalYear) =>
        IncomeTaxYearParameter.Create(
            fiscalYear,
            IsStandardRate, IsReducedRate, IsSectorRate,
            MinTaxRate, MinTaxReducedRate, MinTaxFloorTnd,
            CssApplies, CssRate, CssFloorTnd,
            AcompteRate, AcompteCount, DeficitCarryForwardYears,
            IrppBracketsJsonFor(fiscalYear),
            MinTaxFloorReducedTnd,
            RoundTaxableToDinar);
}
