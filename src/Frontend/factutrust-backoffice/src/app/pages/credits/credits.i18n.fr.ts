/**
 * Lot C3 — Libellés FR pour la page Crédits tenant.
 */
export const CREDITS_FR = {
  'list.title': 'Crédits tenant',
  'list.subtitle':
    'Accordez des crédits compensatoires aux entreprises (avoir, dédommagement, parrainage). Les crédits sont consommés en priorité par date d’expiration croissante lors de la génération de facture.',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.grant': 'Accorder un crédit',

  'kpi.total': 'Total crédits',
  'kpi.active': 'Actifs',
  'kpi.granted': 'Total accordé (TND)',
  'kpi.remaining': 'Solde restant (TND)',

  'filter.tenant': 'Filtrer par entreprise',
  'filter.all': 'Tous tenants',
  'filter.activeOnly': 'État',
  'filter.activeOnlyTrue': 'Actifs uniquement',
  'filter.allStates': 'Tous',

  'col.tenant': 'Entreprise',
  'col.amount': 'Montant',
  'col.consumed': 'Consommé',
  'col.remaining': 'Solde',
  'col.reason': 'Raison',
  'col.granted': 'Accordé le',
  'col.expires': 'Expire le',
  'col.state': 'État',
  'col.actions': 'Actions',

  'state.active': 'Actif',
  'state.expired': 'Expiré',
  'state.consumed': 'Consommé',
  'state.revoked': 'Révoqué',

  'action.revoke': 'Révoquer',

  'grant.title': 'Accorder un crédit',
  'grant.intro':
    'Les crédits sont consommés automatiquement par la génération de factures. La révocation est possible tant qu’ils ne sont pas (entièrement) consommés.',
  'grant.field.tenant': 'Entreprise',
  'grant.field.amount': 'Montant (TND)',
  'grant.field.reason': 'Raison',
  'grant.field.expiresAt': 'Date d’expiration (optionnel)',
  'grant.confirm': 'Accorder',

  'revoke.title': 'Révoquer ce crédit',
  'revoke.desc': 'Le crédit sera marqué comme révoqué et ne pourra plus être consommé. Les éventuels montants déjà consommés ne sont pas annulés.',
  'revoke.field.reason': 'Motif de révocation',
  'revoke.confirm': 'Révoquer',

  'empty.title': 'Aucun crédit',
  'empty.desc': 'Accordez le premier crédit à une entreprise.',

  'toast.grant.success': 'Crédit accordé',
  'toast.revoke.success': 'Crédit révoqué',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type CreditsFrKey = keyof typeof CREDITS_FR;
