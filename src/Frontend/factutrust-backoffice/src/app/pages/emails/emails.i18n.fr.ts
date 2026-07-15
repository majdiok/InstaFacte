/**
 * Lot C2 — Libellés FR pour la page Emails (log d'envoi).
 */
export const EMAILS_FR = {
  'list.title': 'Journal des emails',
  'list.subtitle':
    'Consultez tous les emails envoyés par la plateforme (invitations, bienvenue, etc.). Chaque ligne contient le rendu HTML final pour audit. Configuration SMTP modifiable dans appsettings.json (section "Smtp").',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.sendTest': 'Envoyer un test',

  'kpi.total': 'Total filtré',
  'kpi.queued': 'En file',
  'kpi.sent': 'Envoyés',
  'kpi.failed': 'Échecs',
  'kpi.last24h': 'Dernières 24 h',

  'filter.search': 'Rechercher (email, sujet, template)',
  'filter.status': 'Statut',
  'filter.allStatuses': 'Tous statuts',
  'status.queued': 'En file',
  'status.sending': 'Envoi en cours',
  'status.sent': 'Envoyé',
  'status.failed': 'Échec',
  'status.bounced': 'Rejeté',
  'status.cancelled': 'Annulé',
  'status.skipped': 'Ignoré (SMTP désactivé)',

  'col.createdAt': 'Date',
  'col.to': 'Destinataire',
  'col.template': 'Template',
  'col.subject': 'Sujet',
  'col.status': 'Statut',
  'col.attempts': 'Tentatives',
  'col.actions': 'Actions',

  'action.view': 'Voir le détail',
  'action.retry': 'Renvoyer',

  'empty.title': 'Aucun email',
  'empty.desc':
    'Aucun email correspondant aux filtres. Les emails sont créés par les actions admin (invitations, etc.).',

  'detail.title': 'Détail email',
  'detail.section.header': 'Informations',
  'detail.section.preview': 'Rendu HTML',
  'detail.section.error': 'Erreur',
  'detail.field.from': 'De',
  'detail.field.to': 'À',
  'detail.field.subject': 'Sujet',
  'detail.field.template': 'Template',
  'detail.field.status': 'Statut',
  'detail.field.created': 'Créé le',
  'detail.field.sent': 'Envoyé le',
  'detail.field.attempts': 'Tentatives',
  'detail.field.providerId': 'ID provider',

  'sendTest.title': 'Envoyer un email de test',
  'sendTest.intro':
    'Vérifie que la configuration SMTP est fonctionnelle en émettant un email simple. Un message sera créé en file ; consultez le journal pour voir le résultat.',
  'sendTest.field.to': 'Adresse de destination',
  'sendTest.confirm': 'Envoyer',

  'toast.retry.success': 'Email remis en file',
  'toast.sendTest.success': 'Email de test mis en file',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type EmailsFrKey = keyof typeof EMAILS_FR;
