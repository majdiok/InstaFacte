/** Lot C6 — Libellés FR pour la page Dunning. */
export const DUNNING_FR = {
  'page.title': 'Renouvellement & Dunning',
  'page.subtitle':
    'Pilotez les campagnes de relance impayés (rappels e-mail, passage en PastDue, suspension automatique) et les renouvellements d’abonnements. Workers Hangfire quotidiens.',
  'page.actions.refresh': 'Rafraîchir',

  'tab.states': 'Cycles en cours',
  'tab.campaigns': 'Campagnes',

  'kpi.active': 'En cours',
  'kpi.paid': 'Payés',
  'kpi.suspended': 'Suspendus',
  'kpi.giveup': 'Abandonnés',

  'states.col.tenant': 'Entreprise',
  'states.col.invoice': 'Facture',
  'states.col.dueDate': 'Échéance',
  'states.col.step': 'Étape courante',
  'states.col.nextAction': 'Prochaine action',
  'states.col.attempts': 'Tentatives',
  'states.col.outcome': 'Issue',
  'states.col.actions': 'Actions',

  'states.filter.outcomeAll': 'Toutes les issues',
  'states.filter.outcomeActive': 'En cours',
  'states.filter.outcomePaid': 'Payés',
  'states.filter.outcomeSuspended': 'Suspendus',
  'states.filter.outcomeGiveup': 'Abandonnés',

  'states.action.renewNow': 'Renouveler',
  'states.action.extendGrace': 'Étendre délai',

  'states.empty.title': 'Aucun cycle dunning',
  'states.empty.desc':
    'Aucun cycle de relance n’est en cours. Le job Hangfire scanne les factures impayées chaque jour à 05h UTC.',

  'campaigns.kpi.total': 'Total',
  'campaigns.kpi.active': 'Active',

  'campaigns.col.name': 'Nom',
  'campaigns.col.steps': 'Étapes',
  'campaigns.col.state': 'État',
  'campaigns.col.actions': 'Actions',

  'campaigns.action.activate': 'Activer',
  'campaigns.action.deactivate': 'Désactiver',
  'campaigns.action.edit': 'Modifier',
  'campaigns.action.create': 'Nouvelle campagne',

  'campaigns.state.active': 'Active',
  'campaigns.state.inactive': 'Inactive',

  'campaigns.empty.title': 'Aucune campagne',
  'campaigns.empty.desc':
    'Créez une campagne dunning ou utilisez la campagne par défaut (J+1 / J+3 / J+7 / J+14).',

  'form.title.create': 'Nouvelle campagne dunning',
  'form.title.edit': 'Modifier la campagne',
  'form.field.name': 'Nom',
  'form.field.description': 'Description',
  'form.field.activate': 'Activer immédiatement',
  'form.section.steps': 'Étapes (chronologiques)',
  'form.steps.add': 'Ajouter une étape',
  'form.steps.remove': 'Retirer',
  'form.steps.day': 'Jour',
  'form.steps.action': 'Action',
  'form.steps.template': 'Code template e-mail',
  'form.steps.label': 'Libellé',
  'form.confirm.create': 'Créer',
  'form.confirm.update': 'Enregistrer',

  'action.email': 'E-mail',
  'action.markPastDue': 'Marquer impayé',
  'action.suspend': 'Suspendre',

  'extend.title': 'Étendre le délai de grâce',
  'extend.field.days': 'Nombre de jours (1–30)',
  'extend.confirm': 'Étendre',

  'toast.create.success': 'Campagne créée',
  'toast.update.success': 'Campagne mise à jour',
  'toast.activate.success': 'Campagne activée',
  'toast.deactivate.success': 'Campagne désactivée',
  'toast.renew.success': 'Abonnement renouvelé',
  'toast.extend.success': 'Délai étendu',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.'
} as const satisfies Record<string, string>;

export type DunningFrKey = keyof typeof DUNNING_FR;
