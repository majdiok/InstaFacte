/**
 * Lot C3 — Libellés FR pour la page Coupons.
 */
export const COUPONS_FR = {
  'list.title': 'Coupons promotionnels',
  'list.subtitle':
    'Créez et gérez les codes promo applicables aux abonnements InstaFact. Pourcentage ou montant fixe, validité bornée, limite de redemptions, plan ciblé.',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.create': 'Nouveau coupon',

  'kpi.total': 'Total',
  'kpi.active': 'Actifs',
  'kpi.redeemable': 'Utilisables',
  'kpi.redemptions': 'Redemptions',

  'filter.search': 'Rechercher un code',
  'filter.activeOnly': 'État',
  'filter.all': 'Tous',
  'filter.activeOnlyTrue': 'Actifs uniquement',
  'filter.activeOnlyFalse': 'Désactivés uniquement',

  'col.code': 'Code',
  'col.type': 'Type',
  'col.value': 'Valeur',
  'col.validity': 'Validité',
  'col.usage': 'Usage',
  'col.state': 'État',
  'col.actions': 'Actions',

  'state.redeemable': 'Utilisable',
  'state.expired': 'Expiré',
  'state.exhausted': 'Épuisé',
  'state.deactivated': 'Désactivé',
  'state.notYetValid': 'Non encore valide',

  'action.deactivate': 'Désactiver',
  'action.reactivate': 'Réactiver',
  'action.viewRedemptions': 'Redemptions',
  'action.edit': 'Modifier',

  'form.title.create': 'Nouveau coupon',
  'form.title.edit': 'Modifier le coupon',
  'form.field.code': 'Code',
  'form.field.code.hint': 'Lettres + chiffres uniquement, en majuscules',
  'form.field.type': 'Type de réduction',
  'form.field.value.percent': 'Pourcentage (%)',
  'form.field.value.fixed': 'Montant fixe (TND)',
  'form.field.validFrom': 'Valable du',
  'form.field.validTo': 'Au',
  'form.field.maxRedemptions': 'Limite redemptions (vide = illimité)',
  'form.field.durationMonths': 'Durée en mois (optionnel)',
  'form.field.appliesToPlan': 'Plan ciblé (vide = tous)',
  'form.field.notes': 'Notes (interne)',
  'form.confirm.create': 'Créer',
  'form.confirm.update': 'Enregistrer',

  'redemptions.title': 'Redemptions du coupon',
  'redemptions.col.tenant': 'Entreprise',
  'redemptions.col.redeemedAt': 'Utilisé le',
  'redemptions.col.amountSaved': 'Économie',

  'empty.title': 'Aucun coupon',
  'empty.desc': 'Créez votre premier coupon promotionnel.',

  'toast.create.success': 'Coupon créé',
  'toast.update.success': 'Coupon mis à jour',
  'toast.deactivate.success': 'Coupon désactivé',
  'toast.reactivate.success': 'Coupon réactivé',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type CouponsFrKey = keyof typeof COUPONS_FR;
