using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.Services;

/// <summary>
/// Ce que le module paie apporte à la déclaration mensuelle des impôts et taxes pour une période :
/// la TFP et le FOPROLOS (taxes patronales assises sur les salaires), et la retenue à la source
/// opérée sur les traitements (IRPP + CSS salariés).
///
/// <para>
/// <b>Point d'entrée unique.</b> C'est la seule dépendance du domaine fiscal vers le domaine paie :
/// la query de déclaration n'accède plus directement aux dépôts de paie. Un seul endroit à tester,
/// un seul endroit à faire évoluer.
/// </para>
/// </summary>
public sealed class PayrollDeclarationContributionProvider
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly IPayrollParametersRepository _parameters;

    public PayrollDeclarationContributionProvider(
        IPayrollRunRepository payrollRuns,
        IPayrollParametersRepository parameters)
    {
        _payrollRuns = payrollRuns;
        _parameters = parameters;
    }

    public async Task<PayrollMonthlyContribution> GetAsync(int year, int month, CancellationToken cancellationToken)
    {
        var run = await _payrollRuns.GetByPeriodAsync(year, month, cancellationToken);
        if (run is null)
            return PayrollMonthlyContribution.None;

        // Un cycle brouillon ou seulement calculé est provisoire : il ne doit jamais alimenter une
        // déclaration fiscale. On remonte tout de même son existence et son statut, pour que
        // l'écran puisse inviter à le valider plutôt que d'afficher un silence trompeur.
        if (run.Status is not (PayrollRunStatus.Validated or PayrollRunStatus.Closed))
            return PayrollMonthlyContribution.NotUsable(run.Status);

        var (tfpRate, foprolosRate) = await ResolveRatesAsync(run, cancellationToken);

        return new PayrollMonthlyContribution
        {
            RunExists = true,
            Status = run.Status,
            IsUsable = true,
            Tfp = run.TotalTfp,
            Foprolos = run.TotalFoprolos,
            TaxBase = ResolveTaxBase(run, tfpRate, foprolosRate),
            TfpRatePercent = tfpRate,
            FoprolosRatePercent = foprolosRate,
            // Retenue à la source sur traitements et salaires. Les régularisations annuelles sont
            // incluses : elles sont bien retenues (ou restituées) sur le mois concerné.
            WithholdingIrpp = run.TotalIrpp + run.TotalIrppRegularization,
            WithholdingCss = run.TotalCss + run.TotalCssRegularization,
            SalariesGrossBase = run.TotalGross,
            SalariesNetTaxableBase = run.TotalNetTaxable
        };
    }

    /// <summary>
    /// Taux réellement applicables au cycle. On privilégie le taux <b>figé sur le cycle</b> : les
    /// paramètres d'exercice restent modifiables, et les relire ferait bouger l'assiette imprimée
    /// sur des déclarations déjà déposées. Repli sur les paramètres pour les cycles antérieurs à
    /// ce figeage, puis <c>0</c> — l'appelant retombe alors sur ses propres replis plutôt que
    /// d'imprimer un taux inventé.
    /// </summary>
    private async Task<(decimal Tfp, decimal Foprolos)> ResolveRatesAsync(
        PayrollRun run, CancellationToken cancellationToken)
    {
        var parameters = await _parameters.GetByFiscalYearAsync(run.ParametersFiscalYear, cancellationToken);

        var tfpRate = run.AppliedTfpRate
            ?? (parameters is null
                ? 0m
                : parameters.IsIndustrialSector ? parameters.TfpRateIndustry : parameters.TfpRateOther);

        return (tfpRate, parameters?.FoprolosRate ?? 0m);
    }

    /// <summary>
    /// Assiette TFP/FOPROLOS (masse salariale soumise). Figée sur le cycle depuis son introduction ;
    /// pour les cycles antérieurs, on la reconstitue à partir du montant et du taux, ce qui reste
    /// exact tant que les deux proviennent du même calcul. FOPROLOS sert de second repli car son
    /// taux est unique, là où la TFP dépend du secteur d'activité.
    /// </summary>
    private static decimal ResolveTaxBase(PayrollRun run, decimal tfpRate, decimal foprolosRate)
    {
        if (run.TotalPayrollTaxBase is { } persisted)
            return persisted;

        if (tfpRate > 0m && run.TotalTfp > 0m)
            return Round(run.TotalTfp / tfpRate * 100m);

        if (foprolosRate > 0m && run.TotalFoprolos > 0m)
            return Round(run.TotalFoprolos / foprolosRate * 100m);

        return 0m;
    }

    private static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>Apport du module paie pour un mois donné. Tous les montants sont en TND.</summary>
public sealed record PayrollMonthlyContribution
{
    /// <summary>Un cycle de paie existe pour la période, quel que soit son statut.</summary>
    public bool RunExists { get; init; }

    /// <summary>Statut du cycle, ou <c>null</c> si aucun cycle n'existe.</summary>
    public PayrollRunStatus? Status { get; init; }

    /// <summary>
    /// Vrai si le cycle est validé ou clôturé, donc exploitable par une déclaration. Quand c'est
    /// faux, tous les montants valent zéro alors même que <see cref="RunExists"/> peut être vrai.
    /// </summary>
    public bool IsUsable { get; init; }

    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }

    /// <summary>Assiette des taxes sur salaires (masse salariale soumise). 0 si indéterminable.</summary>
    public decimal TaxBase { get; init; }

    public decimal TfpRatePercent { get; init; }
    public decimal FoprolosRatePercent { get; init; }

    /// <summary>IRPP retenu sur les traitements du mois, régularisations annuelles comprises.</summary>
    public decimal WithholdingIrpp { get; init; }

    /// <summary>CSS retenue sur les traitements du mois, régularisations annuelles comprises.</summary>
    public decimal WithholdingCss { get; init; }

    /// <summary>Masse salariale brute — conservée pour le contrat API, plus utilisée comme assiette RS officielle.</summary>
    public decimal SalariesGrossBase { get; init; }

    /// <summary>
    /// Net imposable cumulé des salariés du mois — assiette des articles 1 (IRPP) et 3 (CSS)
    /// du formulaire officiel.
    /// </summary>
    public decimal SalariesNetTaxableBase { get; init; }

    /// <summary>Retenue à la source totale sur traitements et salaires.</summary>
    public decimal WithholdingTotal => WithholdingIrpp + WithholdingCss;

    public static readonly PayrollMonthlyContribution None = new();

    public static PayrollMonthlyContribution NotUsable(PayrollRunStatus status) =>
        new() { RunExists = true, Status = status, IsUsable = false };
}
