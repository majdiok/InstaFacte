namespace FactuTrust.Domain.Enums;

/// <summary>
/// Type de contrat de travail (Tunisie).
/// </summary>
public enum ContractType
{
    /// <summary>Contrat à durée indéterminée.</summary>
    Cdi = 0,
    /// <summary>Contrat à durée déterminée.</summary>
    Cdd = 1,
    /// <summary>Stage d'initiation à la vie professionnelle (SIVP).</summary>
    Sivp = 2,
    /// <summary>Contrat Karama / dispositif d'emploi aidé.</summary>
    Karama = 3,
    /// <summary>Stage / apprentissage.</summary>
    Internship = 4,
    /// <summary>Autre type de contrat.</summary>
    Other = 99
}

public static class ContractTypeExtensions
{
    public static string ToDisplayString(this ContractType type) => type switch
    {
        ContractType.Cdi => "CDI",
        ContractType.Cdd => "CDD",
        ContractType.Sivp => "SIVP",
        ContractType.Karama => "Karama",
        ContractType.Internship => "Stage",
        ContractType.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

/// <summary>
/// Régime de sécurité sociale du salarié.
/// </summary>
public enum SocialRegime
{
    /// <summary>Régime des salariés non agricoles (secteur privé) — CNSS.</summary>
    Rsna = 0,
    /// <summary>SIVP / CIVP : exonéré de cotisations CNSS salariales et patronales.</summary>
    SivpExonere = 1,
    /// <summary>Régime des salariés agricoles.</summary>
    Rsa = 2,
    /// <summary>Aucun régime (non affilié).</summary>
    None = 99
}

public static class SocialRegimeExtensions
{
    public static string ToDisplayString(this SocialRegime regime) => regime switch
    {
        SocialRegime.Rsna => "RSNA (salariés non agricoles)",
        SocialRegime.SivpExonere => "SIVP / CIVP (exonéré)",
        SocialRegime.Rsa => "RSA (salariés agricoles)",
        SocialRegime.None => "Non affilié",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };

    /// <summary>Vrai si le régime donne lieu à cotisations CNSS (salariale et patronale).</summary>
    public static bool IsSubjectToCnss(this SocialRegime regime) =>
        regime is SocialRegime.Rsna or SocialRegime.Rsa;
}

/// <summary>
/// Statut du cycle de paie mensuel.
/// </summary>
public enum PayrollRunStatus
{
    /// <summary>Brouillon : le cycle est en cours de préparation.</summary>
    Draft = 0,
    /// <summary>Calculé : les bulletins ont été générés mais pas encore validés.</summary>
    Calculated = 1,
    /// <summary>Validé : la paie est arrêtée, écritures comptables générées.</summary>
    Validated = 2,
    /// <summary>Clôturé : la période est verrouillée définitivement.</summary>
    Closed = 3
}

public static class PayrollRunStatusExtensions
{
    public static string ToDisplayString(this PayrollRunStatus status) => status switch
    {
        PayrollRunStatus.Draft => "Brouillon",
        PayrollRunStatus.Calculated => "Calculé",
        PayrollRunStatus.Validated => "Validé",
        PayrollRunStatus.Closed => "Clôturé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    /// <summary>Le cycle peut-il encore être recalculé / modifié ?</summary>
    public static bool CanBeEdited(this PayrollRunStatus status) =>
        status is PayrollRunStatus.Draft or PayrollRunStatus.Calculated;
}

/// <summary>
/// Nature d'une ligne de bulletin de paie.
/// </summary>
public enum PayslipLineKind
{
    /// <summary>Gain (salaire de base, prime, heures supplémentaires…).</summary>
    Earning = 0,
    /// <summary>Retenue salariale (CNSS, IRPP, CSS, avance…).</summary>
    Deduction = 1,
    /// <summary>Charge patronale (hors net à payer, pour comptabilité et déclarations).</summary>
    EmployerContribution = 2,
    /// <summary>Ligne informative (base, cumul…).</summary>
    Info = 3
}

public static class PayslipLineKindExtensions
{
    public static string ToDisplayString(this PayslipLineKind kind) => kind switch
    {
        PayslipLineKind.Earning => "Gain",
        PayslipLineKind.Deduction => "Retenue",
        PayslipLineKind.EmployerContribution => "Charge patronale",
        PayslipLineKind.Info => "Information",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>
/// Situation familiale du salarié.
/// </summary>
public enum MaritalStatus
{
    Single = 0,
    Married = 1,
    Divorced = 2,
    Widowed = 3
}

public static class MaritalStatusExtensions
{
    public static string ToDisplayString(this MaritalStatus status) => status switch
    {
        MaritalStatus.Single => "Célibataire",
        MaritalStatus.Married => "Marié(e)",
        MaritalStatus.Divorced => "Divorcé(e)",
        MaritalStatus.Widowed => "Veuf(ve)",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}

/// <summary>
/// Type de congé / absence.
/// </summary>
public enum LeaveType
{
    /// <summary>Congé payé (n'impacte pas le brut).</summary>
    Paid = 0,
    /// <summary>Congé sans solde (déduit du brut).</summary>
    Unpaid = 1,
    /// <summary>Congé maladie.</summary>
    Sick = 2,
    /// <summary>Congé de maternité.</summary>
    Maternity = 3,
    /// <summary>Absence non justifiée (déduite du brut).</summary>
    Unjustified = 4,
    /// <summary>Congé paternité.</summary>
    Paternity = 5,
    /// <summary>Congé décès (proche).</summary>
    Bereavement = 6,
    /// <summary>Récupération / repos compensateur.</summary>
    Recovery = 7,
    /// <summary>Autre.</summary>
    Other = 99
}

public static class LeaveTypeExtensions
{
    public static string ToDisplayString(this LeaveType type) => type switch
    {
        LeaveType.Paid => "Congé payé",
        LeaveType.Unpaid => "Congé sans solde",
        LeaveType.Sick => "Congé maladie",
        LeaveType.Maternity => "Congé maternité",
        LeaveType.Unjustified => "Absence non justifiée",
        LeaveType.Paternity => "Congé paternité",
        LeaveType.Bereavement => "Congé décès",
        LeaveType.Recovery => "Récupération",
        LeaveType.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>Vrai si ce type de congé/absence réduit le brut du mois.</summary>
    public static bool ReducesGross(this LeaveType type) =>
        type is LeaveType.Unpaid or LeaveType.Unjustified;
}
