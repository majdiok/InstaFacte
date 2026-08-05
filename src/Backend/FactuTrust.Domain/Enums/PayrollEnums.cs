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

/// <summary>Statut de paiement d'un bulletin de paie.</summary>
public enum PayslipPaymentStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2
}

/// <summary>Statut de paiement global d'un cycle de paie.</summary>
public enum PayrollRunPaymentStatus
{
    NotPaid = 0,
    PartiallyPaid = 1,
    FullyPaid = 2
}

public static class PayslipPaymentStatusExtensions
{
    public static string ToDisplayString(this PayslipPaymentStatus status) => status switch
    {
        PayslipPaymentStatus.Unpaid => "Non payé",
        PayslipPaymentStatus.PartiallyPaid => "Partiellement payé",
        PayslipPaymentStatus.Paid => "Payé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}

public static class PayrollRunPaymentStatusExtensions
{
    public static string ToDisplayString(this PayrollRunPaymentStatus status) => status switch
    {
        PayrollRunPaymentStatus.NotPaid => "Non payé",
        PayrollRunPaymentStatus.PartiallyPaid => "Partiellement payé",
        PayrollRunPaymentStatus.FullyPaid => "Entièrement payé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
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

/// <summary>
/// Nature d'une retenue sur salaire (pour ventilation comptable et affichage bulletin).
/// </summary>
/// <summary>
/// Mode d'exonération IRPP SMIG (article 21 du code de l'IRPP, LF 2019).
/// </summary>
public enum SmigIrppExemptionMode
{
    /// <summary>Aucune exonération — comportement historique.</summary>
    None = 0,
    /// <summary>Portion du salaire imposable plafonnée au SMIG exonérée au taux applicable.</summary>
    SmigPortion = 1,
    /// <summary>IRPP nul si le salaire de base mensuel ne dépasse pas le SMIG.</summary>
    FullIfBelow = 2
}

public static class SmigIrppExemptionModeExtensions
{
    public static string ToDisplayString(this SmigIrppExemptionMode mode) => mode switch
    {
        SmigIrppExemptionMode.None => "Désactivée",
        SmigIrppExemptionMode.SmigPortion => "Portion SMIG exonérée (art. 21)",
        SmigIrppExemptionMode.FullIfBelow => "Exonération totale si salaire ≤ SMIG",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

/// <summary>
/// Motif déclenchant une régularisation IRPP/CSS annuelle.
/// </summary>
public enum IrppRegularizationReason
{
    /// <summary>Régularisation de fin d'exercice, portée sur le cycle de décembre.</summary>
    YearEnd = 0,
    /// <summary>Solde de tout compte : le contrat s'achève dans le mois du cycle.</summary>
    FinalSettlement = 1,
    /// <summary>Saisie manuelle par le gestionnaire de paie.</summary>
    Manual = 2
}

public static class IrppRegularizationReasonExtensions
{
    public static string ToDisplayString(this IrppRegularizationReason reason) => reason switch
    {
        IrppRegularizationReason.YearEnd => "Régularisation annuelle (décembre)",
        IrppRegularizationReason.FinalSettlement => "Solde de tout compte",
        IrppRegularizationReason.Manual => "Saisie manuelle",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };
}

public enum DeductionKind
{
    Advance = 0,
    Loan = 1,
    Garnishment = 2,
    Alimony = 3,
    MutuelleEmployee = 4,
    MealVoucherEmployeeShare = 5,
    InKindBenefitOffset = 6,
    Other = 99
}

public static class DeductionKindExtensions
{
    public static string ToDisplayString(this DeductionKind kind) => kind switch
    {
        DeductionKind.Advance => "Avance sur salaire",
        DeductionKind.Loan => "Remboursement prêt",
        DeductionKind.Garnishment => "Saisie sur salaire",
        DeductionKind.Alimony => "Pension alimentaire",
        DeductionKind.MutuelleEmployee => "Mutuelle / caisse complémentaire",
        DeductionKind.MealVoucherEmployeeShare => "Tickets restaurant (part employée)",
        DeductionKind.InKindBenefitOffset => "Compensation avantage en nature",
        DeductionKind.Other => "Autre retenue",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>Type d'avantage en nature.</summary>
public enum BenefitInKindType
{
    CompanyVehicle = 0,
    Housing = 1,
    Other = 99
}

public static class BenefitInKindTypeExtensions
{
    public static string ToDisplayString(this BenefitInKindType type) => type switch
    {
        BenefitInKindType.CompanyVehicle => "Véhicule de fonction",
        BenefitInKindType.Housing => "Logement de fonction",
        BenefitInKindType.Other => "Autre avantage en nature",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

/// <summary>Base de calcul d'une caisse / mutuelle complémentaire.</summary>
public enum SocialFundBase
{
    GrossCnssable = 0,
    NetTaxable = 1,
    FixedAmount = 2
}

/// <summary>Statut d'un prêt salarié.</summary>
public enum EmployeeLoanStatus
{
    Active = 0,
    FullyRepaid = 1,
    Cancelled = 2
}

public static class EmployeeLoanStatusExtensions
{
    public static string ToDisplayString(this EmployeeLoanStatus status) => status switch
    {
        EmployeeLoanStatus.Active => "En cours",
        EmployeeLoanStatus.FullyRepaid => "Soldé",
        EmployeeLoanStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}

/// <summary>Type de saisie sur salaire.</summary>
public enum GarnishmentType
{
    Garnishment = 0,
    Alimony = 1
}

/// <summary>Mode de calcul d'une saisie.</summary>
public enum GarnishmentAmountKind
{
    FixedAmount = 0,
    PercentOfNet = 1
}

/// <summary>Statut d'une saisie sur salaire.</summary>
public enum EmployeeGarnishmentStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2
}

public static class EmployeeGarnishmentStatusExtensions
{
    public static string ToDisplayString(this EmployeeGarnishmentStatus status) => status switch
    {
        EmployeeGarnishmentStatus.Active => "Active",
        EmployeeGarnishmentStatus.Completed => "Terminée",
        EmployeeGarnishmentStatus.Cancelled => "Annulée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
