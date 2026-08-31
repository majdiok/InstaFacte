namespace FactuTrust.Domain.Enums;

/// <summary>
/// Profil d'imputation comptable de la paie. <c>Legacy</c> reproduit la cartographie historique
/// (TFP/FOPROLOS/CSS pat dans 647/432, indemnités dans 641…) ; <c>Sce2026</c> applique la
/// ventilation conforme au plan comptable SCE (TFP→6611, FOPROLOS→6612, taxes→437, indemnités de
/// rupture→64602, avantage en nature→6404, compensation AN→4386…). La sélection par cycle se fait
/// via <c>AccountingSettings.PayrollAccountProfileEffectiveDate</c> (cf. plan §5.3).
/// </summary>
public enum PayrollAccountProfile
{
    /// <summary>Cartographie historique — préservée à l'octet près pour les cycles antérieurs au bascule.</summary>
    Legacy = 0,
    /// <summary>Cartographie conforme au plan comptable tunisien SCE.</summary>
    Sce2026 = 1
}

/// <summary>
/// Assiette des taxes sur salaires (TFP, FOPROLOS, CSS patronale). L'assiette légale tunisienne
/// est le brut total de la rémunération ; le mode <c>Legacy</c> reproduit le comportement
/// historique (assiette CNSS plafonnée ou non selon <c>ApplyCnssCeilingToPayrollTaxes</c>).
/// </summary>
public enum PayrollTaxBaseMode
{
    /// <summary>Base historique : cnssableGross (avec ou sans plafond CNSS selon ApplyCnssCeilingToPayrollTaxes).</summary>
    Legacy = 0,
    /// <summary>Base légale : brut total de la rémunération (hors remboursements de frais purs — en attente de confirmation Q1 de l'assiette exacte).</summary>
    TotalGross = 1
}

public static class PayrollTaxBaseModeExtensions
{
    public static string ToDisplayString(this PayrollTaxBaseMode mode) => mode switch
    {
        PayrollTaxBaseMode.Legacy => "Base historique (CNSS)",
        PayrollTaxBaseMode.TotalGross => "Brut total (assiette légale)",
        _ => mode.ToString()
    };
}

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

/// <summary>Statut du versement CNSS mensuel.</summary>
public enum CnssRemittancePaymentStatus
{
    Pending = 0,
    Paid = 1,
    Cancelled = 2
}

public static class CnssRemittancePaymentStatusExtensions
{
    public static string ToDisplayString(this CnssRemittancePaymentStatus status) => status switch
    {
        CnssRemittancePaymentStatus.Pending => "À payer",
        CnssRemittancePaymentStatus.Paid => "Payé",
        CnssRemittancePaymentStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
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

    /// <summary>Vrai si ce type déclenche un calcul statutaire (maladie, maternité, paternité).</summary>
    public static bool AffectsPayrollComputation(this LeaveType type) =>
        type is LeaveType.Sick or LeaveType.Maternity or LeaveType.Paternity;

    /// <summary>Vrai si un certificat médical est requis pour ce type de congé.</summary>
    public static bool RequiresMedicalCertificate(this LeaveType type) =>
        type is LeaveType.Sick or LeaveType.Maternity;
}

/// <summary>Statut d'une créance IJ CNSS à récupérer.</summary>
public enum CnssIjClaimStatus
{
    /// <summary>Émise, en attente de règlement CNSS.</summary>
    Pending = 0,
    /// <summary>Réglée par la CNSS.</summary>
    Paid = 1,
    /// <summary>Rejetée par la CNSS.</summary>
    Rejected = 2
}

public static class CnssIjClaimStatusExtensions
{
    public static string ToDisplayString(this CnssIjClaimStatus status) => status switch
    {
        CnssIjClaimStatus.Pending => "En attente",
        CnssIjClaimStatus.Paid => "Réglée",
        CnssIjClaimStatus.Rejected => "Rejetée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}

/// <summary>Type de suspension de contrat (paie).</summary>
public enum PayrollSuspensionType
{
    /// <summary>Mise à pied disciplinaire.</summary>
    Disciplinary = 0,
    /// <summary>Suspension administrative.</summary>
    Administrative = 1,
    /// <summary>Autre motif.</summary>
    Other = 99
}

public static class PayrollSuspensionTypeExtensions
{
    public static string ToDisplayString(this PayrollSuspensionType type) => type switch
    {
        PayrollSuspensionType.Disciplinary => "Disciplinaire",
        PayrollSuspensionType.Administrative => "Administrative",
        PayrollSuspensionType.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

/// <summary>
/// Nature d'une retenue sur salaire (pour ventilation comptable et affichage bulletin).
/// </summary>
/// <summary>
/// Mode d'exemption/déduction IRPP pour les bas salaires (SMIG/SMAG). Aucune référence
/// légale vérifiable n'est citée ici : le libellé « art. 21 » précédent était non documenté.
/// Le mode par défaut (None) n'applique aucune exemption ; les autres modes correspondent à des
/// interprétations de conformité dont l'assiette et le plafond exacts doivent être validés
/// contre les textes en vigueur (LF / code de l'IRPP) avant usage en production.
/// </summary>
public enum SmigIrppExemptionMode
{
    /// <summary>Aucune exonération — comportement historique.</summary>
    None = 0,
    /// <summary>Portion du salaire imposable plafonnée au SMIG exonérée au taux applicable (base légale non vérifiée).</summary>
    SmigPortion = 1,
    /// <summary>IRPP nul si le salaire de base mensuel ne dépasse pas le SMIG.</summary>
    FullIfBelow = 2,
    /// <summary>Déduction annuelle de 500 TND pour SMIG/SMAG — forfait annuel proratisé au mois puis régularisé à l'année (LF 2019 interprétée).</summary>
    SmigAnnualDeduction = 3
}

public static class SmigIrppExemptionModeExtensions
{
    public static string ToDisplayString(this SmigIrppExemptionMode mode) => mode switch
    {
        SmigIrppExemptionMode.None => "Désactivée",
        SmigIrppExemptionMode.SmigPortion => "Portion SMIG exonérée",
        SmigIrppExemptionMode.FullIfBelow => "Exonération totale si salaire ≤ SMIG",
        SmigIrppExemptionMode.SmigAnnualDeduction => "Déduction annuelle 500 TND (SMIG/SMAG)",
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

/// <summary>Nature d'une ligne de gain (earning) sur le bulletin de paie, utilisée pour l'imputation comptable SCE.</summary>
public enum EarningKind
{
    Salary = 0,
    Overtime = 1,
    Bonus = 2,
    OrdinaryAllowance = 3,
    InKindBenefit = 4,
    TerminationIndemnity = 5,
    Other = 99
}

public static class EarningKindExtensions
{
    public static string ToDisplayString(this EarningKind kind) => kind switch
    {
        EarningKind.Salary => "Salaire",
        EarningKind.Overtime => "Heures supplémentaires",
        EarningKind.Bonus => "Prime",
        EarningKind.OrdinaryAllowance => "Indemnité ordinaire",
        EarningKind.InKindBenefit => "Avantage en nature",
        EarningKind.TerminationIndemnity => "Indemnité de rupture",
        EarningKind.Other => "Autre gain",
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

/// <summary>Lien de parenté pour une déclaration de parent à charge (art. 40 IRPP).</summary>
public enum DependentParentKinship
{
    Father = 0,
    Mother = 1
}

public static class DependentParentKinshipExtensions
{
    public static string ToDisplayString(this DependentParentKinship kinship) => kinship switch
    {
        DependentParentKinship.Father => "Père",
        DependentParentKinship.Mother => "Mère",
        _ => throw new ArgumentOutOfRangeException(nameof(kinship))
    };
}

/// <summary>Type de jour férié tunisien (calendrier civil ou islamique).</summary>
public enum PublicHolidayKind
{
    /// <summary>Fête à date fixe (calendrier grégorien).</summary>
    Fixed = 0,
    /// <summary>Fête islamique (date lunaire, souvent estimée).</summary>
    Islamic = 1
}

public static class PublicHolidayKindExtensions
{
    public static string ToDisplayString(this PublicHolidayKind kind) => kind switch
    {
        PublicHolidayKind.Fixed => "Fixe",
        PublicHolidayKind.Islamic => "Islamique",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>État des déclarations de parents à charge d'un salarié.</summary>
public enum ParentClaimsStatus
{
    /// <summary>Aucun parent déclaré et aucun compteur legacy.</summary>
    None = 0,
    /// <summary>Déclarations nominatives (CIN) complètes et sans conflit.</summary>
    Complete = 1,
    /// <summary>Compteur legacy &gt; 0 sans CIN — déduction non applicable jusqu'à saisie.</summary>
    Incomplete = 2,
    /// <summary>Le même CIN parent est déclaré par un autre salarié de l'entreprise.</summary>
    Conflict = 3
}

/// <summary>Motif de rupture du contrat de travail.</summary>
public enum TerminationReason
{
    Dismissal = 0,
    Resignation = 1,
    Retirement = 2,
    MutualAgreement = 3,
    GrossMisconduct = 4,
    EndOfCdd = 5,
    EndOfSivp = 6,
    Death = 7
}

public static class TerminationReasonExtensions
{
    public static string ToDisplayString(this TerminationReason reason) => reason switch
    {
        TerminationReason.Dismissal => "Licenciement",
        TerminationReason.Resignation => "Démission",
        TerminationReason.Retirement => "Retraite",
        TerminationReason.MutualAgreement => "Rupture conventionnelle",
        TerminationReason.GrossMisconduct => "Faute grave",
        TerminationReason.EndOfCdd => "Fin de CDD",
        TerminationReason.EndOfSivp => "Fin de SIVP",
        TerminationReason.Death => "Décès",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };

    /// <summary>Vrai si l'indemnité légale art. 22bis CDT peut s'appliquer.</summary>
    public static bool IsEligibleForLegalIndemnity(this TerminationReason reason) =>
        reason is TerminationReason.Dismissal or TerminationReason.MutualAgreement or TerminationReason.EndOfCdd;
}

/// <summary>Statut du workflow d'un solde de tout compte / rupture.</summary>
public enum TerminationSettlementStatus
{
    Draft = 0,
    Calculated = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4
}

public static class TerminationSettlementStatusExtensions
{
    public static string ToDisplayString(this TerminationSettlementStatus status) => status switch
    {
        TerminationSettlementStatus.Draft => "Brouillon",
        TerminationSettlementStatus.Calculated => "Calculé",
        TerminationSettlementStatus.Approved => "Approuvé",
        TerminationSettlementStatus.Paid => "Payé",
        TerminationSettlementStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static bool CanBeEdited(this TerminationSettlementStatus status) =>
        status is TerminationSettlementStatus.Draft or TerminationSettlementStatus.Calculated;
}

/// <summary>Nature d'une prime annuelle paramétrable.</summary>
public enum AnnualBonusKind
{
    ThirteenthMonth = 0,
    Seniority = 1,
    Vacation = 2,
    Other = 99
}

public static class AnnualBonusKindExtensions
{
    public static string ToDisplayString(this AnnualBonusKind kind) => kind switch
    {
        AnnualBonusKind.ThirteenthMonth => "13e mois",
        AnnualBonusKind.Seniority => "Prime d'ancienneté",
        AnnualBonusKind.Vacation => "Prime de vacances",
        AnnualBonusKind.Other => "Autre prime annuelle",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>Formule de calcul d'une prime annuelle.</summary>
public enum AnnualBonusFormula
{
    FixedAmount = 0,
    PercentOfBase = 1,
    MonthsOfBase = 2
}

public static class AnnualBonusFormulaExtensions
{
    public static string ToDisplayString(this AnnualBonusFormula formula) => formula switch
    {
        AnnualBonusFormula.FixedAmount => "Montant fixe",
        AnnualBonusFormula.PercentOfBase => "% du salaire de base",
        AnnualBonusFormula.MonthsOfBase => "Mois de salaire de base",
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
}

/// <summary>Origine d'une ligne de prime variable mensuelle.</summary>
public enum VariableAllowanceSource
{
    Manual = 0,
    AutoAnnualBonus = 1
}
