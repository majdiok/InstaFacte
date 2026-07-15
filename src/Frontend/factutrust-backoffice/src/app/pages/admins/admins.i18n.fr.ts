/**
 * Libellés FR centralisés pour la page Administrateurs plateforme (Lot B1).
 *
 * Vocabulaire FR-TN figé : `super-administrateur`, `responsable facturation`,
 * `agent support`, `opérateur migrations`, `auditeur lecture seule`.
 */

export const ADMINS_FR = {
  // ----- Page header -------------------------------------------------------
  'list.title': 'Administrateurs plateforme',
  'list.subtitle':
    'Gérez les administrateurs de la plateforme et leurs rôles. Cinq rôles disponibles avec permissions hiérarchisées.',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.create': 'Créer un admin',

  // ----- KPIs --------------------------------------------------------------
  'kpi.total': 'Administrateurs',
  'kpi.total.hint': 'Tous rôles confondus',
  'kpi.active': 'Actifs',
  'kpi.active.hint': 'Comptes pouvant se connecter',
  'kpi.locked': 'Verrouillés',
  'kpi.locked.hint': 'Tentatives échouées',
  'kpi.superAdmin': 'Super-admins',
  'kpi.superAdmin.hint': 'Accès complet',

  // ----- Rôles (libellés UI) -----------------------------------------------
  'role.PlatformAdmin': 'Super-administrateur',
  'role.BillingAdmin': 'Responsable facturation',
  'role.SupportAgent': 'Agent support',
  'role.MigrationOperator': 'Opérateur migrations',
  'role.ReadOnlyAuditor': 'Auditeur (lecture seule)',
  'role.PlatformAdmin.desc': 'Toutes les permissions plateforme.',
  'role.BillingAdmin.desc': 'Plans, coupons, factures, paiements.',
  'role.SupportAgent.desc': 'Lecture tenants + modération vitrines + audit.',
  'role.MigrationOperator.desc': 'Exécution des migrations EF.',
  'role.ReadOnlyAuditor.desc': 'Lecture seule de toutes les données.',

  // ----- Table columns -----------------------------------------------------
  'col.name': 'Nom',
  'col.email': 'Email',
  'col.role': 'Rôle',
  'col.lastLogin': 'Dernière connexion',
  'col.state': 'État',
  'col.actions': 'Actions',

  // ----- Statut compte -----------------------------------------------------
  'state.active': 'Actif',
  'state.disabled': 'Désactivé',
  'state.locked': 'Verrouillé',
  'state.never': 'Jamais',

  // ----- Actions ligne -----------------------------------------------------
  'action.changeRole': 'Modifier le rôle',
  'action.disable': 'Désactiver',
  'action.enable': 'Réactiver',
  'action.resetPassword': 'Réinitialiser le mot de passe',
  'action.copyId': 'Copier l’identifiant',

  // ----- Modal Créer --------------------------------------------------------
  'create.title': 'Créer un administrateur',
  'create.intro':
    'L’administrateur sera créé immédiatement avec le mot de passe initial. Il devra le changer à sa première connexion (recommandé).',
  'create.field.email': 'Email',
  'create.field.firstName': 'Prénom',
  'create.field.lastName': 'Nom',
  'create.field.role': 'Rôle',
  'create.field.password': 'Mot de passe initial (≥ 14 caractères)',
  'create.field.passwordHint':
    'Minimum 14 caractères, mélange recommandé : majuscules, minuscules, chiffres, symboles.',
  'create.confirm': 'Créer',

  // ----- Modal Rôle --------------------------------------------------------
  'role.title': 'Modifier le rôle',
  'role.intro': 'Le nouveau rôle sera appliqué immédiatement. L’utilisateur devra se reconnecter.',
  'role.confirm': 'Modifier le rôle',

  // ----- Modal Reset password ----------------------------------------------
  'reset.title': 'Réinitialiser le mot de passe',
  'reset.intro':
    'Définissez un nouveau mot de passe (≥ 14 caractères). L’utilisateur devra se reconnecter.',
  'reset.field.password': 'Nouveau mot de passe',
  'reset.confirm': 'Réinitialiser',

  // ----- Modal Désactiver --------------------------------------------------
  'disable.title': 'Désactiver l’administrateur',
  'disable.warn':
    'L’utilisateur ne pourra plus se connecter. Le refresh token sera invalidé.',
  'disable.confirm': 'Désactiver',

  // ----- Empty / error states ----------------------------------------------
  'empty.title': 'Aucun administrateur',
  'empty.desc': 'Créez le premier administrateur pour démarrer.',
  'error.title': 'Erreur',
  'error.detail': 'Impossible de contacter l’API.',

  // ----- Toasts ------------------------------------------------------------
  'toast.create.success': 'Administrateur créé',
  'toast.role.success': 'Rôle modifié',
  'toast.disable.success': 'Administrateur désactivé',
  'toast.enable.success': 'Administrateur réactivé',
  'toast.reset.success': 'Mot de passe réinitialisé'
} as const satisfies Record<string, string>;

export type AdminsFrKey = keyof typeof ADMINS_FR;

/** Helper qui convertit un code rôle backend en libellé FR. */
export function roleLabel(role: string): string {
  const key = `role.${role}` as AdminsFrKey;
  return (ADMINS_FR as Record<string, string>)[key] ?? role;
}

/** Helper qui décrit un rôle (utilisé dans dropdown create/change-role). */
export function roleDescription(role: string): string {
  const key = `role.${role}.desc` as AdminsFrKey;
  return (ADMINS_FR as Record<string, string>)[key] ?? '';
}
