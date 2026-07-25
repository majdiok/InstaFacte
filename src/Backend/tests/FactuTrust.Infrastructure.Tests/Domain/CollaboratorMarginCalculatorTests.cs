using FactuTrust.Domain.Services.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class CollaboratorMarginCalculatorTests
{
    private static readonly Guid Amine = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Sonia = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Karim = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ============================================
    // RÉPARTITION DES HONORAIRES
    // ============================================

    [Fact]
    public void Revenue_is_split_in_proportion_to_hours()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            10_000m,
            new Dictionary<Guid, decimal> { [Amine] = 60m, [Sonia] = 40m });

        Assert.Equal(6_000m, shares[Amine]);
        Assert.Equal(4_000m, shares[Sonia]);
    }

    [Fact]
    public void A_lone_collaborator_takes_the_whole_budget()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            12_000m,
            new Dictionary<Guid, decimal> { [Amine] = 35m });

        Assert.Equal(12_000m, shares[Amine]);
    }

    [Fact]
    public void The_shares_always_add_up_to_the_budget()
    {
        // 1 000,000 / 3 ne tombe pas juste : sans absorption du résidu, un millime disparaîtrait
        // à chaque répartition.
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            1_000m,
            new Dictionary<Guid, decimal> { [Amine] = 1m, [Sonia] = 1m, [Karim] = 1m });

        Assert.Equal(1_000m, shares.Values.Sum());
    }

    [Fact]
    public void The_rounding_residual_goes_to_the_largest_contributor()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            1_000m,
            new Dictionary<Guid, decimal> { [Amine] = 50m, [Sonia] = 25m, [Karim] = 25m });

        Assert.Equal(1_000m, shares.Values.Sum());
        // Amine porte le double des heures : il absorbe l'écart et reste le mieux doté.
        Assert.True(shares[Amine] >= shares[Sonia]);
        Assert.True(shares[Amine] >= shares[Karim]);
    }

    [Fact]
    public void Allocation_is_deterministic_across_runs()
    {
        // Condition nécessaire pour qu'un recalcul global soit idempotent.
        var weights = new Dictionary<Guid, decimal> { [Amine] = 1m, [Sonia] = 1m, [Karim] = 1m };

        var first = CollaboratorMarginCalculator.AllocateDossierRevenue(1_000m, weights);
        var second = CollaboratorMarginCalculator.AllocateDossierRevenue(1_000m, weights);

        Assert.Equal(first[Amine], second[Amine]);
        Assert.Equal(first[Sonia], second[Sonia]);
        Assert.Equal(first[Karim], second[Karim]);
    }

    [Fact]
    public void A_dossier_without_hours_allocates_nothing()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            10_000m,
            new Dictionary<Guid, decimal> { [Amine] = 0m, [Sonia] = 0m });

        Assert.All(shares.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void A_dossier_without_budget_allocates_nothing()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            0m,
            new Dictionary<Guid, decimal> { [Amine] = 10m, [Sonia] = 5m });

        Assert.All(shares.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void A_collaborator_without_hours_gets_nothing_but_stays_listed()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            900m,
            new Dictionary<Guid, decimal> { [Amine] = 10m, [Sonia] = 0m });

        Assert.Equal(900m, shares[Amine]);
        Assert.Equal(0m, shares[Sonia]);
    }

    [Fact]
    public void No_collaborator_at_all_yields_an_empty_allocation()
    {
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            5_000m, new Dictionary<Guid, decimal>());

        Assert.Empty(shares);
    }

    // ============================================
    // ARRONDI AU MILLIME
    // ============================================
    // Couverture reprise de Config_unit_rounds_to_millime_away_from_zero, supprimé avec l'entité
    // de configuration : l'arrondi doit rester commercial et non bancaire.

    [Fact]
    public void Allocation_rounds_to_the_millime_away_from_zero()
    {
        // 0,005 partagé en deux : 0,0025 par part → 0,003 en arrondi commercial (0,002 en bancaire).
        var shares = CollaboratorMarginCalculator.AllocateDossierRevenue(
            0.005m,
            new Dictionary<Guid, decimal> { [Amine] = 1m, [Sonia] = 1m });

        Assert.Equal(0.005m, shares.Values.Sum());
        Assert.Contains(shares.Values, v => v == 0.003m);
    }

    // ============================================
    // RÉPARTITION DE LA STRUCTURE
    // ============================================

    [Fact]
    public void Support_cost_is_split_in_proportion_to_employer_cost()
    {
        // Amine coûte le double de Sonia : il absorbe le double de structure.
        var shares = CollaboratorMarginCalculator.AllocateSupportCost(
            9_000m,
            new Dictionary<Guid, decimal> { [Amine] = 40_000m, [Sonia] = 20_000m });

        Assert.Equal(6_000m, shares[Amine]);
        Assert.Equal(3_000m, shares[Sonia]);
        Assert.Equal(9_000m, shares.Values.Sum());
    }

    [Fact]
    public void Support_cost_is_zero_when_no_direct_cost_is_known()
    {
        // Aucun coût employeur renseigné : on ne divise pas par zéro et on n'invente rien.
        var shares = CollaboratorMarginCalculator.AllocateSupportCost(
            9_000m,
            new Dictionary<Guid, decimal> { [Amine] = 0m, [Sonia] = 0m });

        Assert.All(shares.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void No_support_staff_means_no_structure_charge()
    {
        var shares = CollaboratorMarginCalculator.AllocateSupportCost(
            0m,
            new Dictionary<Guid, decimal> { [Amine] = 40_000m, [Sonia] = 20_000m });

        Assert.All(shares.Values, v => Assert.Equal(0m, v));
    }

    // ============================================
    // MARGE
    // ============================================

    [Fact]
    public void Margin_deducts_direct_cost_then_structure()
    {
        var margin = CollaboratorMarginCalculator.ComputeMargin(
            revenue: 100_000m, directCost: 40_000m, supportShare: 6_000m);

        Assert.Equal(54_000m, margin);
    }

    [Fact]
    public void Margin_may_be_negative()
    {
        // Un collaborateur peut coûter plus qu'il ne produit : le chiffre doit le dire.
        var margin = CollaboratorMarginCalculator.ComputeMargin(10_000m, 20_000m, 1_000m);

        Assert.Equal(-11_000m, margin);
    }

    [Fact]
    public void Margin_rounds_to_the_millime_away_from_zero()
    {
        var margin = CollaboratorMarginCalculator.ComputeMargin(10.0000m, 9.9975m, 0m);

        Assert.Equal(0.003m, margin);
    }
}
