using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Valeurs par défaut des paramètres de paie tunisiens (barème IRPP progressif issu de la
/// loi de finances, taux CNSS/CSS/TFP/FOPROLOS). Utilisées pour seeder l'exercice courant ;
/// restent modifiables par l'utilisateur. Aucune valeur légale n'est codée ailleurs.
/// </summary>
public static class PayrollParameterDefaults
{
    /// <summary>Barème IRPP 2025/2026 (8 tranches, 0 % à 40 %).</summary>
    public static IReadOnlyList<(decimal LowerBound, decimal Rate)> Irpp2026Brackets =>
        PayrollLegalPresets.Presets[2026].IrppBrackets;

    /// <summary>Tranches de saisie sur salaire (convention tunisienne simplifiée).</summary>
    public static IReadOnlyList<(decimal LowerBoundMonthlyNet, decimal SeizableFraction)> Garnishment2026Brackets =>
        PayrollLegalPresets.Presets[2026].GarnishmentBrackets;

    /// <summary>
    /// Construit les paramètres par défaut d'un exercice donné à partir du preset légal correspondant.
    /// </summary>
    public static Result<PayrollYearParameters> CreateDefaults(int fiscalYear)
    {
        var preset = PayrollLegalPresets.Resolve(fiscalYear);
        return preset.Materialize(fiscalYear);
    }

    /// <summary>Retourne le preset légal officiel pour un exercice (lecture seule).</summary>
    public static PayrollLegalPreset GetPreset(int fiscalYear) =>
        PayrollLegalPresets.Resolve(fiscalYear);
}
