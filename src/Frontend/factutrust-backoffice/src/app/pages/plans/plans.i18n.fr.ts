/**
 * Lot C1 — Libellés FR centralisés pour la page Plans.
 */
export const PLANS_FR = {
  'list.title': 'Plans tarifaires',
  'list.subtitle':
    'Gérez les plans d’abonnement proposés aux entreprises. Chaque plan définit ses propres limites (factures/mois, stockage, utilisateurs), ses fonctionnalités (signature électronique, export XML, support prioritaire) et la liste des modules accessibles.',
  'list.actions.refresh': 'Rafraîchir',
  'list.actions.includeArchived': 'Afficher les archivés',

  'card.subscriptions': '{count} abonnement(s)',
  'card.subscriptions.zero': 'Aucun abonnement',
  'card.trial.days': '{count} j d’essai',
  'card.modules.included': '{count} module(s) inclus',
  'card.archived': 'Archivé',
  'card.public': 'Public',
  'card.private': 'Privé',

  'section.limits': 'Limites',
  'section.features': 'Fonctionnalités',
  'section.modules': 'Modules accessibles',

  'feature.enabled': 'Activée',
  'feature.disabled': 'Désactivée',
  'module.included': 'Inclus',
  'module.excluded': 'Exclu',

  'action.create': 'Nouveau plan',
  'action.edit': 'Modifier',
  'action.clone': 'Cloner',
  'action.archive': 'Archiver',
  'action.reactivate': 'Réactiver',

  'form.title.create': 'Nouveau plan tarifaire',
  'form.title.edit': 'Modifier le plan',
  'form.tab.general': 'Général',
  'form.tab.limits': 'Limites',
  'form.tab.features': 'Fonctionnalités',
  'form.tab.modules': 'Modules',

  'form.field.code': 'Code (clé)',
  'form.field.code.hint': 'Lettres + chiffres + tirets uniquement, en majuscules. Immuable après création.',
  'form.field.name': 'Nom affiché',
  'form.field.description': 'Description',
  'form.field.billingPeriod': 'Périodicité',
  'form.field.basePriceTND': 'Prix de base (TND)',
  'form.field.trialDays': 'Jours d’essai',
  'form.field.sortOrder': 'Ordre d’affichage',
  'form.field.currency': 'Devise (ISO)',
  'form.field.isPublic': 'Plan visible publiquement aux nouveaux tenants',

  'form.limits.add': 'Ajouter une limite',
  'form.limits.hint': 'Clés conventionnelles : MaxInvoicesPerMonth, MaxStorageBytes, MaxUsers. Valeur "∞" pour illimité.',
  'form.limits.key': 'Clé',
  'form.limits.value': 'Valeur',
  'form.limits.empty': 'Aucune limite — cliquez « Ajouter une limite »',

  'form.features.add': 'Ajouter une fonctionnalité',
  'form.features.hint': 'Clés conventionnelles : ElectronicSignature, XmlExport, PaymentTracking, PrioritySupport.',
  'form.features.key': 'Clé',
  'form.features.enabled': 'Activée',
  'form.features.empty': 'Aucune fonctionnalité — cliquez « Ajouter une fonctionnalité »',

  'form.modules.hint': 'Cochez les modules accessibles aux tenants abonnés à ce plan. Décocher restreint l’accès UI + API.',

  'form.confirm.create': 'Créer le plan',
  'form.confirm.update': 'Enregistrer',

  'toast.create.success': 'Plan créé',
  'toast.update.success': 'Plan mis à jour',

  'clone.title': 'Cloner un plan',
  'clone.intro':
    'Crée une copie privée du plan source (limites, fonctionnalités et modules). Le nouveau plan est créé en mode <em>Privé</em> — vous pourrez le rendre public après vérification.',
  'clone.field.newCode': 'Code (unique, sans espaces)',
  'clone.field.newName': 'Nom affiché',
  'clone.confirm': 'Cloner',

  'archive.title': 'Archiver le plan',
  'archive.desc':
    'Le plan ne sera plus proposé aux nouveaux abonnements. Les abonnements existants continuent de fonctionner.',
  'archive.confirmKeyword': 'ARCHIVER',
  'archive.confirmLabel': 'Archiver',

  'empty.title': 'Aucun plan',
  'empty.desc': 'Aucun plan correspondant. Le seed initial crée Free / Monthly / Annual.',

  'toast.clone.success': 'Plan cloné',
  'toast.archive.success': 'Plan archivé',
  'toast.reactivate.success': 'Plan réactivé',
  'toast.error.title': 'Erreur',
  'toast.error.detail': 'Impossible de contacter l’API.',
  'toast.error.networkUnreachable': 'API injoignable. Vérifiez que le backend est démarré (https://localhost:7001) et consultez la console (F12).',
  'toast.error.sessionExpired': 'Session expirée. Veuillez vous reconnecter.',
  'toast.error.forbidden': 'Accès refusé : permission « platform.plans:manage » manquante.',
  'toast.error.notFound': 'Ressource introuvable (endpoint /api/platform/plans).',
  'toast.error.server': 'Erreur serveur. Consultez les logs API.',
  'toast.error.badRequest': 'Requête invalide.'
} as const satisfies Record<string, string>;

export type PlansFrKey = keyof typeof PLANS_FR;
