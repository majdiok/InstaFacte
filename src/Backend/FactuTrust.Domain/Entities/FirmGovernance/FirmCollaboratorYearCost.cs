using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Origine des montants de coût employeur d'un collaborateur.</summary>
public enum FirmPayrollCostSource
{
    /// <summary>Saisi par le cabinet.</summary>
    Manual = 1,
    /// <summary>Agrégé depuis les bulletins de paie du cabinet.</summary>
    ImportedFromPayroll = 2
}

/// <summary>
/// Coût employeur annuel d'un collaborateur et, le cas échéant, son taux horaire imposé.
/// </summary>
/// <remarks>
/// <para>
/// Point de vérité unique pour tout ce qui coûte, par collaborateur et par exercice. Alimente
/// à la fois le taux horaire de revient de l'analyse par dossier et le volet masse salariale de
/// la rentabilité collaborateur : deux écrans qui doivent nécessairement s'accorder.
/// </para>
/// <para>
/// Une ligne par exercice, et non une colonne unique sur le profil : un taux 2026 ne doit jamais
/// réécrire l'historique 2025, sous peine de rendre incomparables deux exercices déjà analysés.
/// </para>
/// </remarks>
public sealed class FirmCollaboratorYearCost : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid CollaboratorUserId { get; private set; }
    public int Year { get; private set; }

    /// <summary>Salaire brut annuel.</summary>
    public decimal GrossAnnualSalary { get; private set; }

    /// <summary>Charges patronales annuelles : CNSS, accident du travail, TFP, FOPROLOS.</summary>
    public decimal EmployerContributions { get; private set; }

    /// <summary>Éléments annexes annuels (primes exceptionnelles, avantages, etc.).</summary>
    public decimal PayrollExtras { get; private set; }

    public FirmPayrollCostSource Source { get; private set; } = FirmPayrollCostSource.Manual;

    /// <summary>Horodatage du dernier import de paie, nul si la ligne est saisie.</summary>
    public DateTime? ImportedAt { get; private set; }

    /// <summary>
    /// Taux horaire imposé par le cabinet. Nul dans le cas nominal, où le taux est dérivé du coût.
    /// </summary>
    public decimal? HourlyRateOverride { get; private set; }

    /// <summary>Motif de l'imposition du taux — obligatoire, c'est ce qui la rend défendable en revue.</summary>
    public string? OverrideJustification { get; private set; }

    private FirmCollaboratorYearCost() { }

    /// <summary>Coût employeur annuel total : c'est le numérateur du taux horaire de revient.</summary>
    public decimal TotalEmployerCost =>
        MillimeRounding.Round(GrossAnnualSalary + EmployerContributions + PayrollExtras);

    public static Result<FirmCollaboratorYearCost> Create(
        Guid firmTenantId,
        Guid collaboratorUserId,
        int year)
    {
        if (firmTenantId == Guid.Empty || collaboratorUserId == Guid.Empty)
            return Result.Failure<FirmCollaboratorYearCost>(
                Error.Validation("Tenant", "Cabinet et collaborateur requis"));
        if (year is < 2000 or > 2100)
            return Result.Failure<FirmCollaboratorYearCost>(Error.Validation("Year", "Année invalide"));

        return Result.Success(new FirmCollaboratorYearCost
        {
            FirmTenantId = firmTenantId,
            CollaboratorUserId = collaboratorUserId,
            Year = year
        });
    }

    /// <summary>Enregistre un coût saisi par le cabinet.</summary>
    public Result SetManualCost(decimal grossAnnualSalary, decimal employerContributions, decimal payrollExtras)
    {
        var amounts = new[] { grossAnnualSalary, employerContributions, payrollExtras };
        if (amounts.Any(a => a < 0))
            return Result.Failure(Error.Validation("Payroll", "Les montants de paie ne peuvent pas être négatifs."));

        GrossAnnualSalary = MillimeRounding.Round(grossAnnualSalary);
        EmployerContributions = MillimeRounding.Round(employerContributions);
        PayrollExtras = MillimeRounding.Round(payrollExtras);
        Source = FirmPayrollCostSource.Manual;
        ImportedAt = null;
        return Result.Success();
    }

    /// <summary>Enregistre un coût agrégé depuis les bulletins du cabinet.</summary>
    public Result SetImportedCost(decimal grossAnnualSalary, decimal employerContributions, decimal payrollExtras)
    {
        var applied = SetManualCost(grossAnnualSalary, employerContributions, payrollExtras);
        if (applied.IsFailure)
            return applied;

        Source = FirmPayrollCostSource.ImportedFromPayroll;
        ImportedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Impose un taux horaire, en exigeant sa justification.</summary>
    public Result SetHourlyRateOverride(decimal? rate, string? justification)
    {
        if (rate is null)
        {
            HourlyRateOverride = null;
            OverrideJustification = null;
            return Result.Success();
        }

        if (rate <= 0)
            return Result.Failure(Error.Validation("HourlyRate", "Le taux horaire doit être strictement positif."));
        if (string.IsNullOrWhiteSpace(justification))
            return Result.Failure(Error.Validation(
                "Justification",
                "Un taux horaire imposé doit être justifié : sans motif, il n'est pas défendable en revue."));

        HourlyRateOverride = MillimeRounding.Round(rate.Value);
        OverrideJustification = justification.Trim();
        return Result.Success();
    }

    /// <summary>Charges patronales déduites d'un brut et d'un taux global, en %.</summary>
    public static decimal ComputeEmployerContributions(decimal grossAnnualSalary, decimal totalChargeRatePercent) =>
        grossAnnualSalary <= 0 || totalChargeRatePercent <= 0
            ? 0m
            : MillimeRounding.Round(grossAnnualSalary * totalChargeRatePercent / 100m);
}
