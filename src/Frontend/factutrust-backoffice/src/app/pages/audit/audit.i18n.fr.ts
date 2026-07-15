/**
 * Libellés FR centralisés pour la page Audit Log Viewer (Lot B3).
 *
 * Vocabulaire FR-TN figé : `journal d'audit`, `intégrité de la chaîne`,
 * `entrée d'audit`, `vérification SHA-256`.
 */

export const AUDIT_FR = {
  // ----- Page header -------------------------------------------------------
  'list.title': 'Journal d’audit',
  'list.subtitle':
    'Consultez les actions auditées d’une entreprise. Chaque entrée est chaînée en SHA-256 pour garantir son intégrité (toute modification est détectable).',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.exportCsv': 'Exporter CSV',
  'list.actions.exportPdf': 'Exporter PDF',

  // ----- Tenant picker -----------------------------------------------------
  'picker.label': 'Entreprise ciblée',
  'picker.placeholder': 'Sélectionnez une entreprise…',
  'picker.empty.title': 'Sélectionnez une entreprise',
  'picker.empty.desc':
    'Le journal d’audit est stocké par entreprise. Choisissez celle dont vous voulez consulter les actions.',

  // ----- KPIs --------------------------------------------------------------
  'kpi.total': 'Entrées',
  'kpi.total.hint': 'Total dans la chaîne',
  'kpi.integrity': 'Intégrité',
  'kpi.integrity.hint': 'Chaîne SHA-256',
  'kpi.duplicates': 'Doublons hash',
  'kpi.duplicates.hint': 'Avant correction',

  // ----- Intégrité ---------------------------------------------------------
  'integrity.valid.title': 'Chaîne d’audit intègre',
  'integrity.valid.desc': 'Toutes les entrées passent la vérification SHA-256.',
  'integrity.broken.title': 'Anomalie détectée',
  'integrity.broken.desc.IntegrityMismatch':
    'Le hash stocké d’une entrée ne correspond pas à son contenu (modification a posteriori suspectée).',
  'integrity.broken.desc.ChainMismatch':
    'Le maillon précédent d’une entrée est cassé (insertion/suppression dans la chaîne).',
  'integrity.broken.firstId': 'Première entrée fautive',
  'integrity.checking': 'Vérification en cours…',
  'integrity.refresh': 'Revérifier',

  // ----- Filtres -----------------------------------------------------------
  'filters.from': 'Depuis',
  'filters.to': 'Jusqu’au',
  'filters.action': 'Action',
  'filters.actionPlaceholder': 'Ex: Invoice.Created',
  'filters.userId': 'Utilisateur (UUID)',
  'filters.entityType': 'Type d’entité',
  'filters.entityTypePlaceholder': 'Ex: Invoice, Client, Product',
  'filters.reset': 'Réinitialiser',

  // ----- Table columns -----------------------------------------------------
  'col.dateTime': 'Date / Heure',
  'col.user': 'Utilisateur',
  'col.action': 'Action',
  'col.entity': 'Entité',
  'col.entityId': 'ID',
  'col.actions': 'Actions',
  'col.action.viewDetails': 'Voir détails',

  // ----- Empty / error states ----------------------------------------------
  'empty.title': 'Aucune entrée d’audit',
  'empty.desc': 'Aucune action correspondant aux filtres pour cette entreprise.',
  'error.title': 'Erreur',
  'error.detail': 'Impossible de charger les audit logs.',

  // ----- Détail modal ------------------------------------------------------
  'detail.title': 'Détail de l’entrée d’audit',
  'detail.section.identity': 'Identité',
  'detail.section.payload': 'Modification',
  'detail.section.integrity': 'Intégrité',
  'detail.field.id': 'ID',
  'detail.field.createdAt': 'Date',
  'detail.field.user': 'Utilisateur',
  'detail.field.email': 'Email',
  'detail.field.action': 'Action',
  'detail.field.entityType': 'Type d’entité',
  'detail.field.entityId': 'ID de l’entité',
  'detail.field.entityLabel': 'Libellé',
  'detail.field.ipAddress': 'Adresse IP',
  'detail.field.userAgent': 'User-Agent',
  'detail.field.previousHash': 'Hash précédent',
  'detail.field.hash': 'Hash courant',
  'detail.oldValues': 'Valeurs avant',
  'detail.newValues': 'Valeurs après',
  'detail.noPayload': 'Aucun payload',
  'detail.actionViewEntity': 'Voir l’entité',

  // ----- Toasts ------------------------------------------------------------
  'toast.export.success': 'Export téléchargé',
  'toast.export.fail': 'Export impossible'
} as const satisfies Record<string, string>;

export type AuditFrKey = keyof typeof AUDIT_FR;
