using System.Reflection;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers;
using FactuTrust.Application.Features.Treasury;
using FactuTrust.Application.Features.Treasury.Dtos;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Contrat du contrôleur de trésorerie prévisionnelle : politiques d'autorisation, routes et
/// sérialisation des énumérations.
/// </summary>
/// <remarks>
/// Une action de ce contrôleur qui perdrait sa politique exposerait la trésorerie de la société à
/// tout utilisateur authentifié. Le test le rend impossible sans qu'on s'en aperçoive.
/// </remarks>
public sealed class CashFlowForecastControllerContractTests
{
    private static readonly Type Controller = typeof(CashFlowForecastController);

    private static IEnumerable<MethodInfo> Actions =>
        Controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    [Fact]
    public void Controller_IsRoutedUnderTreasury()
    {
        var route = Controller.GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(route);
        Assert.Equal("api/treasury/cash-forecast", route!.Template);
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(Controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void EveryAction_CarriesATreasuryForecastPolicy()
    {
        var unprotected = Actions
            .Where(a => a.GetCustomAttributes<AuthorizeAttribute>()
                .All(attr => attr.Policy != PermissionPolicies.TreasuryForecastView
                             && attr.Policy != PermissionPolicies.TreasuryForecastManage))
            .Select(a => a.Name)
            .ToList();

        Assert.Empty(unprotected);
    }

    [Theory]
    [InlineData("Recompute")]
    [InlineData("CreateCommitment")]
    [InlineData("UpdateCommitment")]
    [InlineData("DeactivateCommitment")]
    [InlineData("SaveSettings")]
    public void MutatingActions_RequireTheManagePolicy(string actionName)
    {
        var action = Actions.Single(a => a.Name == actionName);

        Assert.Contains(
            action.GetCustomAttributes<AuthorizeAttribute>(),
            attr => attr.Policy == PermissionPolicies.TreasuryForecastManage);
    }

    [Theory]
    [InlineData("GetForecast")]
    [InlineData("GetLines")]
    [InlineData("Export")]
    [InlineData("GetCommitments")]
    [InlineData("GetSettings")]
    public void ReadActions_RequireOnlyTheViewPolicy(string actionName)
    {
        var action = Actions.Single(a => a.Name == actionName);

        Assert.Contains(
            action.GetCustomAttributes<AuthorizeAttribute>(),
            attr => attr.Policy == PermissionPolicies.TreasuryForecastView);
    }

    [Fact]
    public void Policies_AreDerivedFromTheDomainPermissions()
    {
        Assert.Equal("perm:treasury_forecast:view", PermissionPolicies.TreasuryForecastView);
        Assert.Equal("perm:treasury_forecast:manage", PermissionPolicies.TreasuryForecastManage);
    }
}

/// <summary>
/// Sérialisation des énumérations du module : le front consomme des unions de littéraux
/// <c>snake_case</c>, pas les noms .NET. Une divergence casse silencieusement l'affichage.
/// </summary>
public sealed class CashFlowForecastEnumSerializationTests
{
    [Theory]
    [InlineData(CashFlowSourceType.ClientInvoice, "client_invoice")]
    [InlineData(CashFlowSourceType.ClientEffet, "client_effet")]
    [InlineData(CashFlowSourceType.SupplierInvoice, "supplier_invoice")]
    [InlineData(CashFlowSourceType.PayrollContribution, "payroll_contribution")]
    [InlineData(CashFlowSourceType.FiscalObligation, "fiscal_obligation")]
    [InlineData(CashFlowSourceType.LoanInstallment, "loan_installment")]
    [InlineData(CashFlowSourceType.RecurringCommitment, "recurring_commitment")]
    public void SourceType_SerializesToSnakeCase(CashFlowSourceType source, string expected)
    {
        Assert.Equal(expected, CashFlowForecastMappings.ToSlug(source));
    }

    [Fact]
    public void EverySourceType_HasADistinctSlug()
    {
        var slugs = Enum.GetValues<CashFlowSourceType>()
            .Select(CashFlowForecastMappings.ToSlug)
            .ToList();

        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Theory]
    [InlineData("inflow", CashFlowDirection.Inflow)]
    [InlineData("outflow", CashFlowDirection.Outflow)]
    public void Direction_RoundTrips(string slug, CashFlowDirection expected)
    {
        Assert.Equal(expected, CashFlowForecastMappings.ParseDirection(slug));
        Assert.Equal(slug, CashFlowForecastMappings.ToSlug(expected));
    }

    [Fact]
    public void Direction_RejectsUnknownValue()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastMappings.ParseDirection("sideways"));
    }

    [Theory]
    [InlineData("weekly", CashCommitmentFrequency.Weekly)]
    [InlineData("monthly", CashCommitmentFrequency.Monthly)]
    [InlineData("quarterly", CashCommitmentFrequency.Quarterly)]
    [InlineData("semi_annual", CashCommitmentFrequency.SemiAnnual)]
    [InlineData("annual", CashCommitmentFrequency.Annual)]
    public void Frequency_RoundTrips(string slug, CashCommitmentFrequency expected)
    {
        Assert.Equal(expected, CashFlowForecastMappings.ParseFrequency(slug));
        Assert.Equal(slug, CashFlowForecastMappings.ToSlug(expected));
    }

    [Fact]
    public void SourceType_ParsesItsOwnSlug()
    {
        foreach (var source in Enum.GetValues<CashFlowSourceType>())
        {
            var slug = CashFlowForecastMappings.ToSlug(source);
            Assert.Equal(source, CashFlowForecastMappings.ParseSourceType(slug));
        }
    }

    [Fact]
    public void SourceType_RejectsUnknownValue()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastMappings.ParseSourceType("crypto"));
    }

    [Fact]
    public void Scenario_ExposesBothProbabilitiesAndTheirProvenance()
    {
        // L'écran doit pouvoir montrer ce que l'IA a déplacé : les deux valeurs voyagent toujours.
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 100_000m, 10_000m, 50m);
        scenario.ApplyAiProbability(58m, 15, "Carnet ferme");

        var dto = CashFlowForecastMappings.ToDto(scenario);

        Assert.Equal("realistic", dto.Kind);
        Assert.Equal(58m, dto.ProbabilityPercent);
        Assert.Equal(50m, dto.DeterministicProbabilityPercent);
        Assert.Equal("ai_adjusted", dto.ProbabilitySource);
        Assert.Equal("Carnet ferme", dto.AiRationale);
    }

    [Fact]
    public void Insight_MapsSeverityAndOrigin()
    {
        var insight = CashFlowForecastInsight.Alert(
            CashFlowInsightSeverity.Critical,
            "Rupture prévue",
            "Détail",
            new DateTime(2026, 8, 1),
            -12_230m,
            CashFlowInsightOrigin.Rule,
            0);

        var dto = CashFlowForecastMappings.ToDto(insight);

        Assert.Equal("alert", dto.Kind);
        Assert.Equal("critical", dto.Severity);
        Assert.Equal("rule", dto.Origin);
        Assert.Equal(-12_230m, dto.EstimatedBalance);
    }
}
