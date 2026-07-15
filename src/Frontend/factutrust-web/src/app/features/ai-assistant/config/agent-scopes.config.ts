import { AssistantAgentScope } from '../models/ai-chat.models';

/**
 * Source de vérité UI des assistants experts par module : identité, suggestions, préfixes de routes
 * pour l'auto-scope de la bulle flottante et slugs des pages dédiées (/ai-assistant/<slug>).
 * Le backend garde sa propre source de vérité (AiAgentScopeCatalog) pour outils et personas.
 */
export interface AgentScopeConfig {
  scope: AssistantAgentScope;
  /** Segment d'URL de la page dédiée : /ai-assistant/<slug>. */
  slug: string;
  /** Titre de page/onglet — « Assistant IA Ventes ». */
  title: string;
  /** Nom affiché dans le header du panneau — « Expert Ventes ». */
  expertName: string;
  /** Icône Font Awesome de l'entrée sidebar et du badge. */
  icon: string;
  /** Suggestions du scope affichées sur l'écran d'accueil du panneau. */
  suggestions: string[];
  /** Préfixes de routes du module (auto-scope de la bulle flottante). */
  routePrefixes: string[];
}

export const AGENT_SCOPE_CONFIGS: readonly AgentScopeConfig[] = [
  {
    scope: AssistantAgentScope.Sales,
    slug: 'ventes',
    title: 'Assistant IA Ventes',
    expertName: 'Expert Ventes',
    icon: 'fa-solid fa-bag-shopping',
    suggestions: [
      "Quel est mon chiffre d'affaires ce mois-ci ?",
      'Quels sont mes 5 meilleurs clients ce trimestre ?',
      'Quelles factures sont en retard de paiement ?',
      'Quels produits se vendent le mieux ?'
    ],
    routePrefixes: ['/quotes', '/delivery-notes', '/invoices']
  },
  {
    scope: AssistantAgentScope.Purchases,
    slug: 'achats',
    title: 'Assistant IA Achats',
    expertName: 'Expert Achats',
    icon: 'fa-solid fa-cart-shopping',
    suggestions: [
      'Quelles factures fournisseurs ai-je payées ce mois-ci ?',
      'Quels bons de commande sont en attente ?',
      'Quelles sont les recommandations de réapprovisionnement ?',
      'Quels sont mes soldes fournisseurs ?'
    ],
    routePrefixes: ['/suppliers', '/purchase-orders', '/supplier-invoices']
  },
  {
    scope: AssistantAgentScope.Stock,
    slug: 'stock',
    title: 'Assistant IA Stock',
    expertName: 'Expert Stock',
    icon: 'fa-solid fa-boxes-stacked',
    suggestions: [
      "Affiche-moi l'état du stock actuel",
      'Quels produits sont en rupture ou presque finis ?',
      'Quels produits ne se sont jamais vendus ?',
      'Montre-moi la classification ABC/XYZ de mes produits'
    ],
    routePrefixes: ['/stock', '/transfers', '/inventory']
  },
  {
    scope: AssistantAgentScope.Accounting,
    slug: 'comptabilite',
    title: 'Assistant IA Comptabilité',
    expertName: 'Expert Comptabilité',
    icon: 'fa-solid fa-calculator',
    suggestions: [
      'Montre-moi le tableau de bord comptable',
      'Quels sont mes soldes clients et fournisseurs ?',
      'Vérifie la conformité de ma dernière facture',
      'Quelles sont mes créances de plus de 90 jours ?'
    ],
    routePrefixes: ['/accounting']
  },
  {
    scope: AssistantAgentScope.Treasury,
    slug: 'tresorerie',
    title: 'Assistant IA Trésorerie',
    expertName: 'Expert Trésorerie',
    icon: 'fa-solid fa-credit-card',
    suggestions: [
      'Quels encaissements ai-je reçus ce mois-ci ?',
      'Quelles sont mes créances de plus de 90 jours ?',
      'Quelles factures fournisseurs arrivent à échéance ?',
      'Quels clients dois-je relancer en priorité ?'
    ],
    routePrefixes: ['/payments']
  },
  {
    scope: AssistantAgentScope.Crm,
    slug: 'crm',
    title: 'Assistant IA CRM',
    expertName: 'Expert CRM',
    icon: 'fa-solid fa-handshake',
    suggestions: [
      'Quelles opportunités sont ouvertes actuellement ?',
      'Quelles activités sont prévues cette semaine ?',
      'Quels clients dois-je relancer ?',
      'Résume mon pipeline commercial'
    ],
    routePrefixes: ['/crm']
  }
];

export function getAgentScopeConfig(scope: AssistantAgentScope): AgentScopeConfig | undefined {
  return AGENT_SCOPE_CONFIGS.find(c => c.scope === scope);
}

export function getAgentScopeConfigBySlug(slug: string): AgentScopeConfig | undefined {
  return AGENT_SCOPE_CONFIGS.find(c => c.slug === slug);
}

/** Vrai si `url` est la route `prefix` ou une de ses sous-routes (segments exacts, query string ignorée). */
function matchesPrefix(url: string, prefix: string): boolean {
  if (!url.startsWith(prefix)) {
    return false;
  }
  const rest = url.charAt(prefix.length);
  return rest === '' || rest === '/' || rest === '?' || rest === '#';
}

/**
 * Résout le scope expert depuis l'URL courante :
 * 1. `/ai-assistant/<slug>` → scope explicite de la page dédiée ;
 * 2. préfixe d'un module (ex. /invoices) → scope suggéré pour la bulle flottante ;
 * 3. sinon None (assistant global) — y compris `/ai-assistant` seul et /dashboard.
 */
export function resolveScopeFromUrl(url: string | null | undefined): AssistantAgentScope {
  if (!url) {
    return AssistantAgentScope.None;
  }
  const path = url.split('?')[0].split('#')[0];

  const aiPrefix = '/ai-assistant/';
  if (path.startsWith(aiPrefix)) {
    const slug = path.slice(aiPrefix.length).split('/')[0];
    return getAgentScopeConfigBySlug(slug)?.scope ?? AssistantAgentScope.None;
  }

  for (const config of AGENT_SCOPE_CONFIGS) {
    if (config.routePrefixes.some(prefix => matchesPrefix(path, prefix))) {
      return config.scope;
    }
  }
  return AssistantAgentScope.None;
}
