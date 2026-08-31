/**
 * Libellés FR centralisés du backoffice plateforme InstaFact.
 *
 * Pourquoi ce fichier :
 *  - Vocabulaire FR-TN figé (cf. plan : `entreprise`, `raison sociale`, `NIF`,
 *    `régime fiscal`, `vitrine publique`, etc.).
 *  - Préparation à une éventuelle migration ngx-translate sans tout réécrire.
 *  - Helper `t()` pour usage immédiat ; remplaçable plus tard par
 *    `TranslateService.instant()` avec un seul `replace_all`.
 */

export const FR_LABELS = {
  // ----- App / shell -------------------------------------------------------
  'app.brand': 'InstaFact',
  'app.brand.suffix': 'Plateforme',
  'app.role.platformAdmin': 'Plateforme Admin',

  // ----- Navigation principale --------------------------------------------
  'nav.dashboard': 'Tableau de bord',
  'nav.tenants': 'Entreprises',
  'nav.migrations': 'Migrations',
  'nav.storefronts': 'Vitrines 3D',
  'nav.plans': 'Plans & abonnements',
  'nav.sectorRules': 'Règles sectorielles',
  'nav.coupons': 'Coupons',
  'nav.invoices': 'Factures plateforme',
  'nav.payments': 'Paiements',
  'nav.audit': 'Journal d’audit',
  'nav.admins': 'Administrateurs',
  'nav.notifications': 'Notifications',
  'nav.preferences': 'Préférences',

  // ----- Menu utilisateur --------------------------------------------------
  'user.menu.profile': 'Mon profil',
  'user.menu.preferences': 'Préférences',
  'user.menu.audit': 'Mon audit',
  'user.menu.docs': 'Documentation',
  'user.menu.shortcuts': 'Raccourcis clavier',
  'user.menu.logout': 'Déconnexion',

  // ----- Actions globales --------------------------------------------------
  'action.search': 'Rechercher',
  'action.notifications': 'Notifications',
  'action.apply': 'Appliquer',
  'action.cancel': 'Annuler',
  'action.confirm': 'Confirmer',
  'action.close': 'Fermer',
  'action.refresh': 'Rafraîchir',
  'action.reset': 'Réinitialiser',
  'action.save': 'Enregistrer',
  'action.export': 'Exporter',
  'action.delete': 'Supprimer',
  'action.suspend': 'Suspendre',
  'action.reactivate': 'Réactiver',
  'action.archive': 'Archiver',
  'action.publish': 'Publier',
  'action.reject': 'Refuser',
  'action.approve': 'Approuver',
  'action.preview': 'Prévisualiser',
  'action.details': 'Détails',
  'action.viewMore': 'Voir plus',
  'action.copy': 'Copier',
  'action.copyId': 'Copier l’identifiant',
  'action.retry': 'Réessayer',

  // ----- Vocabulaire métier ------------------------------------------------
  'biz.companyName': 'Raison sociale',
  'biz.email': 'Email',
  'biz.phone': 'Téléphone',
  'biz.address': 'Adresse',
  'biz.nif': 'NIF',
  'biz.taxRegime': 'Régime fiscal',
  'biz.taxRegime.real': 'Régime réel',
  'biz.taxRegime.flatRate': 'Régime forfaitaire',
  'biz.taxRegime.exempt': 'Exonéré',
  'biz.plan': 'Plan',
  'biz.plan.free': 'Gratuit',
  'biz.plan.monthly': 'Mensuel',
  'biz.plan.annual': 'Annuel',
  'biz.subscription.status': 'Statut abonnement',
  'biz.subscription.status.active': 'Actif',
  'biz.subscription.status.trial': 'Essai',
  'biz.subscription.status.expired': 'Expiré',
  'biz.subscription.status.cancelled': 'Annulé',
  'biz.subscription.status.pastDue': 'Impayé',
  'biz.subscription.status.suspended': 'Suspendu',
  'biz.tenant.active': 'Actif',
  'biz.tenant.inactive': 'Inactif',
  'biz.segment.paying': 'Abonné',
  'biz.segment.nonPaying': 'Non abonné',
  'biz.currency.tnd': 'TND',

  // ----- Page Entreprises --------------------------------------------------
  'tenants.list.title': 'Entreprises',
  'tenants.list.subtitle':
    'Annuaire des entreprises onboardées. Filtrez par abonnement, régime fiscal et statut.',
  'tenants.kpi.total': 'Entreprises',
  'tenants.kpi.paying': 'Abonnés payants',
  'tenants.kpi.conversion': 'Conv. essai → payant',
  'tenants.kpi.risk': 'Tenants en risque',
  'tenants.kpi.mrrEstimate': 'TND/mois estimés',
  'tenants.search.placeholder': 'Rechercher (raison, email, NIF, slug)…',
  'tenants.filters.segment': 'Segment',
  'tenants.filters.plan': 'Plan',
  'tenants.filters.status': 'Statut',
  'tenants.filters.taxRegime': 'Régime fiscal',
  'tenants.filters.tenantState': 'Entreprise',
  'tenants.filters.signupDate': 'Inscription',
  'tenants.empty.noResult': 'Aucune entreprise ne correspond. Essayez de retirer un filtre.',
  'tenants.empty.noTenant': 'Aucune entreprise enregistrée',

  // ----- Page Migrations ---------------------------------------------------
  'migrations.list.title': 'Migrations bases tenants',
  'migrations.list.subtitle':
    'État des migrations EF par entreprise. L’application sur une ligne n’affecte que cette base.',
  'migrations.kpi.total': 'Total',
  'migrations.kpi.upToDate': 'À jour',
  'migrations.kpi.pending': 'En retard',
  'migrations.kpi.failed24h': 'Échecs 24h',
  'migrations.applyAll': 'Appliquer sur tous les tenants actifs',
  'migrations.alert.pending': 'entreprise(s) en retard de migration. Une mise à jour est requise.',
  'migrations.status.applied': 'Appliquées',
  'migrations.status.missing': 'Manquantes',
  'migrations.confirm.typeToConfirm': 'Pour confirmer, tapez',

  // ----- Page Vitrines 3D --------------------------------------------------
  'storefronts.list.title': 'Vitrines publiques',
  'storefronts.list.subtitle':
    'Validation des vitrines candidates à l’affichage sur Rue InstaFact. Chaque publication requiert un consentement opt-in et un contrôle de conformité.',
  'storefronts.kpi.pending': 'En attente',
  'storefronts.kpi.published': 'Publiées',
  'storefronts.kpi.rejected': 'Refusées (30j)',
  'storefronts.kpi.suspended': 'Suspendues',
  'storefronts.tab.pending': 'En attente',
  'storefronts.tab.published': 'Publiées',
  'storefronts.tab.rejected': 'Refusées',
  'storefronts.tab.suspended': 'Suspendues',
  'storefronts.empty.allClear': 'Aucune vitrine en attente. Bravo !',
  'storefronts.reject.reason': 'Motif de refus',
  'storefronts.reject.reason.logo': 'Logo non conforme',
  'storefronts.reject.reason.name': 'Nom inapproprié',
  'storefronts.reject.reason.contact': 'Contact invalide ou injoignable',
  'storefronts.reject.reason.category': 'Catégorie incorrecte',
  'storefronts.reject.reason.other': 'Autre',

  // ----- Empty states / messages -----------------------------------------
  'state.loading': 'Chargement…',
  'state.error': 'Une erreur est survenue.',
  'state.error.retry': 'Réessayer',

  // ----- Pagination --------------------------------------------------------
  'pagination.summary': '{first}–{last} sur {total}',
  'pagination.rowsPerPage': 'Lignes par page'
} as const;

export type FrLabelKey = keyof typeof FR_LABELS;

/**
 * Helper de traduction : remplace les placeholders `{key}` du libellé.
 *
 * À terme, remplaçable par `inject(TranslateService).instant(key, params)`
 * avec un simple find/replace sur les call-sites.
 */
export function t(key: FrLabelKey, params?: Record<string, string | number>): string {
  let value: string = FR_LABELS[key];
  if (params) {
    for (const [paramKey, paramValue] of Object.entries(params)) {
      value = value.replace(new RegExp(`\\{${paramKey}\\}`, 'g'), String(paramValue));
    }
  }
  return value;
}
