using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Régularisation IRPP/CSS annuelle d'un salarié, rattachée au mois de paie qui la porte
/// (décembre ou solde de tout compte).
///
/// C'est une **variable du mois**, au même titre qu'une ligne d'heures supplémentaires : elle
/// est saisie ou générée avant le calcul du cycle, verrouillée dès que le mois est validé, puis
/// consommée par le moteur de paie qui la porte sur le bulletin.
///
/// Les cumuls stockés sont un instantané du calcul : ils justifient le montant retenu et
/// alimentent le tableau « Détail du calcul » de l'écran, sans nécessiter de recalcul.
/// </summary>
public sealed class PayrollIrppRegularization : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }

    /// <summary>Motif du déclenchement (fin d'exercice, solde de tout compte, saisie manuelle).</summary>
    public IrppRegularizationReason Reason { get; private set; }

    /// <summary>Nombre de mois entrés dans le cumul — signale une année partielle.</summary>
    public int MonthsCounted { get; private set; }

    /// <summary>Cumul du net imposable de l'exercice.</summary>
    public decimal CumulNetTaxable { get; private set; }
    /// <summary>Cumul de l'IRPP déjà retenu (hors régularisation).</summary>
    public decimal CumulIrppWithheld { get; private set; }
    /// <summary>Cumul de la CSS déjà retenue (hors régularisation).</summary>
    public decimal CumulCssWithheld { get; private set; }

    /// <summary>IRPP réellement dû sur le cumul.</summary>
    public decimal IrppDue { get; private set; }
    /// <summary>CSS réellement due sur le cumul.</summary>
    public decimal CssDue { get; private set; }

    /// <summary>Écart IRPP calculé (positif = rappel, négatif = restitution).</summary>
    public decimal ComputedIrppDelta { get; private set; }
    /// <summary>Écart CSS calculé, même convention de signe.</summary>
    public decimal ComputedCssDelta { get; private set; }

    /// <summary>Écart IRPP forcé par le gestionnaire, s'il a corrigé le calcul.</summary>
    public decimal? OverrideIrppDelta { get; private set; }
    /// <summary>Écart CSS forcé par le gestionnaire.</summary>
    public decimal? OverrideCssDelta { get; private set; }

    /// <summary>Note libre justifiant un ajustement.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Instantané JSON du détail mois par mois ayant servi au calcul (affichage et traçabilité).
    /// </summary>
    public string? DetailJson { get; private set; }

    /// <summary>Vrai si un montant a été forcé manuellement.</summary>
    public bool IsOverridden => OverrideIrppDelta.HasValue || OverrideCssDelta.HasValue;

    /// <summary>Écart IRPP effectivement porté sur le bulletin.</summary>
    public decimal EffectiveIrppDelta => OverrideIrppDelta ?? ComputedIrppDelta;
    /// <summary>Écart CSS effectivement porté sur le bulletin.</summary>
    public decimal EffectiveCssDelta => OverrideCssDelta ?? ComputedCssDelta;

    /// <summary>Écart total effectivement porté sur le bulletin.</summary>
    public decimal EffectiveTotalDelta => R(EffectiveIrppDelta + EffectiveCssDelta);

    /// <summary>Vrai si aucun écart n'est à porter : la ligne peut être ignorée.</summary>
    public bool IsNeutral => EffectiveTotalDelta == 0m;

    private PayrollIrppRegularization() { }

    public static Result<PayrollIrppRegularization> Create(
        Guid employeeId,
        int year,
        int month,
        IrppRegularizationReason reason,
        IrppRegularizationResult result,
        string? detailJson = null,
        string? notes = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<PayrollIrppRegularization>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year is < 2000 or > 2100)
            return Result.Failure<PayrollIrppRegularization>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<PayrollIrppRegularization>(Error.Validation("Month", "Mois invalide."));

        ArgumentNullException.ThrowIfNull(result);

        var entity = new PayrollIrppRegularization
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            Reason = reason,
            Notes = Normalize(notes)
        };
        entity.ApplyComputation(result, detailJson);
        return Result.Success(entity);
    }

    /// <summary>
    /// Rafraîchit les cumuls et l'écart calculé (régénération d'un cycle recalculé).
    /// Un montant forcé manuellement est **conservé** : la décision du gestionnaire ne doit
    /// pas disparaître en silence — l'écran signale la ligne comme ajustée.
    /// </summary>
    public Result Refresh(IrppRegularizationResult result, string? detailJson = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        ApplyComputation(result, detailJson);
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Force les écarts à porter sur le bulletin (null = revenir au calculé).</summary>
    public Result SetOverride(decimal? irppDelta, decimal? cssDelta, string? notes = null)
    {
        OverrideIrppDelta = irppDelta.HasValue ? R(irppDelta.Value) : null;
        OverrideCssDelta = cssDelta.HasValue ? R(cssDelta.Value) : null;
        Notes = Normalize(notes) ?? Notes;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Rétablit les montants calculés.</summary>
    public Result ClearOverride()
    {
        OverrideIrppDelta = null;
        OverrideCssDelta = null;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Requalifie le motif (ex. un solde de tout compte détecté après coup).</summary>
    public void SetReason(IrppRegularizationReason reason)
    {
        if (Reason == reason)
            return;
        Reason = reason;
        IncrementVersion();
    }

    private void ApplyComputation(IrppRegularizationResult result, string? detailJson)
    {
        MonthsCounted = result.MonthsCounted;
        CumulNetTaxable = R(result.CumulNetTaxable);
        CumulIrppWithheld = R(result.CumulIrppWithheld);
        CumulCssWithheld = R(result.CumulCssWithheld);
        IrppDue = R(result.IrppDue);
        CssDue = R(result.CssDue);
        ComputedIrppDelta = R(result.IrppDelta);
        ComputedCssDelta = R(result.CssDelta);
        if (detailJson is not null)
            DetailJson = detailJson;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
