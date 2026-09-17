import type { WorkflowStepType } from './studio-workflows.models';

/**
 * Libellés FR centralisés de la fonctionnalité « Workflows » du Studio (4.4a1). Groupes figés
 * (contrat exposé à la partie B) : `nav`, `hub`, `triggers`, `triggerHints`, `designer`, `steps`,
 * `stepTypes`, `enumValues`, `assignee`, `roles`, `instanceStatus`, `stepRunStatus`, `outcomes`,
 * `approvalStatus`, `instances`, `approvals`, `recordTab`, `soon`.
 */
export const STUDIO_WORKFLOW_LABELS = {
  nav: {
    workflows: 'Workflows',
    approvals: 'Mes approbations'
  },

  hub: {
    title: 'Workflows',
    subtitle: 'Automatisez les traitements de vos tables.',
    newWorkflow: 'Nouveau workflow',
    allTables: 'Toutes les tables',
    search: 'Rechercher un workflow…',
    columns: {
      name: 'Nom',
      table: 'Table',
      trigger: 'Déclencheur',
      steps: 'Étapes',
      active: 'Actif',
      openInstances: 'Instances ouvertes',
      lastRun: 'Dernière exécution',
      actions: 'Actions'
    },
    neverRun: 'Jamais exécuté',
    emptyTitle: 'Aucun workflow pour cette table',
    emptyHint: 'Créez votre premier workflow pour automatiser les traitements de cette table.',
    deleteConfirm: 'Supprimer « {name} » ? {count} instance(s) ouverte(s) seront annulées.',
    deleted: '{count} instance(s) annulée(s).',
    duplicateKey: 'Un workflow porte déjà cette clé pour cette table.',
    duplicated: 'Copie créée (inactive).',
    toggled: 'Workflow {state}.',
    tooManyTables: 'Choisissez une table : plus de 25 tables actives.',
    loadError: 'Chargement des workflows impossible.'
  },

  triggers: {
    on_create: 'À la création',
    on_update: 'À la modification',
    field_changed: 'Quand un champ change',
    manual: 'Manuel',
    scheduled: 'Planifié'
  },

  triggerHints: {
    on_create: 'Se déclenche à chaque nouvel enregistrement.',
    on_update: 'Se déclenche à chaque modification.',
    field_changed: 'Se déclenche quand le champ surveillé change (valeurs optionnelles).',
    manual: 'Lancé depuis la fiche par un utilisateur autorisé.',
    scheduled: 'Bientôt disponible.'
  },

  designer: {
    title: 'Workflow',
    newTitle: 'Nouveau workflow',
    name: 'Nom',
    key: 'Clé',
    table: 'Table',
    trigger: 'Déclencheur',
    watchedField: 'Champ surveillé',
    from: 'Ancienne valeur',
    to: 'Nouvelle valeur',
    active: 'Actif',
    inactive: 'Inactif',
    validate: 'Valider',
    save: 'Enregistrer',
    duplicate: 'Dupliquer',
    saved: 'Workflow enregistré.',
    validOk: 'Aucune erreur.',
    validErrors: '{count} erreur(s) à corriger.',
    conflict: 'Ce workflow a été modifié par ailleurs. Rechargez la page.',
    quota: 'Limite de {max} workflows par table atteinte.',
    stepsCount: '{count} / {max}',
    addStep: 'Ajouter une étape',
    properties: 'Propriétés de l\'étape',
    noStep: 'Sélectionnez une étape.',
    unknownKind: 'Propriété non prise en charge par le concepteur, JSON brut.',
    keyHint: 'Minuscule initiale, lettres, chiffres ou « _ » (2 à 64 caractères).',
    keyImmutable: 'La clé est immuable après création.',
    dirty: 'Modifications non enregistrées.',
    reload: 'Recharger',
    tooLarge: 'Le workflow dépasse 64 Ko : réduisez le nombre d\'étapes ou la taille des gabarits.',
    noSteps: 'Ajoutez au moins une étape.',
    unsavedInstances: 'Enregistrez le workflow pour voir ses instances.',
    warnings: '{count} avertissement(s).'
  },

  steps: {
    moveUp: 'Monter',
    moveDown: 'Descendre',
    remove: 'Supprimer',
    key: 'Clé',
    label: 'Libellé',
    keyInvalid: 'Clé invalide : minuscule initiale, puis lettres, chiffres ou « _ » (2 à 64 caractères).',
    keyDuplicate: 'Cette clé est déjà utilisée par une autre étape.',
    conditionPreview: 'Aperçu de la condition',
    gotoTarget: 'Aller à l\'étape'
  },

  /** Repli FR si le catalogue serveur ne fournit pas de `label`. */
  stepTypes: {
    condition: 'Condition',
    update_field: 'Mettre à jour un champ',
    erp_action: 'Action ERP (pont)',
    notify: 'Notifier',
    approval: 'Approbation',
    wait: 'Attendre',
    create_record: 'Créer un enregistrement'
  },

  /** Libellés des valeurs `enum` du catalogue d'étapes (clés = nom de propriété puis valeur). */
  enumValues: {
    match: { all: 'Toutes les conditions', any: 'Au moins une' },
    onFalse: { stop: 'Arrêter le workflow', skip: 'Passer l\'étape', goto: 'Aller à une étape' },
    onFailure: { fail: 'Échouer', continue: 'Continuer' },
    onTimeout: { reject: 'Refuser', approve: 'Approuver', fail: 'Échouer' },
    onReject: { stop: 'Arrêter', goto: 'Aller à une étape', continue: 'Continuer' }
  },

  assignee: {
    role: 'Un rôle',
    user: 'Un utilisateur',
    startedBy: 'La personne qui a lancé',
    pickRole: 'Choisir un rôle',
    pickUser: 'Choisir un utilisateur',
    usersUnavailable: 'Liste des utilisateurs réservée aux administrateurs.'
  },

  /** 11 rôles tenant (D15) — `FirmManager`/`FirmAccountant` exclus (D-44-13) ; clés = noms exacts de l'enum `UserRole`. */
  roles: {
    Administrator: 'Administrateur',
    Accountant: 'Comptable',
    Client: 'Client',
    SalesRep: 'Commercial',
    SalesManager: 'Responsable commercial',
    Warehouse: 'Magasinier',
    Purchaser: 'Acheteur',
    Cashier: 'Caissier',
    Auditor: 'Auditeur',
    Supervisor: 'Superviseur',
    Developer: 'Développeur'
  },

  /** Réutilise les mots de `planStatus` (studio-ai-labels.ts). */
  instanceStatus: {
    running: 'En cours',
    waiting: 'En attente',
    waiting_approval: 'À valider',
    completed: 'Terminé',
    failed: 'Échec',
    cancelled: 'Annulé'
  },

  stepRunStatus: {
    succeeded: 'Terminée',
    skipped: 'Ignorée',
    failed: 'Échec',
    suspended: 'En attente'
  },

  outcomes: {
    continue: 'Continuer',
    skip: 'Ignorer',
    goto: 'Aller à',
    stop: 'Arrêter',
    suspend: 'Suspendre',
    fail: 'Échec'
  },

  approvalStatus: {
    pending: 'En attente',
    approved: 'Approuvée',
    rejected: 'Refusée',
    cancelled: 'Annulée',
    expired: 'Expirée'
  },

  instances: {
    recent: 'Instances récentes',
    empty: 'Aucune instance.',
    detail: 'Détail',
    close: 'Fermer',
    cancel: 'Annuler l\'instance',
    cancelReason: 'Motif (optionnel, 500 caractères max.)',
    cancelTitle: 'Annuler cette instance ?',
    cancelled: 'Instance annulée.',
    cancelConflict: 'Cette instance est déjà terminée.',
    remind: 'Relancer les approbateurs',
    reminded: 'Rappel envoyé.',
    remindConflict: 'Rappel déjà envoyé depuis moins de 24 h',
    startedBy: 'Démarré par',
    startedAt: 'Démarré le',
    dueAt: 'Échéance',
    currentStep: 'Étape courante',
    error: 'Erreur',
    timeline: 'Déroulé',
    duration: '{value}',
    approvals: 'Approbations',
    noApprovals: 'Aucune approbation.',
    context: 'Contexte masqué.',
    summary: '{done} étape(s) terminée(s) · {waiting} en attente · {todo} à venir'
  },

  approvals: {
    title: 'Mes approbations',
    subtitle: 'Approbations qui vous sont assignées ou assignées à votre rôle.',
    kpiPending: 'À traiter',
    kpiLate: 'En retard',
    kpiSoon: 'Sous 24 h',
    tabPending: 'À traiter',
    columns: {
      workflow: 'Workflow',
      record: 'Enregistrement',
      requestedBy: 'Demandé par',
      requestedAt: 'Demandé le',
      dueAt: 'Échéance',
      actions: 'Actions'
    },
    approve: 'Approuver',
    reject: 'Refuser',
    detail: 'Détail',
    comment: 'Commentaire',
    commentRequired: 'Le commentaire est obligatoire pour refuser.',
    approved: 'Approbation enregistrée.',
    rejected: 'Refus enregistré.',
    alreadyDecided: 'Cette approbation a déjà été traitée.',
    notAssigned: 'Cette approbation ne vous est plus assignée.',
    empty: 'Aucune approbation en attente',
    emptyHint: 'Vous êtes à jour.',
    noDue: 'Aucune échéance',
    late: 'En retard de {value}',
    due: 'Dans {value}',
    openRecord: 'Ouvrir la fiche',
    readOnly: 'Lecture seule : votre rôle ne permet pas de décider.'
  },

  recordTab: {
    label: 'Workflows',
    launch: 'Lancer un workflow',
    launchPick: 'Choisir un workflow',
    run: 'Lancer',
    started: 'Workflow lancé.',
    quota: 'Limite de 200 instances atteinte pour cet enregistrement.',
    empty: 'Aucun workflow exécuté sur cet enregistrement.',
    columns: {
      workflow: 'Workflow',
      status: 'Statut',
      currentStep: 'Étape courante',
      startedBy: 'Démarré par'
    }
  },

  soon: 'Bientôt'
} as const;

export type StudioWorkflowLabels = typeof STUDIO_WORKFLOW_LABELS;

/** Remplace les `{jetons}` d'un libellé (copie de `formatLabel`, studio-ai-labels.ts l.697 — D-44-08). */
export function formatWorkflowLabel(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, k: string) => (values[k] !== undefined ? String(values[k]) : `{${k}}`));
}

/** Libellé d'un type d'étape : `label` du catalogue s'il est fourni, sinon repli FR local, sinon la clé brute. */
export function stepTypeLabel(type: WorkflowStepType, catalogLabel?: string | null): string {
  return catalogLabel?.trim() || STUDIO_WORKFLOW_LABELS.stepTypes[type] || type;
}

/** Icônes Font Awesome par type d'étape (vocabulaire partagé avec les maquettes — D-44-09). */
export const STEP_TYPE_ICONS: Readonly<Record<WorkflowStepType, string>> = { condition: 'fa-solid fa-code-branch', update_field: 'fa-solid fa-pen-to-square', erp_action: 'fa-solid fa-bolt', notify: 'fa-solid fa-bell', approval: 'fa-solid fa-user-check', wait: 'fa-solid fa-hourglass-half', create_record: 'fa-solid fa-file-circle-plus' };
