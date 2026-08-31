namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Catalogue des presets légaux tunisiens par exercice (LF 2020 à 2026).
///
/// Sources vérifiées le 2026-08-31 (web) pour la présente correction Phase 3 / WS-3 — voir
/// <c>docs/payroll/legal-presets-history.md</c> pour le détail des citations JORT/décrets/notes
/// communes par valeur. Résumé :
/// <list type="bullet">
/// <item>CNSS RSNA 9,68 %/17,07 % : en vigueur à compter du <b>1er janvier 2025</b> (hausse de
/// 0,50 pt salarié/employeur, LF 2025). Avant cette date : 9,18 %/16,57 % (inchangé 2020-2024).</item>
/// <item>CNSS RSA (régime salariés agricoles) : 4,57 % salarié / 7,72 % employeur, en vigueur
/// depuis le 01/07/2011, constant sur toute la période — assiette forfaitaire simplifiée
/// (le moteur applique le taux à la masse salariale comme pour le RSNA ; le calcul SMAG
/// forfaitaire réel n'est pas modélisé).</item>
/// <item>SMIG mensuel (régime 48 h), série décret par décret : 2020-2021 = 429,312 (décret
/// n° 2020-1069 du 30/12/2020, eff. 01/10/2020) ; 2022-2023 = 459,264 (décret n° 2022-769 du
/// 19/10/2022, eff. 01/10/2022) ; 2024 = 491,504 (décrets n° 2024-419/420 du 09/07/2024, eff.
/// 01/05/2024) ; 2025 = 528,320 (mêmes décrets, eff. 01/01/2025) ; 2026 = 554,736 (décret
/// n° 2026-67 du 30/04/2026, JORT n° 44, eff. 01/01/2026).</item>
/// <item>CSS (personnes physiques/salariés) : 1 % pour 2020-2022 (taux de droit commun avant la
/// LF 2023) ; 0,5 % pour 2023-2026 (LF 2023 ramène à 0,5 %, maintenu par la note commune 01-2026 —
/// confirmé par la presse du 14/01/2026, malgré des rumeurs de retour à 1 % non retenues).</item>
/// <item>IRPP 2024 : barème légal 5 tranches (0/26/28/32/35), en vigueur jusqu'au 31/12/2024 —
/// le barème 8 tranches (LF 2025, note commune 03-2025) ne s'applique qu'à compter du
/// 1er janvier 2025. L'ancien <c>Irpp2024Brackets</c> (hybride 0/15/25/30/33/36/38) ne correspond
/// à aucun texte et a été supprimé (CAL-003).</item>
/// </list>
///
/// Rappel (cardinal, préservé) : ces presets ne s'appliquent qu'au seed initial ou à un
/// rechargement explicite (« Recharger défauts LF ») — les <see cref="Entities.Payroll.PayrollYearParameters"/>
/// déjà persistés pour un exercice ne sont jamais réécrits automatiquement.
/// </summary>
public static class PayrollLegalPresets
{
  private static readonly IReadOnlyList<(decimal LowerBound, decimal Rate)> IrppLegacy5Brackets =
  [
    (0m, 0m),
    (5000m, 26m),
    (20000m, 28m),
    (30000m, 32m),
    (50000m, 35m)
  ];

  private static readonly IReadOnlyList<(decimal LowerBound, decimal Rate)> Irpp2025Brackets =
  [
    (0m, 0m),
    (5000m, 15m),
    (10000m, 25m),
    (20000m, 30m),
    (30000m, 33m),
    (40000m, 36m),
    (50000m, 38m),
    (70000m, 40m)
  ];

  /// <summary>
  /// Barème de saisie/cession sur salaire de l'article 354 du code de procédure civile et
  /// commerciale (CPCC), converti en bornes mensuelles (bornes annuelles ÷ 12) : 1/20 jusqu'à
  /// 300 DT/an, 1/10 jusqu'à 600, 1/5 jusqu'à 900, 1/4 jusqu'à 1200, 1/3 jusqu'à 1500, 2/3 jusqu'à
  /// 3000, puis sans limitation au-delà. Ce barème légal est indépendant du SMIG (contrairement à
  /// la convention simplifiée historique de l'application) — utilisé comme défaut pour les
  /// exercices non explicitement présetés (voir <see cref="Resolve"/>). Les tranches déjà
  /// paramétrées par un tenant (<c>ReplaceGarnishmentBrackets</c>) restent inchangées (R-26).
  /// </summary>
  private static readonly IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> GarnishmentBracketsArt354Cpcc =
  [
    (0m, 1m / 20m),
    (25m, 1m / 10m),
    (50m, 1m / 5m),
    (75m, 1m / 4m),
    (100m, 1m / 3m),
    (125m, 2m / 3m),
    (250m, 1m)
  ];

  public static IReadOnlyDictionary<int, PayrollLegalPreset> Presets { get; } =
    new Dictionary<int, PayrollLegalPreset>
    {
      // CNSS RSNA 9,18 %/16,57 % : taux en vigueur jusqu'au 31/12/2024 (avant la hausse LF 2025).
      [2020] = Build(2020, "LF 2020", 9.18m, 16.57m, cssRate: 1m, smig: 429.312m, irpp: IrppLegacy5Brackets),
      [2021] = Build(2021, "LF 2021", 9.18m, 16.57m, cssRate: 1m, smig: 429.312m, irpp: IrppLegacy5Brackets),
      [2022] = Build(2022, "LF 2022", 9.18m, 16.57m, cssRate: 1m, smig: 459.264m, irpp: IrppLegacy5Brackets),
      [2023] = Build(2023, "LF 2023", 9.18m, 16.57m, cssRate: 0.5m, smig: 459.264m, irpp: IrppLegacy5Brackets),
      [2024] = Build(2024, "LF 2024", 9.18m, 16.57m, cssRate: 0.5m, smig: 491.504m, irpp: IrppLegacy5Brackets),
      // CNSS RSNA relevée à 9,68 %/17,07 % à compter du 1er janvier 2025 (LF 2025).
      [2025] = Build(2025, "LF 2025", 9.68m, 17.07m, cssRate: 0.5m, smig: 528.320m, irpp: Irpp2025Brackets),
      [2026] = Build(2026, "LF 2026", 9.68m, 17.07m, cssRate: 0.5m, smig: 554.736m, irpp: Irpp2025Brackets, cssEmployerRate: 0m)
    };

  /// <summary>
  /// Résout le preset légal d'un exercice. Pour un exercice non présenté explicitement
  /// (postérieur au dernier exercice codé), les taux/barèmes du dernier exercice connu sont
  /// repris tels quels (aucune extrapolation légale possible sans nouvelle loi de finances),
  /// à l'exception des tranches de saisie sur salaire qui basculent sur le barème légal
  /// art. 354 CPCC (<see cref="GarnishmentBracketsArt354Cpcc"/>) plutôt que sur la convention
  /// SMIG simplifiée de l'exercice hérité (R-26).
  /// </summary>
  public static PayrollLegalPreset Resolve(int fiscalYear)
  {
    if (Presets.TryGetValue(fiscalYear, out var preset))
      return preset;

    var latestYear = Presets.Keys.Max();
    var latest = Presets[latestYear];
    return latest with
    {
      FiscalYear = fiscalYear,
      Label = $"{latest.Label} (reporté — exercice {fiscalYear} non paramétré par une loi de finances)",
      GarnishmentBrackets = GarnishmentBracketsArt354Cpcc
    };
  }

  private static PayrollLegalPreset Build(
    int year,
    string label,
    decimal cnssEmployee,
    decimal cnssEmployer,
    decimal cssRate,
    decimal smig,
    IReadOnlyList<(decimal LowerBound, decimal Rate)> irpp,
    decimal cssEmployerRate = 0m) =>
    new()
    {
      FiscalYear = year,
      Label = label,
      CnssEmployeeRate = cnssEmployee,
      CnssEmployerRate = cnssEmployer,
      // RSA (régime salariés agricoles) : 4,57 % salarié / 7,72 % employeur depuis le 01/07/2011,
      // distinct du RSNA (CAL-002). Assiette forfaitaire SMAG simplifiée — non modélisée telle
      // quelle, voir docs/payroll/legal-presets-history.md.
      CnssEmployeeRateRsa = 4.57m,
      CnssEmployerRateRsa = 7.72m,
      CssRate = cssRate,
      CssAnnualExemptionThreshold = 5000m,
      CssEmployerRate = cssEmployerRate,
      ProfessionalExpensesRate = 10m,
      ProfessionalExpensesAnnualCap = 2000m,
      HeadOfFamilyAnnualDeduction = 300m,
      ChildAnnualDeduction = 100m,
      MaxDeductibleChildren = 4,
      StudentChildAnnualDeduction = 1000m,
      DisabledChildAnnualDeduction = 2000m,
      ParentDeductionRatePercent = 5m,
      ParentAnnualDeductionCap = 450m,
      TfpRateIndustry = 1m,
      TfpRateOther = 2m,
      FoprolosRate = 1m,
      MonthlySmig = smig,
      IrppBrackets = irpp,
      // Convention historique de l'application (« saisie indexée sur le SMIG », 0 / 1×SMIG /
      // 2×SMIG), désormais paramétrée avec le SMIG propre à l'exercice au lieu d'une liste
      // statique partagée par toutes les années (R-11). Ce n'est pas le barème légal art. 354
      // CPCC (voir GarnishmentBracketsArt354Cpcc, utilisé pour les exercices non présetés) —
      // conservé ici pour ne pas modifier le comportement des tenants déjà seedés sur ces
      // exercices (R-26 : les tranches déjà en base restent inchangées quoi qu'il arrive).
      GarnishmentBrackets = SmigIndexedGarnishmentBrackets(smig)
    };

  private static IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> SmigIndexedGarnishmentBrackets(decimal smig) =>
  [
    (0m, 0m),
    (smig, 0.333m),
    (2m * smig, 0.666m)
  ];
}
