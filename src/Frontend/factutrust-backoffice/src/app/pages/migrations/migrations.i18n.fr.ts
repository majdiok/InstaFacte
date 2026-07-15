/**
 * Libellés FR centralisés spécifiques à la page Migrations.
 *
 * Vocabulaire FR-TN figé : `migrations EF`, `entreprise`, `Appliquées`,
 * `Manquantes`, `À jour`, `En retard`. Voir `core/i18n/fr.ts` pour les
 * libellés transverses.
 */

export const MIGRATIONS_FR = {
  // ----- Page header -------------------------------------------------------
  'list.title': 'Migrations bases tenants',
  'list.subtitle':
    'État des migrations EF par entreprise. L’application sur une ligne n’affecte que cette base.',

  // ----- Alerte ------------------------------------------------------------
  'alert.pendingPrefix': 'entreprise(s) en retard de migration.',
  'alert.pendingDesc': 'Une mise à jour de schéma est requise pour garantir la cohérence des données.',
  'alert.pendingApplyAll': 'Tout appliquer',

  // ----- KPIs --------------------------------------------------------------
  'kpi.total': 'Tenants actifs',
  'kpi.total.hint': 'Tous régimes confondus',
  'kpi.upToDate': 'À jour',
  'kpi.upToDate.hint': 'Migrations appliquées',
  'kpi.pending': 'En retard',
  'kpi.pending.hint': 'Au moins une migration manquante',
  'kpi.failures24h': 'Échecs 24h',
  'kpi.failures24h.hint': 'Disponible avec Lot D4',

  // ----- Toolbar -----------------------------------------------------------
  'action.refresh': 'Rafraîchir',
  'action.applyAll': 'Appliquer sur tous les tenants actifs',
  'action.applyOne': 'Appliquer',
  'action.viewDetails': 'Voir détails',

  // ----- Table columns -----------------------------------------------------
  'col.tenant': 'Entreprise',
  'col.segment': 'Segment',
  'col.plan': 'Plan',
  'col.migrations': 'Migrations',
  'col.actions': 'Actions',

  // ----- Statut migration --------------------------------------------------
  'status.applied': 'Appliquées',
  'status.missing': 'Manquantes',
  'status.unknown': 'Inconnu',

  // ----- Empty / error states ----------------------------------------------
  'empty.noTenant.title': 'Aucune entreprise active',
  'empty.noTenant.desc': 'Aucune base tenant à migrer pour le moment.',
  'empty.allUpToDate.title': 'Tous les tenants sont à jour',
  'empty.allUpToDate.desc': 'Aucune migration manquante détectée. Bravo !',
  'error.title': 'Une erreur est survenue',
  'error.desc': 'Impossible de récupérer la liste. Réessayez dans un instant.',

  // ----- Modal "Appliquer sur tous" ---------------------------------------
  'applyAll.title': 'Appliquer les migrations à tous les tenants actifs',
  'applyAll.descPrefix': 'Vous êtes sur le point d’appliquer les migrations EF aux',
  'applyAll.descSuffix': 'entreprise(s) actives.',
  'applyAll.warning':
    'Pendant l’opération, les requêtes vers les bases concernées peuvent être ralenties ou mises en file d’attente.',
  'applyAll.runningTitle': 'Migrations en cours…',
  'applyAll.runningDesc': 'Cela peut prendre plusieurs minutes selon le nombre de tenants. Cette fenêtre se mettra à jour à la fin.',
  'applyAll.confirmKeyword': 'APPLIQUER',
  'applyAll.confirmLabel': 'Lancer la migration',

  // ----- Modal "Appliquer un tenant" --------------------------------------
  'applyOne.title': 'Appliquer les migrations',
  'applyOne.desc':
    'Les migrations EF manquantes seront appliquées sur la base de cette entreprise uniquement.',
  'applyOne.confirmLabel': 'Appliquer maintenant',

  // ----- Drawer ------------------------------------------------------------
  'drawer.title': 'Détail migration',
  'drawer.fields.tenant': 'Entreprise',
  'drawer.fields.plan': 'Plan',
  'drawer.fields.segment': 'Segment',
  'drawer.fields.status': 'Statut migrations',
  'drawer.fields.databaseName': 'Base de données',
  'drawer.placeholder.list':
    'La liste détaillée des migrations (appliquées, manquantes, échouées avec extrait d’erreur) arrive avec le Lot D4.',
  'drawer.action.apply': 'Appliquer maintenant',

  // ----- Section Historique ------------------------------------------------
  'history.title': 'Historique des exécutions',
  'history.placeholder':
    'L’historique persistant des exécutions (lancé par, durée, succès/échecs) sera disponible avec le Lot D4 (tables MigrationRuns + MigrationRunItems et job Hangfire).',

  // ----- Toasts ------------------------------------------------------------
  'toast.applyAll.success': 'Migrations appliquées avec succès',
  'toast.applyOne.success': 'Migrations appliquées',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API'
} as const satisfies Record<string, string>;

export type MigrationsFrKey = keyof typeof MIGRATIONS_FR;
