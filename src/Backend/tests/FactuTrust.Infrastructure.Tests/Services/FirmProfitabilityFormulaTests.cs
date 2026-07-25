using System.Globalization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>Golden tests calqués sur les formules Décisiel.</summary>
public sealed class FirmProfitabilityFormulaTests
{
    [Fact]
    public void Timesheet_margin_hours_times_rate()
    {
        const decimal hours = 10m;
        const decimal rate = 50m;
        const decimal budget = 1000m;
        var cost = hours * rate;
        var margin = budget - cost;
        Assert.Equal(500m, cost);
        Assert.Equal(500m, margin);
    }

    [Fact]
    public void Timesheet_zero_hours_margin_equals_budget()
    {
        const decimal hours = 0m;
        const decimal rate = 50m;
        const decimal budget = 1000m;
        var cost = hours * rate;
        var margin = budget - cost;
        Assert.Equal(0m, cost);
        Assert.Equal(1000m, margin);
    }

    [Fact]
    public void Timesheet_missing_budget_allows_negative_margin()
    {
        const decimal hours = 10m;
        const decimal rate = 50m;
        const decimal budget = 0m;
        var margin = budget - (hours * rate);
        Assert.Equal(-500m, margin);
    }

    [Fact]
    public void Collaborator_rentability_decisiel_formula()
    {
        var r = FirmCollaboratorRentability.Compute(
            totalRevenue: 100_000m,
            payrollCost: 40_000m,
            adminCharge: 5_000m,
            itCharge: 3_000m,
            operatingCharge: 2_000m);
        Assert.Equal(50_000m, r);
    }

    [Fact]
    public void Charges_scale_by_headcount()
    {
        const decimal unit = 1000m;
        const int headcount = 3;
        Assert.Equal(3000m, unit * headcount);
    }

    [Fact]
    public void Negative_rentability_allowed()
    {
        var r = FirmCollaboratorRentability.Compute(10_000m, 20_000m, 0, 0, 0);
        Assert.Equal(-10_000m, r);
    }

    // ============================================
    // Arrondi au millime (convention tunisienne)
    // ============================================
    // Math.Round(x, 3) sans MidpointRounding applique l'arrondi bancaire : le demi-millime
    // part vers le chiffre pair. La comptabilité tunisienne arrondit toujours à l'écart de zéro.

    // Les décimales sont passées en chaîne : un littéral decimal n'est pas une constante
    // d'attribut valide en C#, et passer par double introduirait une imprécision binaire.
    [Theory]
    [InlineData("0.0005", "0.001")]   // bancaire : 0,000 (0 est pair)
    [InlineData("0.0015", "0.002")]   // bancaire : 0,002 — identique, sert de témoin
    [InlineData("0.0025", "0.003")]   // bancaire : 0,002 (2 est pair)
    [InlineData("-0.0005", "-0.001")] // symétrie sur les valeurs négatives
    [InlineData("-0.0025", "-0.003")]
    public void Millime_rounding_goes_away_from_zero(string input, string expected)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);
        var awaited = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.Equal(awaited, MillimeRounding.Round(value));
    }

    [Fact]
    public void Millime_rounding_differs_from_banker_rounding()
    {
        // Verrou de non-régression : si quelqu'un réintroduit Math.Round(x, 3), ce test tombe.
        Assert.NotEqual(Math.Round(0.0025m, 3), MillimeRounding.Round(0.0025m));
    }

    [Fact]
    public void Rentability_compute_rounds_to_millime_away_from_zero()
    {
        // 10,0000 − 9,9975 = 0,0025 → 0,003 (et non 0,002)
        var r = FirmCollaboratorRentability.Compute(10.0000m, 9.9975m, 0m, 0m, 0m);
        Assert.Equal(0.003m, r);
    }

    // La couverture d'arrondi millime portée par Config_unit_rounds_to_millime_away_from_zero est
    // reprise dans CollaboratorMarginCalculatorTests, la configuration annuelle ayant été supprimée.

    [Fact]
    public void Dossier_year_budget_rounds_to_millime_away_from_zero()
    {
        var budget = FirmDossierYearBudget.Create(Guid.NewGuid(), Guid.NewGuid(), 2026, 1000.0025m).Value;
        Assert.Equal(1000.003m, budget.BudgetAnnuel);
    }

    [Fact]
    public void Timesheet_hours_keep_three_decimals()
    {
        // 7 h 20 min = 7,333 h : la précision (9,3) doit être conservée, pas tronquée à 7,33.
        var entry = FirmTimeSheetEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Test", new DateTime(2026, 7, 20), 7.3333m).Value;
        Assert.Equal(7.333m, entry.Hours);
    }
}
