/**
 * Lot B4 — Libellés FR centralisés pour la page Sécurité.
 * Vocabulaire : `session active`, `tentative échouée`, `révoquer`, `IP suspecte`.
 */

export const SECURITY_FR = {
  // ----- Header ------------------------------------------------------------
  'list.title': 'Sessions & sécurité',
  'list.subtitle':
    'Visualisez les sessions actives des admins et auditez les tentatives de connexion échouées. Détection automatique de brute-force par IP sur la dernière heure.',
  'list.actions.refresh': 'Rafraîchir',

  // ----- Tabs --------------------------------------------------------------
  'tab.sessions': 'Sessions actives',
  'tab.failedLogins': 'Tentatives échouées',

  // ----- Sessions tab -----------------------------------------------------
  'sessions.kpi.total': 'Sessions',
  'sessions.kpi.total.hint': 'Total dans la fenêtre',
  'sessions.kpi.active': 'Actives',
  'sessions.kpi.active.hint': 'Non révoquées + non expirées',
  'sessions.kpi.revoked': 'Révoquées',
  'sessions.kpi.revoked.hint': 'Manuellement ou expirées',

  'sessions.filter.activeOnly': 'État',
  'sessions.filter.allStates': 'Tous',
  'sessions.filter.active': 'Actives uniquement',
  'sessions.filter.revoked': 'Révoquées uniquement',

  'sessions.col.user': 'Utilisateur',
  'sessions.col.ip': 'IP',
  'sessions.col.userAgent': 'Navigateur',
  'sessions.col.issuedAt': 'Émise le',
  'sessions.col.expiresAt': 'Expire le',
  'sessions.col.state': 'État',
  'sessions.col.actions': 'Actions',

  'sessions.state.active': 'Active',
  'sessions.state.revoked': 'Révoquée',
  'sessions.state.expired': 'Expirée',

  'sessions.action.revoke': 'Révoquer',
  'sessions.action.revokeAll': 'Révoquer toutes ses sessions',

  'sessions.empty.title': 'Aucune session',
  'sessions.empty.desc': 'Aucune session ne correspond aux filtres.',

  'sessions.confirm.revokeTitle': 'Révoquer cette session ?',
  'sessions.confirm.revokeDesc':
    'L’utilisateur sera déconnecté immédiatement et devra se ré-authentifier.',
  'sessions.confirm.revokeAllTitle': 'Révoquer toutes les sessions de l’utilisateur ?',
  'sessions.confirm.revokeAllDesc':
    'Toutes les sessions actives de cet utilisateur seront invalidées. Il devra se reconnecter sur tous ses appareils.',
  'sessions.confirm.confirmKeyword': 'REVOQUER',
  'sessions.confirm.confirmLabel': 'Révoquer',

  // ----- Failed logins tab ------------------------------------------------
  'failed.kpi.total': 'Total tentatives',
  'failed.kpi.total.hint': 'Sur la fenêtre filtrée',
  'failed.kpi.last24h': 'Sur 24 h',
  'failed.kpi.last24h.hint': 'Tous emails confondus',
  'failed.kpi.lastHour': 'Sur 1 h',
  'failed.kpi.lastHour.hint': 'Brute-force possible',

  'failed.alert.bruteForce.title': 'IP suspectes détectées',
  'failed.alert.bruteForce.desc':
    'Les IP suivantes ont fait au moins 10 tentatives échouées sur la dernière heure :',

  'failed.filters.email': 'Email',
  'failed.filters.ip': 'Adresse IP',
  'failed.filters.from': 'Depuis',
  'failed.filters.to': 'Jusqu’au',
  'failed.filters.reset': 'Réinitialiser',

  'failed.col.attemptAt': 'Date',
  'failed.col.email': 'Email',
  'failed.col.ip': 'IP',
  'failed.col.reason': 'Motif',
  'failed.col.userAgent': 'Navigateur',

  'failed.empty.title': 'Aucune tentative échouée',
  'failed.empty.desc': 'Aucune tentative ne correspond aux filtres.',

  // ----- Toasts ------------------------------------------------------------
  'toast.revoke.success': 'Session révoquée',
  'toast.revokeAll.success': 'Sessions révoquées',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type SecurityFrKey = keyof typeof SECURITY_FR;
