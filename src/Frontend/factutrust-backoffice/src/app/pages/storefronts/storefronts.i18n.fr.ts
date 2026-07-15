/**
 * Libellés FR centralisés spécifiques à la page Vitrines 3D.
 *
 * Vocabulaire FR-TN figé : `vitrine publique` (≠ storefront), `Rue InstaFact`,
 * `consentement opt-in`, `motif de refus`. Voir `core/i18n/fr.ts` pour les
 * libellés transverses.
 */

export const STOREFRONTS_FR = {
  // ----- Page header -------------------------------------------------------
  'list.title': 'Vitrines publiques',
  'list.subtitle':
    'Validation des vitrines candidates à l’affichage sur Rue InstaFact. Chaque publication requiert un consentement opt-in et un contrôle de conformité (logo, nom, contact).',
  'list.actions.refresh': 'Rafraîchir',

  // ----- KPIs --------------------------------------------------------------
  'kpi.pending': 'En attente',
  'kpi.pending.hint': 'À valider',
  'kpi.published': 'Publiées',
  'kpi.published.hint': 'En ligne sur Rue InstaFact',
  'kpi.rejected': 'Refusées',
  'kpi.rejected.hint': 'Brouillon avec motif',
  'kpi.suspended': 'Suspendues',
  'kpi.suspended.hint': 'Retirées de la rue',

  // ----- Tabs --------------------------------------------------------------
  'tab.pending': 'En attente',
  'tab.published': 'Publiées',
  'tab.rejected': 'Refusées',
  'tab.suspended': 'Suspendues',

  // ----- Card actions ------------------------------------------------------
  'card.preview': 'Prévisualiser',
  'card.approve': 'Approuver',
  'card.reject': 'Refuser',
  'card.brandColors': 'Couleurs de marque',
  'card.consentVersion': 'Consentement',
  'card.publishedAt': 'Publiée le',
  'card.suspendedAt': 'Suspendue le',
  'card.rejectionReason': 'Motif',

  // ----- Empty / error states ----------------------------------------------
  'empty.pending.title': 'Aucune vitrine en attente',
  'empty.pending.desc': 'Toutes les soumissions ont été traitées. Bravo !',
  'empty.published.title': 'Aucune vitrine publiée',
  'empty.published.desc': 'Les vitrines validées apparaîtront ici.',
  'empty.rejected.title': 'Aucune vitrine refusée',
  'empty.rejected.desc': 'Aucun motif de refus enregistré pour le moment.',
  'empty.suspended.title': 'Aucune vitrine suspendue',
  'empty.suspended.desc': 'Aucune vitrine n’a été retirée de la rue.',
  'error.title': 'Une erreur est survenue',
  'error.desc': 'Impossible de récupérer la liste. Réessayez dans un instant.',

  // ----- Modal Approbation -------------------------------------------------
  'approve.title': 'Publier la vitrine',
  'approve.recap.slug': 'Slug',
  'approve.recap.displayName': 'Nom public',
  'approve.recap.category': 'Catégorie',
  'approve.recap.theme': 'Thème de façade',
  'approve.recap.email': 'Contact email',
  'approve.recap.phone': 'Téléphone',
  'approve.recap.whatsApp': 'WhatsApp',
  'approve.recap.optIn': 'Opt-in (consentement)',
  'approve.recap.optInVersion': 'Version',
  'approve.recap.optInAcceptedAt': 'Accepté le',
  'approve.consent.label':
    'J’ai vérifié le consentement et la conformité légale du contenu (logo, nom, contact).',
  'approve.confirmLabel': 'Publier sur Rue InstaFact',

  // ----- Modal Refus -------------------------------------------------------
  'reject.title': 'Refuser la vitrine',
  'reject.intro': 'Sélectionnez le motif principal puis ajoutez des précisions visibles côté tenant.',
  'reject.reason.label': 'Motif principal',
  'reject.reason.logo': 'Logo non conforme',
  'reject.reason.name': 'Nom inapproprié',
  'reject.reason.contact': 'Contact invalide ou injoignable',
  'reject.reason.category': 'Catégorie incorrecte',
  'reject.reason.other': 'Autre',
  'reject.details.label': 'Détails (visibles côté tenant)',
  'reject.details.placeholder': 'Précisez les corrections attendues…',
  'reject.confirmLabel': 'Refuser la publication',

  // ----- Toasts ------------------------------------------------------------
  'toast.approve.success': 'Vitrine publiée',
  'toast.reject.success': 'Vitrine renvoyée en brouillon',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API'
} as const satisfies Record<string, string>;

export type StorefrontsFrKey = keyof typeof STOREFRONTS_FR;

/**
 * Mapping des codes de catégorie StorefrontCategory (backend) vers libellé FR.
 * Le backend renvoie les valeurs camelCase (ex: `groceryAndMarket`) ou int 0..N.
 */
export const STOREFRONT_CATEGORY_LABELS: Record<string, string> = {
  generalStore: 'Magasin généraliste',
  groceryAndMarket: 'Épicerie & marché',
  fashionAndAccessories: 'Mode & accessoires',
  electronicsAndAppliances: 'Électronique & électroménager',
  homeAndDecor: 'Maison & décoration',
  beautyAndWellness: 'Beauté & bien-être',
  foodAndRestaurant: 'Restauration & alimentaire',
  servicesAndRepair: 'Services & réparation'
};

/** Mapping des codes FacadeTheme (backend) vers libellé FR. */
export const STOREFRONT_THEME_LABELS: Record<string, string> = {
  classicStone: 'Pierre classique',
  modernGlass: 'Verre moderne',
  warmWood: 'Bois chaleureux',
  artDeco: 'Art déco',
  minimalist: 'Minimaliste',
  industrial: 'Industriel'
};
