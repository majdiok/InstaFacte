using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Variables d'entrée d'un calcul de bulletin pour un salarié sur un mois donné.
/// </summary>
public sealed class PayrollComputationInput
{
    /// <summary>Salaire de base mensuel brut.</summary>
    public decimal BaseSalary { get; init; }
    /// <summary>Primes/indemnités imposables et soumises à CNSS.</summary>
    public decimal TaxableCnssableAllowances { get; init; }
    /// <summary>Primes imposables mais non soumises à CNSS (matrice 4 quadrants).</summary>
    public decimal TaxableOnlyAllowances { get; init; }
    /// <summary>Indemnités soumises à CNSS mais non imposables (matrice 4 quadrants).</summary>
    public decimal CnssOnlyAllowances { get; init; }
    /// <summary>Indemnités non imposables et non soumises à CNSS (ajoutées au net).</summary>
    public decimal NonTaxableAllowances { get; init; }
    /// <summary>Montant des heures supplémentaires (imposable et soumis à CNSS).</summary>
    public decimal OvertimeAmount { get; init; }
    /// <summary>Retenue pour absences non rémunérées (réduit le brut).</summary>
    public decimal UnpaidAbsenceAmount { get; init; }
    /// <summary>Retenue carence congé maladie (réduit le brut).</summary>
    public decimal SickLeaveDeductionAmount { get; init; }
    /// <summary>Complément employeur congé maladie (indemnité non imposable).</summary>
    public decimal SickLeaveTopUpAmount { get; init; }
    /// <summary>Avance subrogation IJ CNSS versée au salarié (non imposable).</summary>
    public decimal SickLeaveSubrogationAmount { get; init; }
    /// <summary>Complément employeur congé maternité (indemnité non imposable).</summary>
    public decimal MaternityTopUpAmount { get; init; }
    /// <summary>Maintien intégral congé paternité (indemnité non imposable).</summary>
    public decimal PaternityMaintenanceAmount { get; init; }
    /// <summary>Retenue prorata embauche / départ / suspension (réduit le brut, base seule).</summary>
    public decimal ProrataDeductionAmount { get; init; }
    /// <summary>Jours travaillés retenus pour le prorata (convention 26 jours).</summary>
    public decimal ProrataWorkedDays { get; init; }
    /// <summary>Jours non rémunérés retenus pour le prorata.</summary>
    public decimal ProrataNonWorkedDays { get; init; }
    /// <summary>Autres retenues (avances, oppositions) déduites du net.</summary>
    public decimal OtherDeductions { get; init; }

    /// <summary>
    /// Régularisation IRPP annuelle (signée) : positive pour un rappel à prélever, négative
    /// pour une restitution à reverser. Zéro hors mois de régularisation — le calcul mensuel
    /// est alors strictement identique au comportement historique.
    /// </summary>
    public decimal IrppRegularization { get; init; }
    /// <summary>Régularisation CSS annuelle (signée), même convention que <see cref="IrppRegularization"/>.</summary>
    public decimal CssRegularization { get; init; }

    public SocialRegime Regime { get; init; } = SocialRegime.Rsna;
    /// <summary>Taux d'accident de travail (charge patronale), en %.</summary>
    public decimal WorkAccidentRate { get; init; }
    /// <summary>Vrai si l'entreprise relève du secteur industriel (taux TFP réduit).</summary>
    public bool IsIndustrialSector { get; init; }

    public bool IsHeadOfFamily { get; init; }
    /// <summary>Nombre total d'enfants à charge.</summary>
    public int DependentChildren { get; init; }
    /// <summary>Dont enfants étudiants non boursiers de moins de 25 ans (déduction majorée).</summary>
    public int StudentChildren { get; init; }
    /// <summary>Dont enfants infirmes (déduction majorée, hors plafond de rang).</summary>
    public int DisabledChildren { get; init; }
    /// <summary>Parents à charge (0 à 2).</summary>
    public int DependentParents { get; init; }

    /// <summary>
    /// Détail des primes/indemnités pour l'affichage bulletin (libellés individuels).
    /// N'affecte pas le calcul si les buckets agrégés sont déjà renseignés.
    /// </summary>
    public IReadOnlyList<AllowanceLineInput> AllowanceLines { get; init; } = Array.Empty<AllowanceLineInput>();

    /// <summary>
    /// Retenues typées déduites du net (avances, prêts, mutuelle, tickets restaurant…).
    /// Si renseigné, <see cref="OtherDeductions"/> doit refléter la somme de ces lignes.
    /// </summary>
    public IReadOnlyCollection<DeductionLineInput> DeductionLines { get; init; } = Array.Empty<DeductionLineInput>();

    /// <summary>
    /// Retenues post-impôt (saisies, pensions alimentaires) déduites après IRPP/CSS.
    /// </summary>
    public IReadOnlyCollection<DeductionLineInput> PostTaxDeductionLines { get; init; } = Array.Empty<DeductionLineInput>();

    /// <summary>
    /// Avantages en nature imposables et soumis à CNSS (véhicule, logement…).
    /// Ajoutés au brut imposable/CNSSable mais non versés en cash.
    /// </summary>
    public decimal InKindTaxableCnssableBenefits { get; init; }

    /// <summary>Charges patronales complémentaires (ex. part employeur mutuelle).</summary>
    public IReadOnlyCollection<EmployerChargeLineInput> EmployerChargeLines { get; init; } = Array.Empty<EmployerChargeLineInput>();
}

/// <summary>
/// Résultat détaillé d'un calcul de bulletin.
/// </summary>
public sealed class PayrollComputation
{
    /// <summary>Brut total (inclut les indemnités non imposables).</summary>
    public decimal GrossSalary { get; init; }
    /// <summary>Brut soumis à cotisation CNSS et à l'impôt.</summary>
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal TaxableBaseAfterCnss { get; init; }
    public decimal ProfessionalExpenses { get; init; }
    public decimal FamilyDeductions { get; init; }
    public decimal MonthlyNetTaxable { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    public decimal Css { get; init; }
    /// <summary>IRPP brut avant exonération/déduction SMIG.</summary>
    public decimal IrppBeforeSmigExemption { get; init; }
    /// <summary>Montant de l'exonération IRPP SMIG appliquée (mode SmigPortion / FullIfBelow).</summary>
    public decimal IrppSmigExemption { get; init; }
    /// <summary>
    /// Forfait mensuel de la déduction annuelle SMIG (500 TND/an ÷ 12) appliqué sur la base
    /// imposable (mode <c>SmigAnnualDeduction</c>, R-12). Nul hors de ce mode. Traçable jusqu'au
    /// bulletin pour audit de conformité.
    /// </summary>
    public decimal SmigAnnualDeductionAmount { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }

    /// <summary>Régularisation IRPP effectivement appliquée au net (signée, après écrêtage).</summary>
    public decimal IrppRegularization { get; init; }
    /// <summary>Régularisation CSS effectivement appliquée au net (signée, après écrêtage).</summary>
    public decimal CssRegularization { get; init; }
    /// <summary>Part du rappel non prélevée faute de net suffisant (toujours positive ou nulle).</summary>
    public decimal RegularizationDeferred { get; init; }
    /// <summary>Vrai si le rappel a dû être écrêté au net disponible.</summary>
    public bool IsRegularizationCapped { get; init; }

    /// <summary>
    /// R-22 : vrai si au moins une retenue (pré- ou post-impôt) n'a pas pu être prélevée
    /// intégralement faute de net suffisant et a été partiellement reportée au mois suivant.
    /// </summary>
    public bool HasPartialDeductions { get; init; }
    /// <summary>R-22 : total des retenues reportées au mois suivant (pré- + post-impôt).</summary>
    public decimal PartialDeductionCarryOver { get; init; }

    public decimal NetSalary { get; init; }

    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }
    public decimal CssEmployer { get; init; }

    /// <summary>
    /// Assiette des taxes sur salaires (TFP, FOPROLOS, CSS patronale). Distincte de la base CNSS
    /// dès lors qu'un plafond CNSS est paramétré : ces taxes n'ont pas de plafond légal.
    /// </summary>
    public decimal PayrollTaxBase { get; init; }

    /// <summary>Taux de TFP retenu (%), figé pour la traçabilité et la déclaration mensuelle.</summary>
    public decimal AppliedTfpRate { get; init; }

    /// <summary>Total des charges patronales.</summary>
    public decimal TotalEmployerCharges => CnssEmployer + WorkAccidentContribution + Tfp + Foprolos + CssEmployer;

    public IReadOnlyList<PayrollComputationLine> Lines { get; init; } = Array.Empty<PayrollComputationLine>();
}

/// <summary>
/// Ligne détaillée produite par le calcul (reprise telle quelle dans le bulletin).
/// </summary>
public sealed class PayrollComputationLine
{
    public int Order { get; init; }
    public string Label { get; init; } = string.Empty;
    public PayslipLineKind Kind { get; init; }
    public decimal? Base { get; init; }
    public decimal? Rate { get; init; }
    public decimal Amount { get; init; }
    public DeductionKind? DeductionKind { get; init; }
    /// <summary>Nature du gain, pour ventilation comptable SCE (indemnités de rupture, avantage en nature…).</summary>
    public EarningKind? EarningKind { get; init; }
    /// <summary>Compte SCE figé (ex. compte du régime de fonds social). Null si non applicable.</summary>
    public string? AccountSce { get; init; }
    /// <summary>Identifiant de l'entité source (avance, prêt, saisie, enrôlement…) figé au calcul.</summary>
    public Guid? SourceEntityId { get; init; }
    /// <summary>Montant initialement demandé (saisies). Null si non applicable.</summary>
    public decimal? RequestedAmount { get; init; }
    /// <summary>Solde reporté au mois suivant (saisies). Null si non applicable.</summary>
    public decimal? CarriedOverAmount { get; init; }
}

/// <summary>Ligne de retenue typée en entrée du calculateur.</summary>
public sealed record DeductionLineInput(
    string Label,
    decimal Amount,
    DeductionKind Kind,
    Guid? SourceEntityId = null,
    decimal? RequestedAmount = null,
    decimal? CarriedOverAmount = null,
    /// <summary>Compte SCE du régime figé sur la ligne (mutuelle part salarié — R-14).
    /// Null → le builder utilise le compte paramétré par défaut (ex. 428.1).</summary>
    string? AccountSce = null);

/// <summary>Charge patronale complémentaire (mutuelle employeur…).</summary>
public sealed record EmployerChargeLineInput(
    string Label,
    decimal Amount,
    string AccountSce);
