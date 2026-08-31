/**
 * Libellés FR centralisés spécifiques à la page Entreprises.
 *
 * Ces clés sont importées depuis le composant via :
 *  ```ts
 *  import { TENANTS_FR } from './tenants.i18n.fr';
 *  ```
 * et accessibles via `TENANTS_FR['list.kpi.total']`. Voir aussi `core/i18n/fr.ts`
 * pour les libellés transverses.
 */

export const TENANTS_FR = {
  // ----- Page header -------------------------------------------------------
  'list.title': 'Entreprises',
  'list.subtitle':
    'Annuaire des entreprises onboardées. Filtrez par abonnement, régime fiscal et statut.',
  'list.actions.export': 'Exporter',
  'list.actions.create': 'Nouvelle entreprise',

  // ----- KPIs --------------------------------------------------------------
  'kpi.total': 'Entreprises',
  'kpi.total.hint': '{count} sur 30j',
  'kpi.paying': 'Abonnés payants',
  'kpi.paying.hint': '≈ {mrr} TND/mois',
  'kpi.conversion': 'Conv. essai → payant',
  'kpi.conversion.hint': '30 derniers jours',
  'kpi.risk': 'Tenants en risque',
  'kpi.risk.hint': 'Impayé + suspendu',

  // ----- Toolbar / filtres --------------------------------------------------
  'filters.search.placeholder': 'Rechercher (raison sociale, email, NIF)…',
  'filters.segment': 'Segment',
  'filters.plan': 'Plan',
  'filters.status': 'Statut abonnement',
  'filters.tenantState': 'Entreprise',
  'filters.taxRegime': 'Régime fiscal',
  'filters.reset': 'Réinitialiser',
  'filters.savedViews': 'Vues',
  'filters.savedViews.save': 'Sauvegarder ce filtre',
  'filters.savedViews.delete': 'Supprimer cette vue',
  'filters.savedViews.namePrompt': 'Nom de la vue',
  'filters.activeChips.clearAll': 'Effacer tout',

  // ----- Segment options ---------------------------------------------------
  'segment.paying': 'Abonnés payants',
  'segment.nonPaying': 'Non abonnés',

  // ----- Plan options ------------------------------------------------------
  'plan.free': 'Gratuit',
  'plan.monthly': 'Mensuel',
  'plan.annual': 'Annuel',

  // ----- Status options ----------------------------------------------------
  'status.active': 'Actif',
  'status.trial': 'Essai',
  'status.expired': 'Expiré',
  'status.cancelled': 'Annulé',
  'status.pastDue': 'Impayé',
  'status.suspended': 'Suspendu',

  // ----- Tenant state options ----------------------------------------------
  'tenantState.active': 'Active',
  'tenantState.inactive': 'Inactive',

  // ----- Tax regime options ------------------------------------------------
  'taxRegime.real': 'Régime réel',
  'taxRegime.flatRate': 'Régime forfaitaire',
  'taxRegime.exempt': 'Exonéré',

  // ----- Table columns -----------------------------------------------------
  'col.companyName': 'Entreprise',
  'col.nif': 'NIF',
  'col.taxRegime': 'Régime',
  'col.plan': 'Plan',
  'col.status': 'Statut',
  'col.mrr': 'MRR',
  'col.lastActivity': 'Dernière activité',
  'col.actions': 'Actions',

  // ----- Row actions -------------------------------------------------------
  'rowAction.viewDetails': 'Voir les détails',
  'rowAction.quickView': 'Aperçu rapide',
  'rowAction.editSubscription': 'Modifier l’abonnement',
  'rowAction.aiConfig': 'Configuration IA',
  'rowAction.suspend': 'Suspendre',
  'rowAction.reactivate': 'Réactiver',
  'rowAction.sendEmail': 'Envoyer un email',
  'rowAction.viewAudit': 'Voir l’audit',
  'rowAction.copyId': 'Copier l’identifiant',

  // ----- Bulk actions ------------------------------------------------------
  'bulk.selected': '{count} entreprise(s) sélectionnée(s)',
  'bulk.suspend': 'Suspendre',
  'bulk.export': 'Exporter sélection',
  'bulk.email': 'Envoyer email',
  'bulk.clear': 'Désélectionner',

  // ----- Empty / error states ----------------------------------------------
  'empty.noResult.title': 'Aucune entreprise ne correspond',
  'empty.noResult.desc': 'Essayez de retirer un filtre ou modifier votre recherche.',
  'empty.noTenant.title': 'Aucune entreprise enregistrée',
  'empty.noTenant.desc': 'Les entreprises apparaîtront ici dès leur premier onboarding.',
  'error.title': 'Une erreur est survenue',
  'error.desc': 'Impossible de récupérer la liste. Réessayez dans un instant.',

  // ----- Drawer (quick view) -----------------------------------------------
  'drawer.title': 'Aperçu',
  'drawer.tab.overview': 'Aperçu',
  'drawer.tab.subscription': 'Abonnement',
  'drawer.tab.modules': 'Modules',
  'drawer.tab.activity': 'Activité',
  'drawer.tab.migrations': 'Migrations',
  'drawer.tab.storefront': 'Vitrine',
  'drawer.field.email': 'Email',
  'drawer.field.phone': 'Téléphone',
  'drawer.field.nif': 'NIF',
  'drawer.field.taxRegime': 'Régime fiscal',
  'drawer.field.address': 'Adresse',
  'drawer.field.website': 'Site web',
  'drawer.field.createdAt': 'Inscrite le',
  'drawer.field.deactivatedAt': 'Désactivée le',
  'drawer.field.databaseName': 'Base de données',
  'drawer.field.plan': 'Plan',
  'drawer.field.status': 'Statut',
  'drawer.field.startDate': 'Début',
  'drawer.field.endDate': 'Prochaine facture',
  'drawer.field.migrationStatus': 'Migrations EF',
  'drawer.migrations.applied': 'Appliquées',
  'drawer.migrations.missing': 'Manquantes',
  'drawer.action.openFullPage': 'Voir page complète',
  'drawer.action.openAiConfig': 'Configuration IA',
  'drawer.placeholder.modules':
    'La gestion des modules par tenant arrive avec le Lot C1 (Plans configurables).',
  'drawer.placeholder.activity':
    'Le journal d’activité par tenant arrive avec le Lot D3 (impersonation + audit dédié).',
  'drawer.placeholder.storefront':
    'Le détail vitrine arrive avec le Lot D5 (signalements + historique).',

  // ----- Pagination --------------------------------------------------------
  'pagination.template': '{first}–{last} sur {totalRecords}',

  // ----- Phase 2 (WP-F8) — Configuration sectorielle -------------------------
  // ATTENTION : « Secteur » désigne ici le secteur d'activité (commerce, BTP…), à ne jamais
  // confondre avec le filtre « Segment » de facturation (payant/non payant) déjà existant.
  'sector.tab.label': 'Configuration sectorielle',
  'sector.card.title': 'Configuration sectorielle actuelle',
  'sector.card.subtitle': 'Secteur d’activité et domaine utilisés pour déterminer les modules recommandés et les modèles de données.',
  'sector.segment': 'Secteur',
  'sector.domain': 'Domaine',
  'sector.lastChange': 'Dernière reconfiguration',
  'sector.lastChange.unknown': 'Inconnue',
  'sector.action.modify': 'Modifier',
  'sector.empty.title': 'Aucun profil sectoriel',
  'sector.empty.desc': 'Cette entreprise n’a pas encore été classifiée (créée avant Phase 2, ou onboarding non finalisé).',
  'sector.picker.newSegment': 'Nouveau secteur',
  'sector.picker.newDomain': 'Nouveau domaine',
  'sector.action.preview': 'Prévisualiser la reconfiguration',
  'sector.action.apply': 'Appliquer la reconfiguration',
  'sector.action.cancel': 'Annuler',
  'sector.preview.title': 'Prévisualisation — {from} → {to}',
  'sector.preview.computedAt': 'Calculée le {date} · règles version {version}',
  'sector.diff.modulesEnabled': 'Modules à activer',
  'sector.diff.modulesDisabled': 'Modules à désactiver',
  'sector.diff.modulesDisabled.warning':
    'La désactivation masque le module sans supprimer les données. Il peut être réactivé manuellement à tout moment.',
  'sector.diff.templates': 'Modèles de données à appliquer',
  'sector.diff.settings': 'Paramètres',
  'sector.diff.warnings': 'Avertissements',
  'sector.diff.none': 'Aucun changement',
  'sector.audit.note': 'Chaque reconfiguration est journalisée dans l’audit plateforme.',
  'sector.confirm.checkbox':
    'J’ai vérifié la prévisualisation ci-dessus et je comprends que les modules désactivés seront masqués (sans perte de données).',
  'sector.confirm.tenantWarning': 'Cette action modifie la configuration de {tenant}.',
  'sector.toast.apply.success': 'Reconfiguration sectorielle appliquée',
  'sector.toast.preview.error': 'Impossible de calculer la prévisualisation',
  'sector.readonly.hint': 'Vous n’avez pas la permission de modifier la configuration sectorielle.'
} as const satisfies Record<string, string>;

export type TenantsFrKey = keyof typeof TENANTS_FR;
