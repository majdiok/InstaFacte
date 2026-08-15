import { AssistantAgentScope } from '../models/ai-chat.models';
import {
  AGENT_SCOPE_CONFIGS,
  FIRM_AGENT_SCOPE_CONFIG,
  getAgentScopeConfig,
  getAgentScopeConfigBySlug,
  resolveScopeFromUrl
} from './agent-scopes.config';

const HISTORICAL_FIRM_SUGGESTIONS = [
  'Quelles échéances sont en retard et chez quels clients ?',
  'Quels dossiers sont les plus à risque cette semaine ?',
  'Comment se répartit la charge entre mes collaborateurs ?',
  "Quels dossiers n'ont pas eu d'écriture depuis un mois ?"
] as const;

describe('agent-scopes.config', () => {
  it('expose une config complète et unique pour les 6 experts', () => {
    expect(AGENT_SCOPE_CONFIGS.length).toBe(6);
    const slugs = new Set(AGENT_SCOPE_CONFIGS.map(c => c.slug));
    const scopes = new Set(AGENT_SCOPE_CONFIGS.map(c => c.scope));
    expect(slugs.size).toBe(6);
    expect(scopes.size).toBe(6);
    for (const config of AGENT_SCOPE_CONFIGS) {
      expect(config.scope).not.toBe(AssistantAgentScope.None);
      expect(config.title.length).toBeGreaterThan(0);
      expect(config.expertName.length).toBeGreaterThan(0);
      expect(config.suggestions.length).toBe(4);
      expect(config.routePrefixes.length).toBeGreaterThan(0);
    }
  });

  it('getAgentScopeConfig retourne undefined pour None (assistant global)', () => {
    expect(getAgentScopeConfig(AssistantAgentScope.None)).toBeUndefined();
    expect(getAgentScopeConfig(AssistantAgentScope.Sales)?.slug).toBe('ventes');
    expect(getAgentScopeConfigBySlug('comptabilite')?.scope).toBe(AssistantAgentScope.Accounting);
  });

  it('résout les pages dédiées /ai-assistant/<slug>', () => {
    expect(resolveScopeFromUrl('/ai-assistant/ventes')).toBe(AssistantAgentScope.Sales);
    expect(resolveScopeFromUrl('/ai-assistant/achats')).toBe(AssistantAgentScope.Purchases);
    expect(resolveScopeFromUrl('/ai-assistant/stock')).toBe(AssistantAgentScope.Stock);
    expect(resolveScopeFromUrl('/ai-assistant/comptabilite')).toBe(AssistantAgentScope.Accounting);
    expect(resolveScopeFromUrl('/ai-assistant/tresorerie')).toBe(AssistantAgentScope.Treasury);
    expect(resolveScopeFromUrl('/ai-assistant/crm')).toBe(AssistantAgentScope.Crm);
    expect(resolveScopeFromUrl('/ai-assistant/inconnu')).toBe(AssistantAgentScope.None);
  });

  it('résout les préfixes de routes des modules (auto-scope de la bulle)', () => {
    expect(resolveScopeFromUrl('/invoices')).toBe(AssistantAgentScope.Sales);
    expect(resolveScopeFromUrl('/invoices/unpaid?status=late')).toBe(AssistantAgentScope.Sales);
    expect(resolveScopeFromUrl('/quotes')).toBe(AssistantAgentScope.Sales);
    expect(resolveScopeFromUrl('/purchase-orders')).toBe(AssistantAgentScope.Purchases);
    expect(resolveScopeFromUrl('/supplier-invoices/unpaid')).toBe(AssistantAgentScope.Purchases);
    expect(resolveScopeFromUrl('/stock')).toBe(AssistantAgentScope.Stock);
    expect(resolveScopeFromUrl('/inventory/wizard')).toBe(AssistantAgentScope.Stock);
    expect(resolveScopeFromUrl('/accounting/journal')).toBe(AssistantAgentScope.Accounting);
    expect(resolveScopeFromUrl('/payments/cash-desk')).toBe(AssistantAgentScope.Treasury);
    expect(resolveScopeFromUrl('/crm/dashboard')).toBe(AssistantAgentScope.Crm);
  });

  it('retourne None pour le global, les routes hors module et les faux préfixes', () => {
    expect(resolveScopeFromUrl('/ai-assistant')).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl('/dashboard')).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl('/settings')).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl('/forecasting/revenue')).toBe(AssistantAgentScope.None);
    // Garde de segment exact : « /stockage » ne matche pas « /stock ».
    expect(resolveScopeFromUrl('/stockage')).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl(null)).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl(undefined)).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl('')).toBe(AssistantAgentScope.None);
  });

  it('force Accounting scope for firm delegated users regardless of route', () => {
    expect(resolveScopeFromUrl('/invoices', { firmDelegated: true })).toBe(AssistantAgentScope.Accounting);
    expect(resolveScopeFromUrl('/payments', { firmDelegated: true })).toBe(AssistantAgentScope.Accounting);
    expect(resolveScopeFromUrl('/ai-assistant/ventes', { firmDelegated: true })).toBe(
      AssistantAgentScope.Accounting
    );
  });

  it('isole le Chef de mission hors des experts de module (routes, slug, URL)', () => {
    expect(AGENT_SCOPE_CONFIGS.some(c => c.scope === AssistantAgentScope.FirmMission)).toBeFalse();
    expect(getAgentScopeConfig(AssistantAgentScope.FirmMission)).toBe(FIRM_AGENT_SCOPE_CONFIG);
    expect(getAgentScopeConfigBySlug('chef-de-mission')).toBeUndefined();
    expect(resolveScopeFromUrl('/firm/assistant')).toBe(AssistantAgentScope.None);
    expect(resolveScopeFromUrl('/ai-assistant/chef-de-mission')).toBe(AssistantAgentScope.None);
  });

  it('ne catégorise pas les suggestions des 6 experts de module', () => {
    for (const config of AGENT_SCOPE_CONFIGS) {
      expect(config.suggestionCategories).toBeUndefined();
    }
  });

  it('catalogue Chef de mission : catégories cohérentes, flatten = suggestions, questions historiques conservées', () => {
    const categories = FIRM_AGENT_SCOPE_CONFIG.suggestionCategories;
    expect(categories).toBeDefined();
    expect(categories!.map(c => c.id)).toEqual(['overview', 'deadlines', 'risk', 'workload', 'review']);

    const ids = categories!.map(c => c.id);
    expect(new Set(ids).size).toBe(ids.length);

    for (const category of categories!) {
      expect(category.id.length).toBeGreaterThan(0);
      expect(category.label.length).toBeGreaterThan(0);
      expect(category.questions.length).toBeGreaterThan(0);
    }

    const flattened = categories!.flatMap(c => [...c.questions]);
    expect(flattened.length).toBe(19);
    expect(FIRM_AGENT_SCOPE_CONFIG.suggestions).toEqual(flattened);
    expect(new Set(flattened).size).toBe(flattened.length);

    for (const historical of HISTORICAL_FIRM_SUGGESTIONS) {
      expect(flattened).toContain(historical);
    }
  });
});
