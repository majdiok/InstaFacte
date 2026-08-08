namespace FactuTrust.Application.Configuration;

/// <summary>
/// Interrupteurs (feature flags) du programme « cycle de révision & dépôt légal » du module
/// comptabilité. Tous <c>false</c> par défaut : le comportement historique est strictement préservé
/// tant qu'un dossier ne les active pas explicitement (activation progressive, réversible sans
/// redéploiement). Cf. le plan de non-régression.
/// </summary>
public sealed class AccountingSettings
{
    public const string SectionName = "Accounting";

    /// <summary>
    /// Active le workflow brouillard/validation : les écritures (auto + manuelles) sont créées en
    /// brouillon et doivent être validées. Quand false, toute écriture naît <c>Validee</c>
    /// (comportement historique, aucun impact sur les états).
    /// </summary>
    public bool BrouillardEnabled { get; set; }

    /// <summary>
    /// Politique d'inclusion des écritures en brouillard dans les états de consultation
    /// (journal, grand livre, balance). true = incluses avec marqueur (comportement Sage) ;
    /// les sorties légales (FEC, clôture, déclarations, liasse) opèrent toujours sur le validé.
    /// </summary>
    public bool IncludeBrouillardInReports { get; set; } = true;

    /// <summary>Active l'extourne (contre-passation) manuelle d'une écriture validée depuis l'UI.</summary>
    public bool ManualReversalEnabled { get; set; }

    /// <summary>Active la reprise de dossier par import (Excel/CSV + FEC) et l'écran associé.</summary>
    public bool DossierImportEnabled { get; set; }

    /// <summary>
    /// Active la déclaration mensuelle unique complète (TVA + RS + TCL + TFP/FOPROLOS + timbre +
    /// acomptes + rectificative). Quand false, la déclaration TVA historique (19/13/7) est servie.
    /// </summary>
    public bool MonthlyDeclarationV2Enabled { get; set; }

    /// <summary>
    /// Active l'export de la déclaration mensuelle au format du <b>formulaire officiel DGI</b>
    /// (tamponnage du gabarit arabe préimprimé « التصريح الشهري بالأداءات »). Quand false, seul le
    /// PDF de synthèse historique est exposé — le bouton correspondant est masqué et l'endpoint
    /// refuse la demande. Sans effet sur le calcul de la déclaration.
    /// </summary>
    public bool MonthlyDeclarationOfficialFormEnabled { get; set; }

    /// <summary>
    /// Active les états financiers au format NCT (bilan/résultat structurés + flux + variation des
    /// capitaux propres + notes). Quand false, seuls les états historiques sont exposés.
    /// </summary>
    public bool NctStatementsEnabled { get; set; }

    /// <summary>
    /// Active la partie fiscale de la liasse (détermination du résultat fiscal, calcul IS/IRPP,
    /// export consolidé). Défaut true — mettre à false pour masquer la fonctionnalité.
    /// </summary>
    public bool FiscalLiasseEnabled { get; set; } = true;

    /// <summary>
    /// Taux du FODEC (%) appliqué à la base HT de la période pour préremplir la déclaration mensuelle.
    /// Standard tunisien : 1 %. Mis à 0 pour désactiver le préremplissage FODEC.
    /// </summary>
    public decimal FodecRatePercent { get; set; } = 1.0m;

    /// <summary>
    /// Taux de la TCL (%) appliqué au chiffre d'affaires TTC de la période. Standard tunisien : 0,2 %.
    /// Mis à 0 pour désactiver le préremplissage TCL.
    /// </summary>
    public decimal TclRatePercent { get; set; } = 0.2m;

    /// <summary>
    /// Active l'outil de remplacement de compte (remplace un compte par un autre sur les lignes
    /// d'écriture des périodes ouvertes). OFF par défaut (opération de maintenance sensible).
    /// </summary>
    public bool AccountReplacementEnabled { get; set; }

    /// <summary>
    /// Active le catalogue de journaux modifiable (création/édition de journaux et familles). OFF par
    /// défaut : la liste standard figée (JV/JA/JC/JB/JOD/JIM/JAN) reste utilisée.
    /// </summary>
    public bool JournalCatalogEnabled { get; set; }

    /// <summary>Active l'import PDF/OCR de relevés bancaires dans le rapprochement.</summary>
    public bool BankStatementPdfImportEnabled { get; set; } = true;

    /// <summary>
    /// Active l'import d'une facture (vente ou achat) depuis la saisie manuelle d'écritures :
    /// détection des zones de la pièce puis proposition de l'écriture selon le plan comptable
    /// tunisien. L'endpoint est en lecture seule — il ne fait que proposer.
    /// </summary>
    public bool DocumentImportEnabled { get; set; } = true;

    /// <summary>
    /// Fenêtre de dates (± jours) utilisée par l'association automatique du rapprochement bancaire
    /// pour apparier une ligne de relevé à une écriture de même montant. Défaut : 10 jours.
    /// </summary>
    public int BankMatchWindowDays { get; set; } = 10;

    /// <summary>
    /// Active la checklist des contrôles de pré-clôture (brouillons, comptes d'attente non soldés,
    /// lettrage en suspens, dotations non passées, TVA compta vs déclarée…) et le blocage de la
    /// clôture annuelle / du verrouillage définitif sur les contrôles bloquants.
    /// </summary>
    public bool PreClosingControlsEnabled { get; set; }

    /// <summary>
    /// Seuil d'ancienneté (jours) au-delà duquel une ligne de tiers non lettrée est signalée par les
    /// contrôles de pré-clôture. Défaut : 90 jours.
    /// </summary>
    public int UnletteredAgeThresholdDays { get; set; } = 90;

    /// <summary>Active l'assistant d'écritures d'inventaire (CCA/PCA, charges à payer, provisions…).</summary>
    public bool InventoryAssistantEnabled { get; set; }

    /// <summary>
    /// Active le verrouillage définitif (irréversible) d'un exercice : bloque la réouverture des
    /// périodes et toute création d'écriture sur l'exercice verrouillé.
    /// </summary>
    public bool DefinitiveLockEnabled { get; set; }

    /// <summary>
    /// Active la comptabilité budgétaire : postes budgétaires, budgets annuels mensualisés
    /// (versions Initial/Révisé) et état budget vs réalisé. OFF = feature totalement inerte.
    /// </summary>
    public bool BudgetingEnabled { get; set; }

    /// <summary>
    /// Active l'envoi automatique quotidien de rappels e-mail aux responsables des échéances
    /// fiscales (J-7/J-1 et relance hebdomadaire en retard). OFF = aucun envoi (le rappel
    /// manuel de l'écran reste disponible).
    /// </summary>
    public bool FiscalEmailRemindersEnabled { get; set; }

    /// <summary>
    /// Jours d'anticipation déclenchant un rappel avant l'échéance (défaut : J-7 et J-1).
    /// </summary>
    public int[] FiscalReminderLeadDays { get; set; } = [7, 1];

    /// <summary>
    /// Synchronise l'échéancier fiscal depuis la déclaration mensuelle : à chaque
    /// enregistrement/soumission, l'échéance « Déclaration mensuelle » de la période est liée
    /// (SourceId), son montant estimé aligné sur le total à payer, et la soumission la marque
    /// « Déposée » (créée automatiquement si absente). OFF = aucun couplage (comportement historique).
    /// </summary>
    public bool DeclarationScheduleSyncEnabled { get; set; }

    /// <summary>
    /// Active le mode de paiement « traite / effet de commerce » : exposition de l'option à
    /// l'encaissement (ventes &amp; achats), saisie de l'échéance, écritures comptables d'effets
    /// (réception 412/403, puis encaissement/paiement à échéance 532/412 ou 403/532) et l'action
    /// « Encaisser l'effet ». OFF = l'option traite est refusée à l'enregistrement d'un paiement et
    /// les endpoints de règlement d'effet sont inertes (comportement historique intégralement préservé).
    /// </summary>
    public bool EffetDeCommerceEnabled { get; set; }

    /// <summary>
    /// Active le plan tiers unifié : répertoire clients+fournisseurs avec fiche comptable
    /// (code auxiliaire, compte collectif, délai de règlement), génération des codes auxiliaires
    /// et export FEC avec CompAuxNum lisible. OFF = feature inerte (comportement historique).
    /// </summary>
    public bool ThirdPartyDirectoryEnabled { get; set; }

    /// <summary>
    /// Active le centre de contrôle d'intégrité comptable (lecture seule) : diagnostics à la demande
    /// hors clôture — contrôles de pré-clôture partagés + détection d'anomalies structurelles
    /// (comptes hors plan, écritures hors période, doublons de pièce, tiers non auxiliarisables).
    /// Aucune action corrective (jamais de mutation). OFF = endpoint indisponible.
    /// </summary>
    public bool AccountingHealthEnabled { get; set; }

    /// <summary>
    /// Active le tableau de bord d'audit comptable (anomalies granulaires, KPI, analytics).
    /// OFF = l'écran /accounting/health conserve l'UI checklist simple.
    /// </summary>
    public bool AccountingAuditDashboardEnabled { get; set; }

    /// <summary>
    /// Persiste les runs de contrôle et les anomalies détectées (workflow collaboratif).
    /// OFF = évaluation à la volée sans écriture en base.
    /// </summary>
    public bool AccountingAuditPersistenceEnabled { get; set; }

    /// <summary>
    /// Active la planification Hangfire des contrôles automatiques.
    /// </summary>
    public bool AccountingAuditSchedulingEnabled { get; set; }

    /// <summary>
    /// Active le lien trésorerie paie : enregistrement des paiements de salaires, écritures JB/JC
    /// et lettrage sur le compte 421. OFF = comportement historique (export virement CSV seul).
    /// </summary>
    public bool PayrollTreasuryLinkEnabled { get; set; } = true;

    /// <summary>
    /// Ventile le crédit 421 par salarié (comptes auxiliaires) à la validation de la paie.
    /// OFF = une seule ligne 421 agrégée (comportement historique).
    /// </summary>
    public bool PayrollEmployeeAuxiliaryEnabled { get; set; } = true;

    /// <summary>Compte SCE pour les prêts salariés en cours (retenues mensuelles).</summary>
    public string PayrollEmployeeLoansAccount { get; set; } = "425.1";

    /// <summary>Compte SCE pour les saisies sur salaire et pensions alimentaires.</summary>
    public string PayrollGarnishmentsAccount { get; set; } = "427";

    /// <summary>Compte SCE pour les retenues salariales mutuelle / caisse complémentaire.</summary>
    public string PayrollMutuelleEmployeeAccount { get; set; } = "428.1";

    /// <summary>Compte SCE pour les charges patronales mutuelle / caisse complémentaire.</summary>
    public string PayrollMutuelleEmployerAccount { get; set; } = "647";

    /// <summary>Compte SCE pour la part employée des tickets restaurant.</summary>
    public string PayrollMealVoucherEmployeeAccount { get; set; } = "428.2";

    /// <summary>Active les caisses / mutuelles complémentaires dans le module paie.</summary>
    public bool PayrollSocialFundsEnabled { get; set; }

    /// <summary>Active les tickets restaurant dans le module paie.</summary>
    public bool PayrollMealVouchersEnabled { get; set; }

    /// <summary>Active les avantages en nature (véhicule, logement…) dans le module paie.</summary>
    public bool PayrollInKindBenefitsEnabled { get; set; }

    /// <summary>Active les prêts salariés avec échéancier dans le module paie.</summary>
    public bool PayrollEmployeeLoansEnabled { get; set; }

    /// <summary>Active les saisies sur salaire et pensions alimentaires dans le module paie.</summary>
    public bool PayrollGarnishmentsEnabled { get; set; }

    /// <summary>
    /// Active le bordereau CNSS mensuel et le suivi des versements (compte 453).
    /// Désactivé par défaut.
    /// </summary>
    public bool PayrollCnssRemittanceEnabled { get; set; }

    /// <summary>Active le calcul automatique des congés maladie (IJ CNSS, carence, subrogation).</summary>
    public bool PayrollStatutorySickLeaveEnabled { get; set; }

    /// <summary>Active le calcul automatique des congés maternité.</summary>
    public bool PayrollStatutoryMaternityLeaveEnabled { get; set; }

    /// <summary>Active le calcul automatique des congés paternité.</summary>
    public bool PayrollStatutoryPaternityLeaveEnabled { get; set; }

    /// <summary>Active le calcul des indemnités de rupture et soldes de tout compte.</summary>
    public bool PayrollTerminationIndemnityEnabled { get; set; }

    /// <summary>Active la génération des documents RH (STC, certificat de travail, attestation de salaire).</summary>
    public bool PayrollHrDocumentsEnabled { get; set; }

    /// <summary>Active les primes annuelles paramétrées (13e mois, ancienneté, vacances…).</summary>
    public bool PayrollAnnualBonusesEnabled { get; set; }

    /// <summary>Active les jours fériés tunisiens dans les calculs de jours ouvrables.</summary>
    public bool PayrollPublicHolidaysEnabled { get; set; }

    /// <summary>Active les fonctionnalités CIVP/SIVP étendues (attestation, alertes, validation).</summary>
    public bool PayrollCivpEnhancementsEnabled { get; set; }

    /// <summary>Active les plafonds CNSS et assiettes spécifiques dans le moteur de calcul.</summary>
    public bool PayrollCnssCeilingsEnabled { get; set; }

    /// <summary>
    /// Active l'historique des presets légaux multi-LF (lecture et comparaison).
    /// Safe par défaut : lecture seule, pas d'impact sur le calcul.
    /// </summary>
    public bool PayrollLegalPresetsHistoryEnabled { get; set; } = true;
}
