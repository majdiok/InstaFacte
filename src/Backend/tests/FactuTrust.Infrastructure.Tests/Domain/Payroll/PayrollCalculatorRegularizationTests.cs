using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Portage de la régularisation IRPP/CSS sur le bulletin : impact sur le net, écrêtage du
/// rappel au net disponible, et lignes produites.
/// </summary>
public sealed class PayrollCalculatorRegularizationTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollComputation Compute(
        decimal baseSalary = 2000m,
        decimal irppRegularization = 0m,
        decimal cssRegularization = 0m)
    {
        return PayrollCalculator.Compute(
            new PayrollComputationInput
            {
                BaseSalary = baseSalary,
                Regime = SocialRegime.Rsna,
                WorkAccidentRate = 0.4m,
                IrppRegularization = irppRegularization,
                CssRegularization = cssRegularization
            },
            Params());
    }

    [Fact]
    public void SansRegularisation_LeBulletinEstStrictementIdentique()
    {
        // Garde-fou de non-régression n°1 : hors mois de régularisation, le moteur doit
        // produire exactement le bulletin d'avant la fonctionnalité.
        var baseline = Compute();

        Assert.Equal(0m, baseline.IrppRegularization);
        Assert.Equal(0m, baseline.CssRegularization);
        Assert.Equal(0m, baseline.RegularizationDeferred);
        Assert.False(baseline.IsRegularizationCapped);

        // Valeurs figées du golden test historique (PayrollCalculatorTests) — preset 2026 corrigé
        // (CNSS RSNA 9,68 % depuis le 01/01/2025).
        Assert.Equal(193.600m, baseline.CnssEmployee);
        Assert.Equal(166.667m, baseline.ProfessionalExpenses);
        Assert.Equal(264.100m, baseline.Irpp);

        // Aucune ligne de régularisation ne doit apparaître.
        Assert.DoesNotContain(baseline.Lines, l => l.Label.Contains("Régularisation"));
    }

    [Fact]
    public void Rappel_ReduitLeNetDuMontantExact()
    {
        var baseline = Compute();
        var withRappel = Compute(irppRegularization: 150m);

        Assert.Equal(150m, withRappel.IrppRegularization);
        Assert.Equal(baseline.NetSalary - 150m, withRappel.NetSalary);
        Assert.False(withRappel.IsRegularizationCapped);

        // L'IRPP mensuel pur ne bouge pas : c'est ce qui rend le cumul idempotent.
        Assert.Equal(baseline.Irpp, withRappel.Irpp);
    }

    [Fact]
    public void Restitution_AugmenteLeNetDuMontantExact()
    {
        var baseline = Compute();
        var withRestitution = Compute(irppRegularization: -220m);

        Assert.Equal(-220m, withRestitution.IrppRegularization);
        Assert.Equal(baseline.NetSalary + 220m, withRestitution.NetSalary);
        Assert.False(withRestitution.IsRegularizationCapped);
        Assert.Equal(0m, withRestitution.RegularizationDeferred);
    }

    [Fact]
    public void RappelSuperieurAuNet_EstEcreteEtLeResteEstReporte()
    {
        // Sans écrêtage, le clamp « net ≥ 0 » avalerait une partie du rappel tout en la
        // comptabilisant au 432 : de l'argent réputé retenu mais jamais prélevé.
        var baseline = Compute();
        var excessive = baseline.NetSalary + 500m;

        var capped = Compute(irppRegularization: excessive);

        Assert.True(capped.IsRegularizationCapped);
        Assert.Equal(baseline.NetSalary, capped.IrppRegularization);
        Assert.Equal(500m, capped.RegularizationDeferred);
        Assert.Equal(0m, capped.NetSalary);

        // Invariant : ce qui est prélevé plus ce qui est reporté égale le rappel demandé.
        Assert.Equal(excessive, capped.IrppRegularization + capped.RegularizationDeferred);
    }

    [Fact]
    public void EcretageServ_IrppEnPrioriteEtCssAbsorbeLeSolde()
    {
        var baseline = Compute();
        var net = baseline.NetSalary;

        // IRPP = net - 10, CSS = 100 → seuls 10 de CSS sont prélevables.
        var capped = Compute(irppRegularization: net - 10m, cssRegularization: 100m);

        Assert.True(capped.IsRegularizationCapped);
        Assert.Equal(net - 10m, capped.IrppRegularization);
        Assert.Equal(10m, capped.CssRegularization);
        Assert.Equal(90m, capped.RegularizationDeferred);
        Assert.Equal(0m, capped.NetSalary);
    }

    [Fact]
    public void Rappel_ProduitUneLigneDeRetenuePositive()
    {
        var computation = Compute(irppRegularization: 150m, cssRegularization: 12m);

        var irppLine = Assert.Single(computation.Lines, l => l.Label == "Régularisation IRPP (rappel)");
        Assert.Equal(PayslipLineKind.Deduction, irppLine.Kind);
        Assert.Equal(150m, irppLine.Amount);

        var cssLine = Assert.Single(computation.Lines, l => l.Label == "Régularisation CSS (rappel)");
        Assert.Equal(PayslipLineKind.Deduction, cssLine.Kind);
        Assert.Equal(12m, cssLine.Amount);

        // Aucun montant négatif ne circule : le PDF et l'affichage restent inchangés.
        Assert.All(computation.Lines, l => Assert.True(l.Amount >= 0));
    }

    [Fact]
    public void Restitution_ProduitUneLigneDeGainPositive()
    {
        var computation = Compute(irppRegularization: -220m);

        var line = Assert.Single(computation.Lines, l => l.Label == "Régularisation IRPP (restitution)");
        Assert.Equal(PayslipLineKind.Earning, line.Kind);
        Assert.Equal(220m, line.Amount);
        Assert.All(computation.Lines, l => Assert.True(l.Amount >= 0));
    }

    [Fact]
    public void LaRegularisationNEstPasVentileeCommeUneRetenueTierce()
    {
        // L'impôt va à l'État (432), pas à un tiers : les lignes ne portent pas de
        // DeductionKind, faute de quoi elles seraient imputées à un compte de tiers.
        var computation = Compute(irppRegularization: 150m);

        var line = Assert.Single(computation.Lines, l => l.Label.StartsWith("Régularisation IRPP"));
        Assert.Null(line.DeductionKind);
    }

    [Fact]
    public void LeBulletinFigeLesMontantsDeRegularisation()
    {
        var computation = Compute(irppRegularization: 150m, cssRegularization: -20m);
        var run = PayrollRun.Create(2026, 12, 2026).Value;
        var (employeeRate, employerRate) = PayrollCalculator.ResolveCnssRates(SocialRegime.Rsna, Params());

        var payslip = Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 12, computation,
            employeeRate, employerRate);
        run.SetPayslips([payslip]);

        Assert.Equal(150m, payslip.IrppRegularization);
        Assert.Equal(-20m, payslip.CssRegularization);

        // Les totaux du cycle agrègent les régularisations à part de l'IRPP mensuel.
        Assert.Equal(150m, run.TotalIrppRegularization);
        Assert.Equal(-20m, run.TotalCssRegularization);
        Assert.Equal(payslip.Irpp, run.TotalIrpp);
    }
}
