using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcule le montant saisissable et alloue les saisies par priorité.
///
/// <para>
/// R-26 (CAL-013) : le barème réellement appliqué est celui des tranches
/// <see cref="Entities.Payroll.PayrollGarnishmentBracket"/> de l'exercice (paramétrable par
/// tenant via <c>ReplaceGarnishmentBrackets</c>), désormais seedé par défaut, pour les exercices
/// non déjà présetés, avec le barème légal de l'article 354 du code de procédure civile et
/// commerciale (CPCC) — voir <c>PayrollLegalPresets.GarnishmentBracketsArt354Cpcc</c>. Les
/// exercices déjà présetés (2020-2026) conservent la convention historique indexée sur le SMIG de
/// l'exercice pour ne pas modifier le comportement des tenants existants.
/// </para>
/// <para>
/// <see cref="ComputeAvailableSeizable"/> ci-dessous ne sert de secours que lorsque le tenant n'a
/// <i>aucune</i> tranche configurée (cas normalement inatteignable depuis un preset) :
/// pension alimentaire → 50 % du net (convention prudente, l'alimentaire peut légalement
/// atteindre la portion normalement insaisissable — art. 354 al. 2 CPCC) ; à défaut de tranches et
/// hors alimentaire → 33 % forfaitaire. Le calcul par tranches légales est la voie normale.
/// </para>
/// </summary>
public static class GarnishmentCalculator
{
    public sealed record GarnishmentAllocation(
        Guid GarnishmentId,
        GarnishmentType Type,
        string Label,
        decimal RequestedAmount,
        decimal AppliedAmount,
        decimal CarriedOverAmount,
        string? BeneficiaryRib);

    public static decimal ComputeAvailableSeizable(
        decimal monthlyNetBeforeGarnishments,
        IReadOnlyList<PayrollGarnishmentBracket> brackets,
        bool hasAlimony)
    {
        if (monthlyNetBeforeGarnishments <= 0)
            return 0m;

        if (hasAlimony)
        {
            // Pension alimentaire : jusqu'à 50 % du net (convention prudente).
            return R(monthlyNetBeforeGarnishments * 0.5m);
        }

        if (brackets.Count == 0)
            return R(monthlyNetBeforeGarnishments * 0.33m);

        var ordered = brackets.OrderBy(b => b.LowerBoundMonthlyNet).ToList();
        decimal seizable = 0m;
        for (var i = 0; i < ordered.Count; i++)
        {
            var lower = ordered[i].LowerBoundMonthlyNet;
            var upper = i + 1 < ordered.Count ? ordered[i + 1].LowerBoundMonthlyNet : decimal.MaxValue;
            if (monthlyNetBeforeGarnishments <= lower)
                break;

            var taxableInBracket = Math.Min(monthlyNetBeforeGarnishments, upper) - lower;
            if (taxableInBracket <= 0)
                continue;

            seizable += taxableInBracket * ordered[i].SeizableFraction;
        }

        return R(seizable);
    }

    public static IReadOnlyList<GarnishmentAllocation> Allocate(
        decimal availableSeizable,
        IEnumerable<GarnishmentRequest> garnishments)
    {
        var remaining = availableSeizable;
        var result = new List<GarnishmentAllocation>();

        var ordered = garnishments
            .OrderBy(g => g.Type == GarnishmentType.Alimony ? 0 : 1)
            .ThenBy(g => g.Priority)
            .ThenBy(g => g.IssuedAt)
            .ToList();

        foreach (var g in ordered)
        {
            if (remaining <= 0)
            {
                result.Add(new GarnishmentAllocation(
                    g.Id, g.Type, g.Label, g.RequestedAmount, 0m, g.RequestedAmount, g.BeneficiaryRib));
                continue;
            }

            var applied = Math.Min(g.RequestedAmount, remaining);
            var carriedOver = R(g.RequestedAmount - applied);
            remaining = R(remaining - applied);

            result.Add(new GarnishmentAllocation(
                g.Id, g.Type, g.Label, g.RequestedAmount, applied, carriedOver, g.BeneficiaryRib));
        }

        return result;
    }

    public sealed record GarnishmentRequest(
        Guid Id,
        GarnishmentType Type,
        string Label,
        int Priority,
        DateTime IssuedAt,
        decimal RequestedAmount,
        string? BeneficiaryRib);

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
