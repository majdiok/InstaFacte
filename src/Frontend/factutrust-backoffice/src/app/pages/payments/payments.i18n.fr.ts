/** Lot C5 — Libellés FR pour la page Providers paiement + audit intents. */
export const PAYMENTS_FR = {
  'list.title': 'Providers paiement',
  'list.subtitle':
    'Configurez les passerelles de paiement (Konnect Network, Paymee.tn, virement bancaire) et leurs secrets API. Les webhooks signés HMAC alimentent automatiquement les reçus InstaFact.',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.viewIntents': 'Voir les intentions',

  'kpi.total': 'Providers',
  'kpi.enabled': 'Activés',
  'kpi.testMode': 'En mode test',
  'kpi.intents': 'Intentions',

  'col.provider': 'Provider',
  'col.status': 'État',
  'col.testMode': 'Mode test',
  'col.secrets': 'Secrets API',
  'col.webhook': 'Secret webhook',
  'col.updatedAt': 'Modifié',
  'col.actions': 'Actions',

  'state.enabled': 'Activé',
  'state.disabled': 'Désactivé',
  'state.test': 'Test',
  'state.live': 'Production',

  'secrets.set': 'Configurés',
  'secrets.missing': 'Manquants',

  'action.configure': 'Configurer',

  'edit.title': 'Configurer {{provider}}',
  'edit.field.displayName': 'Nom affiché',
  'edit.field.isEnabled': 'Activer ce provider',
  'edit.field.isTestMode': 'Mode test (sandbox)',
  'edit.field.allowedReturnDomain': 'Domaine autorisé pour returnUrl',
  'edit.field.secretsJson': 'Secrets API (JSON)',
  'edit.field.secretsJson.hint':
    'Ex.: { "apiKey": "...", "merchantId": "..." }. Chiffré côté serveur. Laissez vide pour conserver l’existant.',
  'edit.field.webhookSecret': 'Secret webhook (HMAC)',
  'edit.field.webhookSecret.hint':
    'Phrase secrète partagée avec le provider pour valider la signature HMAC SHA-256 des webhooks. Laissez vide pour conserver.',
  'edit.confirm': 'Enregistrer',

  'intents.title': 'Intentions de paiement',
  'intents.subtitle':
    'Audit des tentatives de paiement (réussies, échouées, en attente). Chaque ligne représente une intention initiée par un tenant pour régler une facture plateforme.',
  'intents.col.invoice': 'Facture',
  'intents.col.tenant': 'Entreprise',
  'intents.col.provider': 'Provider',
  'intents.col.amount': 'Montant',
  'intents.col.status': 'État',
  'intents.col.providerRef': 'Référence',
  'intents.col.createdAt': 'Créée le',
  'intents.col.completedAt': 'Finalisée le',

  'intents.kpi.total': 'Total',
  'intents.kpi.succeeded': 'Réussies',
  'intents.kpi.pending': 'En attente',
  'intents.kpi.failed': 'Échouées',
  'intents.kpi.amount': 'Encaissé (TND)',

  'intents.filter.provider': 'Provider',
  'intents.filter.providerAll': 'Tous',
  'intents.filter.status': 'État',
  'intents.filter.statusAll': 'Tous',

  'status.created': 'Créée',
  'status.redirect': 'Redirigée',
  'status.pending': 'En attente',
  'status.succeeded': 'Réussie',
  'status.failed': 'Échouée',
  'status.cancelled': 'Annulée',
  'status.refunded': 'Remboursée',

  'empty.title': 'Aucune intention',
  'empty.desc': 'Aucun paiement n’a encore été initié.',

  'toast.update.success': 'Provider mis à jour',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type PaymentsFrKey = keyof typeof PAYMENTS_FR;
