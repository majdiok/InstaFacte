/**
 * Phase 2 (WP-F7) — Libellés FR centralisés pour la page « Règles sectorielles ».
 *
 * ATTENTION nommage (cf. plan Phase 2) : la liste des entreprises possède déjà un filtre
 * « Segment » qui désigne le segment de FACTURATION (payant / non payant). Le nouveau concept
 * de cette page — secteur d'activité (commerce, services, BTP…) — doit toujours être nommé
 * « Secteur » dans les libellés utilisateur pour éviter toute confusion.
 */
export const SECTOR_RULES_FR = {
  'page.title': 'Règles sectorielles',
  'page.subtitle':
    'Gérez le catalogue des secteurs, domaines d’activité et règles de modules utilisés par l’assistant d’onboarding et la reconfiguration sectorielle des entreprises.',
  'page.actions.newSegment': 'Nouveau segment',
  'page.actions.resync': 'Resynchroniser depuis le catalogue',
  'page.actions.refresh': 'Rafraîchir',

  'banner.version': 'version {version}',

  'resync.title': 'Resynchroniser depuis le catalogue',
  'resync.desc':
    'Réinitialise les segments, domaines, règles de modules et paramètres par défaut à partir du catalogue statique embarqué dans le backend. Les enregistrements existants créés manuellement peuvent être écrasés si l’option "forcer" est activée.',
  'resync.confirmKeyword': 'RESYNC',
  'resync.confirmLabel': 'Resynchroniser',
  'resync.force': 'Forcer l’écrasement des enregistrements existants',

  'tab.segments': 'Segments',
  'tab.domains': 'Domaines',
  'tab.associations': 'Associations',
  'tab.moduleRules': 'Règles de modules',
  'tab.dependencies': 'Dépendances',
  'tab.settings': 'Paramètres',
  'tab.templates': 'Modèles de données',

  // ----- Segments ---------------------------------------------------------
  'segments.col.code': 'Code',
  'segments.col.label': 'Libellé',
  'segments.col.modules': 'Modules recommandés',
  'segments.col.warehouse': 'Entrepôt par défaut',
  'segments.col.order': 'Ordre',
  'segments.col.active': 'Actif',
  'segments.col.actions': 'Actions',
  'segments.empty.title': 'Aucun segment',
  'segments.empty.desc': 'Créez un premier segment ou resynchronisez depuis le catalogue.',
  'segments.action.edit': 'Modifier',
  'segments.action.deactivate': 'Désactiver',
  'segments.action.activate': 'Réactiver',
  'segments.form.title.create': 'Nouveau segment',
  'segments.form.title.edit': 'Modifier le segment',
  'segments.form.code': 'Code (kebab-case)',
  'segments.form.code.hint': 'Minuscules, chiffres et tirets uniquement (50 caractères max). Immuable après création.',
  'segments.form.label': 'Libellé',
  'segments.form.description': 'Description',
  'segments.form.icon': 'Icône (classe PrimeIcons)',
  'segments.form.order': 'Ordre d’affichage',
  'segments.form.warehouse': 'Nom d’entrepôt par défaut',
  'segments.form.active': 'Actif',
  'segments.confirm.create': 'Créer le segment',
  'segments.confirm.update': 'Enregistrer',
  'segments.toast.create.success': 'Segment créé',
  'segments.toast.update.success': 'Segment mis à jour',
  'segments.toast.deactivate.success': 'Segment désactivé',
  'segments.deactivate.title': 'Désactiver le segment',
  'segments.deactivate.desc': 'Le segment ne sera plus proposé à l’onboarding ni à la reconfiguration sectorielle.',

  // ----- Domaines -----------------------------------------------------------
  'domains.col.code': 'Code',
  'domains.col.label': 'Libellé',
  'domains.col.order': 'Ordre',
  'domains.col.active': 'Actif',
  'domains.col.actions': 'Actions',
  'domains.empty.title': 'Aucun domaine',
  'domains.empty.desc': 'Créez un premier domaine d’activité ou resynchronisez depuis le catalogue.',
  'domains.form.title.create': 'Nouveau domaine',
  'domains.form.title.edit': 'Modifier le domaine',
  'domains.form.code': 'Code (kebab-case)',
  'domains.form.code.hint': 'Minuscules, chiffres et tirets uniquement (50 caractères max). Immuable après création.',
  'domains.form.label': 'Libellé',
  'domains.form.order': 'Ordre d’affichage',
  'domains.form.active': 'Actif',
  'domains.action.edit': 'Modifier',
  'domains.action.deactivate': 'Désactiver',
  'domains.action.activate': 'Réactiver',
  'domains.confirm.create': 'Créer le domaine',
  'domains.confirm.update': 'Enregistrer',
  'domains.toast.create.success': 'Domaine créé',
  'domains.toast.update.success': 'Domaine mis à jour',
  'domains.toast.deactivate.success': 'Domaine désactivé',

  // ----- Associations --------------------------------------------------------
  'assoc.hint':
    'Cochez les domaines proposés pour chaque segment lors de l’onboarding. « Autre domaine » est toujours disponible et ne peut pas être décoché.',
  'assoc.col.domain': 'Domaine',
  'assoc.locked.title': 'Toujours disponible pour tous les segments',
  'assoc.actions.save': 'Enregistrer les associations',
  'assoc.toast.save.success': 'Associations enregistrées',
  'assoc.other.code': 'autre',

  // ----- Règles de modules -----------------------------------------------------
  'rules.segment.title': 'Base de modules par segment',
  'rules.segment.desc': 'Modules recommandés (en plus du socle commun toujours actif) pour chaque segment.',
  'rules.domain.title': 'Surcouche de modules par domaine',
  'rules.domain.desc':
    'Modules additionnels proposés lorsque ce domaine d’activité précis est sélectionné (en plus de la base du segment).',
  'rules.picker.segment': 'Segment',
  'rules.picker.domain': 'Domaine',
  'rules.dialog.title': 'Règle de modules — {name}',
  'rules.dialog.core.title': 'Modules de base du segment',
  'rules.dialog.core.hint': 'Les modules du socle commun sont toujours actifs et ne peuvent pas être retirés.',
  'rules.dialog.preview.title': 'Aperçu en direct',
  'rules.dialog.preview.live': 'Mis à jour en direct',
  'rules.dialog.preview.core': 'Socle',
  'rules.dialog.preview.recommended': 'Recommandés',
  'rules.dialog.preview.optional': 'Optionnels',
  'rules.dialog.dependency.note': 'Dépendance appliquée : « {module} » requiert « {requires} ».',
  'rules.edit': 'Modifier les modules',
  'rules.confirm.save': 'Enregistrer',
  'rules.toast.save.success': 'Règle de modules enregistrée',

  // ----- Dépendances -----------------------------------------------------------
  'deps.col.module': 'Module',
  'deps.col.requires': 'Requiert',
  'deps.col.actions': 'Actions',
  'deps.add.title': 'Ajouter une dépendance',
  'deps.add.module': 'Module',
  'deps.add.requires': 'Requiert le module',
  'deps.add.action': 'Ajouter',
  'deps.empty.title': 'Aucune dépendance',
  'deps.empty.desc': 'Les modules sont indépendants les uns des autres tant qu’aucune dépendance n’est déclarée.',
  'deps.action.delete': 'Supprimer',
  'deps.toast.create.success': 'Dépendance ajoutée',
  'deps.toast.delete.success': 'Dépendance supprimée',
  'deps.delete.title': 'Supprimer la dépendance',
  'deps.delete.desc': 'Cette dépendance ne sera plus vérifiée lors de la reconfiguration sectorielle.',

  // ----- Paramètres par défaut ----------------------------------------------------
  'settings.col.segment': 'Segment',
  'settings.col.domain': 'Domaine',
  'settings.col.key': 'Clé',
  'settings.col.type': 'Type',
  'settings.col.value': 'Valeur',
  'settings.col.actions': 'Actions',
  'settings.add.title': 'Nouveau paramètre',
  'settings.form.title.edit': 'Modifier le paramètre',
  'settings.empty.title': 'Aucun paramètre par défaut',
  'settings.empty.desc': 'Ex. « Nom d’entrepôt par défaut » par segment.',
  'settings.action.edit': 'Modifier',
  'settings.toast.save.success': 'Paramètre enregistré',
  'settings.form.segment': 'Segment (optionnel)',
  'settings.form.domain': 'Domaine (optionnel)',
  'settings.form.key': 'Clé',
  'settings.form.type': 'Type',
  'settings.form.value': 'Valeur',
  'settings.confirm.save': 'Enregistrer',

  // ----- Modèles de données --------------------------------------------------------
  'templates.col.code': 'Code',
  'templates.col.label': 'Libellé',
  'templates.col.segment': 'Segment',
  'templates.col.domain': 'Domaine',
  'templates.col.items': 'Éléments',
  'templates.col.actions': 'Actions',
  'templates.empty.title': 'Aucun modèle de données',
  'templates.empty.desc': 'Ex. numérotation de documents, plan comptable, paramètres par défaut.',
  'templates.action.view': 'Détails',
  'templates.dialog.title': 'Modèle de données — {code}',
  'templates.dialog.version': 'Version',
  'templates.dialog.item.kind': 'Type',
  'templates.dialog.item.payload': 'Contenu (JSON)',
  'templates.dialog.item.order': 'Ordre',
  'templates.confirm.save': 'Enregistrer',
  'templates.toast.save.success': 'Modèle de données mis à jour',

  'toast.error.title': 'Erreur',
  'toast.error.generic': 'Une erreur est survenue. Consultez la console (F12).',
  'toast.error.forbidden': 'Accès refusé : permission « platform.sector-rules:manage » requise.',

  'common.none': '—',
  'common.cancel': 'Annuler',
  'common.yes': 'Oui',
  'common.no': 'Non'
} as const satisfies Record<string, string>;

export type SectorRulesFrKey = keyof typeof SECTOR_RULES_FR;
