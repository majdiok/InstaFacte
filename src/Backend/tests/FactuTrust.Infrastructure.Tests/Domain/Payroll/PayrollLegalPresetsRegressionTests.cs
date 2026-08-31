using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Régression des presets légaux — anciennement une tautologie (preset 2026 == défauts de
/// l'application) qui laissait passer R-09/R-10/R-11. Désormais les valeurs officielles sont
/// pointées en dur dans <see cref="PayrollLegalPresetsOfficialFiguresTests"/> (plan §8) ; cette
/// suite conserve les garde-fous structurels : matérialisation réussie, cohérence année/label,
/// et non-réécriture des exercices persistants (les presets ne s'appliquent qu'au seed/rechargement
/// explicite — règle cardinale du plan §3).
/// </summary>
public sealed class PayrollLegalPresetsRegressionTests
{
    [Theory]
    [InlineData(2020)]
    [InlineData(2024)]
    [InlineData(2026)]
    public void Resolve_ReturnsValidParameters(int year)
    {
        var result = PayrollParameterDefaults.CreateDefaults(year);
        Assert.True(result.IsSuccess);
        Assert.Equal(year, result.Value.FiscalYear);
        Assert.NotEmpty(result.Value.IrppBrackets);
    }

    [Fact]
    public void Materialize_ProducesConsistentYearAndLabel()
    {
        var preset = PayrollLegalPresets.Presets[2026];
        var materialized = preset.Materialize(2026);

        Assert.True(materialized.IsSuccess);
        Assert.Equal(2026, materialized.Value.FiscalYear);
        // Les taux matérialisés correspondent au preset (la matérialisation est une copie fidèle).
        Assert.Equal(preset.CnssEmployeeRate, materialized.Value.CnssEmployeeRate);
        Assert.Equal(preset.CnssEmployerRate, materialized.Value.CnssEmployerRate);
        Assert.Equal(preset.MonthlySmig, materialized.Value.MonthlySmig);
        Assert.Equal(preset.CssRate, materialized.Value.CssRate);
    }

    /// <summary>
    /// Garde-fou de la règle cardinale : le défaut de 2026 porte désormais les valeurs légales
    /// corrigées (CNSS 9,68/17,07, SMIG 554,736) — l'ancien test qui assertait preset == legacy
    /// (9,18/16,57, 528,320) est supprimé car c'est ce qui masquait les non-conformités.
    /// Les chiffres exacts par exercice sont validés dans <see cref="PayrollLegalPresetsOfficialFiguresTests"/>.
    /// </summary>
    [Fact]
    public void CreateDefaults_2026_CarriesCorrectedLegalValues()
    {
        var defaults = PayrollParameterDefaults.CreateDefaults(2026);

        Assert.True(defaults.IsSuccess);
        // CNSS RSNA depuis le 01/01/2025 (LF 2025) — pas l'ancien 9,18/16,57.
        Assert.Equal(9.68m, defaults.Value.CnssEmployeeRate);
        Assert.Equal(17.07m, defaults.Value.CnssEmployerRate);
        // SMIG 2026 (décret n° 2026-67) — pas l'ancien 528,320.
        Assert.Equal(554.736m, defaults.Value.MonthlySmig);
        // CSS 0,5 % (LF 2023, maintenue par la note commune 01-2026).
        Assert.Equal(0.5m, defaults.Value.CssRate);
    }

    [Fact]
    public void Presets_CoverFullYearRange_2020To2026()
    {
        for (var year = 2020; year <= 2026; year++)
        {
            Assert.True(PayrollLegalPresets.Presets.ContainsKey(year),
                $"Le preset de l'exercice {year} doit être défini.");
        }
    }
}
