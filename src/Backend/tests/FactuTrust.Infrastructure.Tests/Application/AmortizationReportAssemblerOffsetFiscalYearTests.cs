using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Tests unitaires de <see cref="AmortizationReportAssembler"/> (plan « Exercices décalés », P4) :
/// en-tête d'exercice conscient de la frontière décalée (dates de début/fin d'exercice réelles),
/// parité stricte janvier (01/01/N → 31/12/N), groupement par clé d'exercice inchangé et aucun
/// montant modifié.
/// </summary>
public sealed class AmortizationReportAssemblerOffsetFiscalYearTests
{
    private static readonly Guid AssetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001");

    private static AmortizationReportAssetProjection CreateProjection(
        decimal origin = 40_000m,
        decimal priorAcc = 8_000m,
        decimal dotationCalc = 8_000m,
        decimal dotaCompta = 8_000m,
        decimal endAccum = 16_000m,
        decimal endNbv = 24_000m,
        string account = "224",
        string categoryCode = "VEH_PASS",
        string categoryLabel = "Véhicules de tourisme") =>
        new(
            AssetId,
            account,
            "IMMO-2026-0001",
            "Voiture",
            new DateTime(2026, 1, 10),
            origin,
            5m,
            DepreciationMethod.Linear,
            categoryCode,
            categoryLabel,
            priorAcc,
            dotationCalc,
            dotaCompta,
            endAccum,
            endNbv,
            TotalLineCount: 1,
            PostedLineCount: 1);

    // ------------------------------------------------------------------
    // En-tête — exercice civil (parité stricte janvier)
    // ------------------------------------------------------------------

    [Fact]
    public void Assemble_CivilStart_PeriodShouldBeCalendarYear()
    {
        var projections = new[] { CreateProjection() };
        var report = AmortizationReportAssembler.Assemble(
            projections, fiscalYear: 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test", fiscalYearStartMonth: 1);

        Assert.Equal(2026, report.Header.FiscalYear);
        Assert.Equal(new DateTime(2026, 1, 1), report.Header.PeriodStart);
        Assert.Equal(new DateTime(2026, 12, 31), report.Header.PeriodEnd);
        Assert.Equal("Société Test", report.Header.CompanyName);
    }

    [Fact]
    public void Assemble_LegacyOverload_ShouldMatchCivilOverload()
    {
        // La surcharge historique (sans startMonth) délègue avec mois 1 → résultats identiques
        // (parité stricte avec le comportement antérieur).
        var projections = new[] { CreateProjection() };
        var legacy = AmortizationReportAssembler.Assemble(
            projections, 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test");
        var civil = AmortizationReportAssembler.Assemble(
            projections, 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test", 1);

        Assert.Equal(civil.Header.PeriodStart, legacy.Header.PeriodStart);
        Assert.Equal(civil.Header.PeriodEnd, legacy.Header.PeriodEnd);
        Assert.Equal(civil.Header.FiscalYear, legacy.Header.FiscalYear);
        Assert.Equal(civil.GrandTotal.DotationCalculeeExercice, legacy.GrandTotal.DotationCalculeeExercice);
    }

    // ------------------------------------------------------------------
    // En-tête — exercice décalé (juil. → juin)
    // ------------------------------------------------------------------

    [Fact]
    public void Assemble_OffsetStart_PeriodShouldBeFiscalYearBounds()
    {
        var projections = new[] { CreateProjection() };
        var report = AmortizationReportAssembler.Assemble(
            projections, fiscalYear: 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test", fiscalYearStartMonth: 7);

        // Exercice 2026 = juil. 2026 → juin 2027. La clé logique reste 2026 (année de début, D2).
        Assert.Equal(2026, report.Header.FiscalYear);
        Assert.Equal(new DateTime(2026, 7, 1), report.Header.PeriodStart);
        Assert.Equal(new DateTime(2027, 6, 30), report.Header.PeriodEnd);
    }

    [Fact]
    public void Assemble_OffsetStart_March_LeapYearEnd_ShouldBeFebruary29()
    {
        // Exercice démarrant en mars : exercice 2023 (mar. 2023 → fév. 2024), 2024 bissextile.
        var projections = new[] { CreateProjection() };
        var report = AmortizationReportAssembler.Assemble(
            projections, 2023, AmortizationReportGroupingMode.AssetAccount, "Société Test", fiscalYearStartMonth: 3);

        Assert.Equal(new DateTime(2023, 3, 1), report.Header.PeriodStart);
        Assert.Equal(new DateTime(2024, 2, 29), report.Header.PeriodEnd);
    }

    // ------------------------------------------------------------------
    // Montants & groupement — inchangés (P2 : lignes déjà étiquetées par clé)
    // ------------------------------------------------------------------

    [Fact]
    public void Assemble_OffsetStart_ShouldNotModifyAmounts()
    {
        var projections = new[]
        {
            CreateProjection(origin: 40_000m, dotationCalc: 8_000m, dotaCompta: 4_000m, endNbv: 20_000m),
            CreateProjection(origin: 10_000m, priorAcc: 0m, dotationCalc: 2_000m, dotaCompta: 0m, endAccum: 2_000m, endNbv: 8_000m,
                account: "213", categoryCode: "MOB_BUR", categoryLabel: "Mobilier bureau")
        };

        var report = AmortizationReportAssembler.Assemble(
            projections, 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test", fiscalYearStartMonth: 7);

        Assert.Equal(50_000m, report.GrandTotal.OriginValue);
        Assert.Equal(10_000m, report.GrandTotal.DotationCalculeeExercice);
        Assert.Equal(4_000m, report.GrandTotal.DotationComptabiliseeExercice);
        Assert.Equal(28_000m, report.GrandTotal.EndOfYearNetBookValue);
    }

    [Fact]
    public void Assemble_OffsetStart_GroupingByKeyUnchanged()
    {
        var projections = new[]
        {
            CreateProjection(account: "2241", categoryCode: "VEH_PASS", categoryLabel: "Véhicules"),
            CreateProjection(account: "2242", categoryCode: "VEH_UTIL", categoryLabel: "Utilitaires"),
            CreateProjection(account: "2131", categoryCode: "MOB_BUR", categoryLabel: "Mobilier")
        };

        var report = AmortizationReportAssembler.Assemble(
            projections, 2026, AmortizationReportGroupingMode.AssetAccount, "Société Test", fiscalYearStartMonth: 7);

        // Groupement par compte (préfixe 3 chiffres) inchangé, indépendant du mois de début d'exercice.
        var groupKeys = report.Groups.Select(g => g.GroupCode).ToList();
        Assert.Equal(new[] { "213", "224" }, groupKeys);
    }
}
