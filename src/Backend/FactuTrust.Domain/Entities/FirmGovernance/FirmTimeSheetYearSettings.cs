using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Paramètres de gouvernance des temps et des coûts, versionnés par exercice pour un cabinet.
/// </summary>
/// <remarks>
/// <para>
/// Même doctrine que <c>PayrollYearParameters</c> : les valeurs légales et conventionnelles sont
/// seedées avec des défauts raisonnables mais restent modifiables. Une loi de finances, une
/// convention collective ou un changement de régime de travail ne doit jamais imposer une
/// modification du code.
/// </para>
/// <para>
/// <see cref="EnforceHardLimits"/> est l'interrupteur de non-régression : les exercices antérieurs
/// sont créés à <c>false</c>, ce qui transforme les dépassements en avertissements au lieu de
/// rendre inaccessible un historique saisi avant la mise en place des contrôles.
/// </para>
/// </remarks>
public sealed class FirmTimeSheetYearSettings : Entity
{
    public Guid FirmTenantId { get; private set; }
    public int Year { get; private set; }

    // ---- Régime de travail (Code du travail tunisien, art. 79 et s.) ----

    /// <summary>Régime hebdomadaire du cabinet : 48 h (208 h/mois) ou 40 h (173,33 h/mois).</summary>
    public WeeklyWorkRegime WeeklyRegime { get; private set; } = WeeklyWorkRegime.FortyEightHours;

    // ---- Plafonds de saisie ----

    /// <summary>Plafond horaire journalier, toutes lignes confondues.</summary>
    public decimal MaxDailyHours { get; private set; }

    /// <summary>Plafond horaire hebdomadaire (semaine ISO, lundi → dimanche).</summary>
    public decimal MaxWeeklyHours { get; private set; }

    /// <summary>Nombre de jours de saisie autorisés en avance (0 = aucune saisie future).</summary>
    public int AllowFutureEntryDays { get; private set; }

    /// <summary>Antériorité maximale de saisie, en jours, hors déverrouillage de période.</summary>
    public int MaxBackdatingDays { get; private set; }

    /// <summary>Si faux, les dépassements sont remontés en avertissement sans blocage.</summary>
    public bool EnforceHardLimits { get; private set; }

    // ---- Base de calcul des heures productives (taux horaire de revient) ----

    /// <summary>Congés payés annuels, en jours ouvrables. Minimum légal : 1 jour par mois travaillé.</summary>
    public decimal PaidLeaveDaysPerYear { get; private set; }

    /// <summary>Jours fériés annuels (fêtes légales fixes et mobiles hégiriennes).</summary>
    public decimal PublicHolidayDaysPerYear { get; private set; }

    /// <summary>Part réellement productive du temps de présence, en % (hors administratif interne, formation).</summary>
    public decimal ProductivityRatePercent { get; private set; }

    /// <summary>
    /// Forfait commun ou congés réellement pris par collaborateur.
    /// </summary>
    /// <remarks>
    /// Reste à <see cref="FirmProductiveHoursMode.Parametric"/> par défaut : basculer un exercice
    /// en individualisé change les taux horaires, donc les marges, et doit rester un acte délibéré.
    /// </remarks>
    public FirmProductiveHoursMode ProductiveHoursMode { get; private set; }
        = FirmProductiveHoursMode.Parametric;

    // ---- Taux patronaux (repli quand la paie du cabinet n'est pas exploitable) ----

    /// <summary>Cotisation CNSS patronale, en % du brut.</summary>
    public decimal CnssEmployerRate { get; private set; }

    /// <summary>Taxe de formation professionnelle, en % du brut.</summary>
    public decimal TfpRate { get; private set; }

    /// <summary>Contribution FOPROLOS (part patronale), en % du brut.</summary>
    public decimal FoprolosRate { get; private set; }

    /// <summary>Cotisation accident du travail, en % du brut. Dépend de l'activité : aucun défaut imposé.</summary>
    public decimal WorkAccidentRate { get; private set; }

    /// <summary>Contribution Sociale de Solidarité patronale, en % du brut soumis à CNSS.</summary>
    public decimal CssEmployerRate { get; private set; }

    private FirmTimeSheetYearSettings() { }

    /// <summary>Nombre de jours ouvrables annuels retenu par convention (26 j/mois × 12).</summary>
    public decimal AnnualWorkingDays =>
        OvertimeAmountCalculator.MonthlyWorkingDays * 12m;

    /// <summary>Heures théoriques annuelles avant déduction des congés et jours fériés.</summary>
    public decimal AnnualBaseHours =>
        MillimeRounding.Round(WeeklyRegime.MonthlyHoursDivisor() * 12m);

    /// <summary>Heures travaillées par jour ouvrable, dérivées du régime.</summary>
    public decimal DailyHours =>
        MillimeRounding.Round(WeeklyRegime.MonthlyHoursDivisor() / OvertimeAmountCalculator.MonthlyWorkingDays);

    /// <summary>
    /// Heures productives annuelles : base théorique, moins congés payés et jours fériés,
    /// pondérée par le taux de productivité. C'est le dénominateur du taux horaire de revient.
    /// </summary>
    public decimal AnnualProductiveHours
    {
        get
        {
            var absenceHours = (PaidLeaveDaysPerYear + PublicHolidayDaysPerYear) * DailyHours;
            var presentHours = AnnualBaseHours - absenceHours;
            if (presentHours <= 0)
                return 0m;
            return MillimeRounding.Round(presentHours * ProductivityRatePercent / 100m);
        }
    }

    /// <summary>Somme des taux patronaux applicables au brut, en %.</summary>
    public decimal TotalEmployerChargeRate =>
        MillimeRounding.Round(CnssEmployerRate + TfpRate + FoprolosRate + WorkAccidentRate + CssEmployerRate);

    /// <summary>
    /// Crée les paramètres d'un exercice avec les valeurs par défaut tunisiennes.
    /// </summary>
    /// <param name="enforceHardLimits">
    /// Laisser à <c>false</c> pour un exercice antérieur : les saisies historiques doivent rester
    /// consultables et modifiables même si elles enfreignent les plafonds introduits depuis.
    /// </param>
    public static Result<FirmTimeSheetYearSettings> Create(
        Guid firmTenantId,
        int year,
        bool enforceHardLimits = true,
        WeeklyWorkRegime regime = WeeklyWorkRegime.FortyEightHours)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmTimeSheetYearSettings>(Error.Validation("Tenant", "Cabinet requis"));
        if (year is < 2000 or > 2100)
            return Result.Failure<FirmTimeSheetYearSettings>(Error.Validation("Year", "Année invalide"));

        return Result.Success(new FirmTimeSheetYearSettings
        {
            FirmTenantId = firmTenantId,
            Year = year,
            WeeklyRegime = regime,
            MaxDailyHours = DefaultMaxDailyHours,
            MaxWeeklyHours = DefaultMaxWeeklyHours(regime),
            AllowFutureEntryDays = DefaultAllowFutureEntryDays,
            MaxBackdatingDays = DefaultMaxBackdatingDays,
            EnforceHardLimits = enforceHardLimits,
            PaidLeaveDaysPerYear = DefaultPaidLeaveDaysPerYear,
            PublicHolidayDaysPerYear = DefaultPublicHolidayDaysPerYear,
            ProductivityRatePercent = DefaultProductivityRatePercent,
            CnssEmployerRate = DefaultCnssEmployerRate,
            TfpRate = DefaultTfpRate,
            FoprolosRate = DefaultFoprolosRate,
            WorkAccidentRate = DefaultWorkAccidentRate,
            CssEmployerRate = DefaultCssEmployerRate
        });
    }

    public Result Update(
        WeeklyWorkRegime regime,
        decimal maxDailyHours,
        decimal maxWeeklyHours,
        int allowFutureEntryDays,
        int maxBackdatingDays,
        bool enforceHardLimits,
        decimal paidLeaveDaysPerYear,
        decimal publicHolidayDaysPerYear,
        decimal productivityRatePercent,
        decimal cnssEmployerRate,
        decimal tfpRate,
        decimal foprolosRate,
        decimal workAccidentRate,
        decimal cssEmployerRate = 0m,
        FirmProductiveHoursMode productiveHoursMode = FirmProductiveHoursMode.Parametric)
    {
        if (maxDailyHours is <= 0 or > 24)
            return Result.Failure(Error.Validation("MaxDailyHours", "Le plafond journalier doit être compris entre 0 et 24 heures."));
        if (maxWeeklyHours <= 0 || maxWeeklyHours > 24m * 7m)
            return Result.Failure(Error.Validation("MaxWeeklyHours", "Le plafond hebdomadaire est invalide."));
        if (maxWeeklyHours < maxDailyHours)
            return Result.Failure(Error.Validation("MaxWeeklyHours", "Le plafond hebdomadaire ne peut pas être inférieur au plafond journalier."));
        if (allowFutureEntryDays < 0 || maxBackdatingDays < 0)
            return Result.Failure(Error.Validation("Dates", "Les tolérances de date ne peuvent pas être négatives."));
        if (paidLeaveDaysPerYear < 0 || publicHolidayDaysPerYear < 0)
            return Result.Failure(Error.Validation("Absences", "Les jours d'absence ne peuvent pas être négatifs."));
        if (paidLeaveDaysPerYear + publicHolidayDaysPerYear >= OvertimeAmountCalculator.MonthlyWorkingDays * 12m)
            return Result.Failure(Error.Validation("Absences", "Congés et jours fériés absorbent la totalité de l'année ouvrable."));
        if (productivityRatePercent is <= 0 or > 100)
            return Result.Failure(Error.Validation("ProductivityRatePercent", "Le taux de productivité doit être compris entre 0 et 100 %."));

        var rates = new[] { cnssEmployerRate, tfpRate, foprolosRate, workAccidentRate, cssEmployerRate };
        if (rates.Any(r => r < 0 || r > 100))
            return Result.Failure(Error.Validation("Rates", "Les taux patronaux doivent être compris entre 0 et 100 %."));

        WeeklyRegime = regime;
        MaxDailyHours = MillimeRounding.Round(maxDailyHours);
        MaxWeeklyHours = MillimeRounding.Round(maxWeeklyHours);
        AllowFutureEntryDays = allowFutureEntryDays;
        MaxBackdatingDays = maxBackdatingDays;
        EnforceHardLimits = enforceHardLimits;
        PaidLeaveDaysPerYear = MillimeRounding.Round(paidLeaveDaysPerYear);
        PublicHolidayDaysPerYear = MillimeRounding.Round(publicHolidayDaysPerYear);
        ProductivityRatePercent = MillimeRounding.Round(productivityRatePercent);
        CnssEmployerRate = MillimeRounding.Round(cnssEmployerRate);
        TfpRate = MillimeRounding.Round(tfpRate);
        FoprolosRate = MillimeRounding.Round(foprolosRate);
        WorkAccidentRate = MillimeRounding.Round(workAccidentRate);
        CssEmployerRate = MillimeRounding.Round(cssEmployerRate);
        ProductiveHoursMode = productiveHoursMode;
        return Result.Success();
    }

    // ============================================
    // DÉFAUTS (modifiables par le cabinet)
    // ============================================

    /// <summary>10 h : marge au-delà des 8 h normales pour absorber les heures supplémentaires.</summary>
    public const decimal DefaultMaxDailyHours = 10m;

    /// <summary>Aucune saisie en avance par défaut : on ne déclare pas un temps non encore travaillé.</summary>
    public const int DefaultAllowFutureEntryDays = 0;

    /// <summary>45 jours : couvre la clôture du mois précédent et sa relance.</summary>
    public const int DefaultMaxBackdatingDays = 45;

    /// <summary>12 jours : minimum légal (1 jour ouvrable par mois travaillé). Les conventions collectives peuvent l'élever.</summary>
    public const decimal DefaultPaidLeaveDaysPerYear = 12m;

    /// <summary>12 jours : fêtes légales fixes et mobiles hégiriennes, moyenne constatée.</summary>
    public const decimal DefaultPublicHolidayDaysPerYear = 12m;

    /// <summary>80 % : part productive usuelle en cabinet, le solde couvrant administratif interne et formation.</summary>
    public const decimal DefaultProductivityRatePercent = 80m;

    /// <summary>16,57 % : CNSS patronale du régime général (RSNA), aligné sur PayrollYearParameters.</summary>
    public const decimal DefaultCnssEmployerRate = 16.57m;

    /// <summary>2 % : TFP hors secteur industriel — le cas d'un cabinet d'expertise comptable.</summary>
    public const decimal DefaultTfpRate = 2m;

    /// <summary>1 % : contribution FOPROLOS patronale.</summary>
    public const decimal DefaultFoprolosRate = 1m;

    /// <summary>0 % : le taux accident du travail dépend de l'activité, le cabinet doit le renseigner.</summary>
    public const decimal DefaultWorkAccidentRate = 0m;

    /// <summary>0 % : CSS patronale optionnelle, alignée sur PayrollYearParameters (opt-in).</summary>
    public const decimal DefaultCssEmployerRate = 0m;

    /// <summary>Plafond hebdomadaire par défaut : la durée légale du régime.</summary>
    public static decimal DefaultMaxWeeklyHours(WeeklyWorkRegime regime) =>
        regime == WeeklyWorkRegime.FortyHours ? 40m : 48m;
}
