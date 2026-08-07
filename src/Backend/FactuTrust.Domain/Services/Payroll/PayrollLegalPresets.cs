namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Catalogue des presets légaux tunisiens par exercice (LF 2020 à 2026).
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

  private static readonly IReadOnlyList<(decimal LowerBound, decimal Rate)> Irpp2024Brackets =
  [
    (0m, 0m),
    (5000m, 15m),
    (20000m, 25m),
    (30000m, 30m),
    (40000m, 33m),
    (50000m, 36m),
    (70000m, 38m)
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

  private static readonly IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> GarnishmentBrackets =
  [
    (0m, 0m),
    (528.320m, 0.333m),
    (1056.640m, 0.666m)
  ];

  public static IReadOnlyDictionary<int, PayrollLegalPreset> Presets { get; } =
    new Dictionary<int, PayrollLegalPreset>
    {
      [2020] = Build(2020, "LF 2020", 9.18m, 16.57m, 466.000m, IrppLegacy5Brackets),
      [2021] = Build(2021, "LF 2021", 9.18m, 16.57m, 466.000m, IrppLegacy5Brackets),
      [2022] = Build(2022, "LF 2022", 9.18m, 16.57m, 466.000m, IrppLegacy5Brackets),
      [2023] = Build(2023, "LF 2023", 9.18m, 16.57m, 486.000m, IrppLegacy5Brackets),
      [2024] = Build(2024, "LF 2024", 9.18m, 16.57m, 511.000m, Irpp2024Brackets),
      [2025] = Build(2025, "LF 2025", 9.18m, 16.57m, 528.320m, Irpp2025Brackets),
      [2026] = Build(2026, "LF 2026", 9.18m, 16.57m, 528.320m, Irpp2025Brackets, cssEmployerRate: 0m)
    };

  public static PayrollLegalPreset Resolve(int fiscalYear)
  {
    if (Presets.TryGetValue(fiscalYear, out var preset))
      return preset;
    var latestYear = Presets.Keys.Max();
    return Presets[latestYear];
  }

  private static PayrollLegalPreset Build(
    int year,
    string label,
    decimal cnssEmployee,
    decimal cnssEmployer,
    decimal smig,
    IReadOnlyList<(decimal LowerBound, decimal Rate)> irpp,
    decimal cssEmployerRate = 0m) =>
    new()
    {
      FiscalYear = year,
      Label = label,
      CnssEmployeeRate = cnssEmployee,
      CnssEmployerRate = cnssEmployer,
      CnssEmployeeRateRsa = cnssEmployee,
      CnssEmployerRateRsa = cnssEmployer,
      CssRate = 0.5m,
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
      GarnishmentBrackets = GarnishmentBrackets
    };
}
