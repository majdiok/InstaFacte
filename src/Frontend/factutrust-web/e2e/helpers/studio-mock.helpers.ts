import { Page, Route } from '@playwright/test';

/**
 * Mocks Playwright pour l'atelier Studio IA (`/studio/ai`) : session authentifiée injectée dans le
 * `localStorage` + routes `/api/**` interceptées. Aucun backend .NET / SQL Server n'est requis :
 * les suites `e2e/studio-*.spec.ts` sont déterministes et indépendantes des PR backend.
 *
 * Ordre d'enregistrement : Playwright évalue les routes de la plus récente à la plus ancienne ;
 * le « filet » `**\/api/**` est donc enregistré en premier et les routes précises ensuite.
 */

export interface StudioCapabilitiesOverrides {
  planPreviewEnabled?: boolean;
  systemGenerationEnabled?: boolean;
  modifyToolsEnabled?: boolean;
  viewToolsEnabled?: boolean;
  reportToolsEnabled?: boolean;
  workbenchEnabled?: boolean;
  templatesEnabled?: boolean;
  pagesEnabled?: boolean;
  advancedModelAvailable?: boolean;
  standardModelLabel?: string;
  advancedModelLabel?: string | null;
  manyToManyEnabled?: boolean;
  recordViewsEnabled?: boolean;
  recordViewToolsEnabled?: boolean;
  systemExportEnabled?: boolean;
  workflowsEnabled?: boolean;
  workflowToolsEnabled?: boolean;
}

/** Capacités « atelier complet » (tous les drapeaux P0/P1 activés, modèle avancé configuré). */
export const STUDIO_CAPABILITIES_ALL_ENABLED: Required<StudioCapabilitiesOverrides> = {
  planPreviewEnabled: true,
  systemGenerationEnabled: true,
  modifyToolsEnabled: true,
  viewToolsEnabled: true,
  reportToolsEnabled: true,
  workbenchEnabled: true,
  templatesEnabled: true,
  pagesEnabled: false,
  advancedModelAvailable: true,
  standardModelLabel: 'Mistral 7B',
  advancedModelLabel: 'GPT-4.1',
  manyToManyEnabled: false,
  recordViewsEnabled: false,
  recordViewToolsEnabled: false,
  systemExportEnabled: false,
  workflowsEnabled: false,
  workflowToolsEnabled: false
};

/** Un `data:` SSE par événement, tel que `AiStreamService` le lit (`type` + `content` JSON). */
export interface SseEvent {
  type: string;
  content?: string;
  conversationId?: string;
  [k: string]: unknown;
}

export interface StudioPlanSummaryMock {
  kind: string;
  title: string;
  steps: { key: string; label: string; detail: string }[];
  entities: { displayName: string; fieldCount: number; relationCount: number; existingKey?: string | null }[];
  warnings: string[];
  duplicates: { specRef: string; specDisplayName: string; existingKey: string; existingDisplayName: string; reason: string }[];
}

export interface StudioMockOptions {
  capabilities?: StudioCapabilitiesOverrides | null;
  /** Événements SSE renvoyés par `POST /api/ai/chat` (par défaut : `meta` avancé + `content` + `done`). */
  chatEvents?: SseEvent[];
  /** Historique renvoyé par `GET /api/studio/ai/plans`. */
  plans?: StudioPlanListItemMock[];
  templates?: StudioTemplateMock[];
}

export interface StudioPlanListItemMock {
  id: string;
  kind: string;
  status: string;
  title: string;
  entityCount: number;
  createdAt: string;
  expiresAt: string;
  executedAt?: string | null;
  systemKey?: string | null;
}

export interface StudioTemplateMock {
  key: string;
  displayName: string;
  description: string;
  category: string;
  moduleTag: string;
  source: string;
  entityCount: number;
}

/** Trace des appels API interceptés (méthode + URL + corps JSON éventuel) pour les assertions. */
export interface RecordedCall {
  method: string;
  url: string;
  body: unknown;
}

export interface StudioMockContext {
  calls: RecordedCall[];
  /** Appels correspondant à un motif (sous-chaîne de l'URL) et éventuellement une méthode. */
  find(urlPart: string, method?: string): RecordedCall[];
  /** Remplace les événements SSE du prochain `POST /api/ai/chat`. */
  setChatEvents(events: SseEvent[]): void;
}

const FAKE_JWT = (() => {
  const b64 = (o: unknown) => Buffer.from(JSON.stringify(o)).toString('base64url');
  return `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64({ sub: 'u-e2e', exp: 4102444800 })}.sig`;
})();

export const STUDIO_E2E_USER = {
  id: 'u-e2e',
  email: 'studio.e2e@instafact.test',
  firstName: 'Studio',
  lastName: 'E2E',
  fullName: 'Studio E2E',
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: 't-e2e',
  companyName: 'InstaFact E2E',
  tenantKind: 'company',
  accessMode: 'native',
  twoFactorEnabled: false,
  // Tous les modules (AppModule 0-17) : `moduleGuard` exige AppModule.Studio (13) sur /studio/**.
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17],
  effectivePermissions: ['studio:design_entities', 'studio:design_forms', 'studio:design_reports', 'custom_records:read'],
  productOnboardingStatus: 'completed'
};

const ok = <T>(data: T) => ({ success: true, data, message: null, errors: [] });

async function fulfilJson(route: Route, data: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) });
}

function sseBody(events: SseEvent[]): string {
  return events.map(e => `data: ${JSON.stringify(e)}\n\n`).join('');
}

export function studioPlanSummary(overrides: Partial<StudioPlanSummaryMock> = {}): StudioPlanSummaryMock {
  return {
    kind: 'CreateSystem',
    title: 'Gestion des congés',
    steps: [
      { key: 'system', label: 'Système', detail: 'Gestion des congés' },
      { key: 'entities', label: 'Tables', detail: '2 tables' }
    ],
    entities: [
      { displayName: 'Employé', fieldCount: 4, relationCount: 0 },
      { displayName: 'Demande de congé', fieldCount: 5, relationCount: 1 }
    ],
    warnings: [],
    duplicates: [],
    ...overrides
  };
}

/** Spec canonique alignée sur `studioPlanSummary()` (renvoyée par `GET {id}/spec`). */
export function studioSystemSpec(existingKey: string | null = null) {
  return {
    system: { displayName: 'Gestion des congés', description: 'Suivi des congés' },
    entities: [
      {
        ref: 'employe',
        displayName: 'Employé',
        displayNamePlural: 'Employés',
        ...(existingKey ? { existingKey } : {}),
        fields: [
          { key: 'nom', label: 'Nom', type: 'text', required: true },
          { key: 'prenom', label: 'Prénom', type: 'text' },
          { key: 'email', label: 'E-mail', type: 'text' },
          { key: 'poste', label: 'Poste', type: 'text' }
        ]
      },
      {
        ref: 'demande',
        displayName: 'Demande de congé',
        displayNamePlural: 'Demandes de congé',
        fields: [
          { key: 'employe', label: 'Employé', type: 'relation', relationRef: 'employe' },
          { key: 'debut', label: 'Début', type: 'date', required: true },
          { key: 'fin', label: 'Fin', type: 'date', required: true },
          { key: 'type', label: 'Type', type: 'select', options: ['Payé', 'Sans solde'] },
          { key: 'statut', label: 'Statut', type: 'select', options: ['Soumise', 'Validée', 'Refusée'] }
        ]
      }
    ],
    seed: []
  };
}

/** Événements SSE d'une génération aboutissant à un plan `studio_plan` (avec doublons optionnels). */
export function studioPlanEvents(options: {
  planId?: string;
  usedAdvancedModel?: boolean;
  fallbackReason?: string | null;
  duplicates?: StudioPlanSummaryMock['duplicates'];
} = {}): SseEvent[] {
  const planId = options.planId ?? 'plan-e2e-1';
  const usedAdvanced = options.usedAdvancedModel ?? false;
  return [
    {
      type: 'meta',
      content: JSON.stringify({
        usedAdvancedModel: usedAdvanced,
        advancedModelFallbackReason: usedAdvanced ? null : options.fallbackReason ?? null,
        model: usedAdvanced ? 'GPT-4.1' : 'Mistral 7B'
      })
    },
    { type: 'content', content: 'Voici une proposition pour votre système de gestion des congés.' },
    {
      type: 'studio_plan',
      content: JSON.stringify({
        planId,
        kind: 'CreateSystem',
        expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(),
        summary: studioPlanSummary({ duplicates: options.duplicates ?? [] })
      })
    },
    { type: 'done', content: '', conversationId: 'conv-e2e-1' }
  ];
}

export const STUDIO_E2E_TEMPLATES: StudioTemplateMock[] = [
  { key: 'crm', displayName: 'CRM commercial', description: 'Prospects, opportunités, relances.', category: 'Commercial', moduleTag: 'crm', source: 'builtin', entityCount: 4 },
  { key: 'rh-conges', displayName: 'Gestion des congés', description: 'Employés, demandes, validations.', category: 'RH', moduleTag: 'hr', source: 'builtin', entityCount: 3 },
  { key: 'stock', displayName: 'Gestion de stock', description: 'Articles, mouvements, inventaires.', category: 'Logistique', moduleTag: 'stock', source: 'tenant', entityCount: 3 },
  { key: 'parc', displayName: 'Parc automobile', description: 'Véhicules, entretiens, conducteurs.', category: 'Logistique', moduleTag: 'fleet', source: 'builtin', entityCount: 3 }
];

export const STUDIO_E2E_PLANS: StudioPlanListItemMock[] = [
  { id: 'p-pending', kind: 'CreateSystem', status: 'Pending', title: 'Gestion des congés', entityCount: 2, createdAt: new Date(Date.now() - 5 * 60_000).toISOString(), expiresAt: new Date(Date.now() + 25 * 60_000).toISOString() },
  { id: 'p-done', kind: 'CreateSystem', status: 'Completed', title: 'Suivi des contrats', entityCount: 3, createdAt: new Date(Date.now() - 86_400_000).toISOString(), expiresAt: new Date(Date.now() - 80_000_000).toISOString(), executedAt: new Date(Date.now() - 86_000_000).toISOString(), systemKey: 'contrats' },
  { id: 'p-cancelled', kind: 'CreateApp', status: 'Cancelled', title: 'Table Fournisseurs', entityCount: 1, createdAt: new Date(Date.now() - 2 * 86_400_000).toISOString(), expiresAt: new Date(Date.now() - 86_400_000).toISOString() }
];

/**
 * Injecte une session « Se souvenir de moi » (jetons + profil dans `localStorage`) avant tout script
 * de l'application : `AuthService` relit ces clés à la construction et `authGuard` laisse passer.
 */
export async function installStudioAuth(page: Page, permissions: string[] = STUDIO_E2E_USER.effectivePermissions): Promise<void> {
  const user = { ...STUDIO_E2E_USER, effectivePermissions: permissions };
  await page.addInitScript(({ token, userJson }) => {
    localStorage.setItem('ft_auth_remember_me', '1');
    localStorage.setItem('ft_access_token', token);
    localStorage.setItem('ft_refresh_token', 'refresh-e2e');
    localStorage.setItem('ft_user', userJson);
  }, { token: FAKE_JWT, userJson: JSON.stringify(user) });
}

/**
 * Intercepte toutes les routes `/api/**` utiles à l'atelier. Les plans en attente (`cancel`,
 * `cancel-pending`) et la suppression de conversation renvoient un succès ; chaque appel est
 * consigné dans `calls` pour les assertions d'ordre (A19) et de charge utile (`useAdvancedModel`).
 */
export async function installStudioApiMocks(page: Page, options: StudioMockOptions = {}): Promise<StudioMockContext> {
  const calls: RecordedCall[] = [];
  const record = (route: Route) => {
    const req = route.request();
    calls.push({ method: req.method(), url: req.url(), body: safeJson(req.postData()) });
  };
  const capabilities = options.capabilities === null
    ? null
    : { ...STUDIO_CAPABILITIES_ALL_ENABLED, ...(options.capabilities ?? {}) };
  const plans = options.plans ?? STUDIO_E2E_PLANS;
  const templates = options.templates ?? STUDIO_E2E_TEMPLATES;
  let chatEvents = options.chatEvents ?? studioPlanEvents({ usedAdvancedModel: true });

  // Filet : tout appel non prévu renvoie une liste paginée vide (le layout charge notifications,
  // badges, recherche… — `res.data.items` ne doit jamais casser). Enregistré en premier : Playwright
  // évalue les routes de la plus récente à la plus ancienne, donc les routes précises passent avant.
  await page.route('**/api/**', async route => {
    record(route);
    await fulfilJson(route, ok({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false }));
  });

  await page.route('**/api/studio/nav', async route => {
    record(route);
    await fulfilJson(route, ok([]));
  });

  await page.route('**/api/ai/health', async route => {
    record(route);
    await fulfilJson(route, ok({ available: true }));
  });

  await page.route('**/api/ai/warm-up', async route => {
    record(route);
    await fulfilJson(route, ok({ warmed: true }));
  });

  await page.route('**/api/auth/me', async route => {
    record(route);
    await fulfilJson(route, ok(STUDIO_E2E_USER));
  });

  await page.route('**/api/ai/studio/capabilities', async route => {
    record(route);
    if (!capabilities) {
      await fulfilJson(route, { success: false, data: null, message: 'Not found', errors: [] }, 404);
      return;
    }
    await fulfilJson(route, ok(capabilities));
  });

  await page.route('**/api/studio/templates', async route => {
    record(route);
    await fulfilJson(route, ok(templates));
  });

  await page.route('**/api/studio/ai/plans?**', async route => {
    record(route);
    const url = new URL(route.request().url());
    const pageNo = Number(url.searchParams.get('page') ?? '1');
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20');
    const status = url.searchParams.get('status');
    const kind = url.searchParams.get('kind');
    const filtered = plans.filter(p => (!status || p.status === status) && (!kind || p.kind === kind));
    const items = filtered.slice((pageNo - 1) * pageSize, pageNo * pageSize);
    await fulfilJson(route, ok({
      items,
      page: pageNo,
      pageSize,
      totalCount: filtered.length,
      totalPages: Math.max(1, Math.ceil(filtered.length / pageSize)),
      hasNextPage: pageNo * pageSize < filtered.length,
      hasPreviousPage: pageNo > 1
    }));
  });

  // Route générique `GET /{id}` : enregistrée avant les sous-routes (`cancel-pending`,
  // `{id}/cancel`, `from-template`…) pour que celles-ci, plus récentes, soient évaluées en priorité.
await page.route(/\/api\/studio\/ai\/plans\/[^/?]+$/, async route => {
    record(route);
    const id = route.request().url().split('/').pop()!;
    const item = plans.find(p => p.id === id);
    if (!item) {
      await fulfilJson(route, { success: false, data: null, message: 'Plan introuvable', errors: [] }, 404);
      return;
    }
    await fulfilJson(route, ok({
      id: item.id, kind: item.kind, status: item.status,
      summaryJson: JSON.stringify(studioPlanSummary({ title: item.title })),
      createdAt: item.createdAt, expiresAt: item.expiresAt, executedAt: item.executedAt ?? null
    }));
  });

  await page.route('**/api/studio/ai/plans/cancel-pending', async route => {
    record(route);
    await fulfilJson(route, ok(plans.filter(p => p.status === 'Pending').length));
  });

  await page.route(/\/api\/studio\/ai\/plans\/[^/?]+\/cancel$/, async route => {
    record(route);
    const id = route.request().url().split('/').slice(-2, -1)[0];
    await fulfilJson(route, ok({ id, kind: 'CreateSystem', status: 'Cancelled', summaryJson: '{}', createdAt: new Date().toISOString(), expiresAt: new Date().toISOString() }));
  });

  await page.route(/\/api\/studio\/ai\/plans\/[^/?]+\/spec$/, async route => {
    record(route);
    const id = route.request().url().split('/').slice(-2, -1)[0];
    const method = route.request().method();
    const body = method === 'PUT' ? route.request().postDataJSON() as { specJson?: string } : null;
    const spec = body?.specJson ? JSON.parse(body.specJson) : studioSystemSpec();
    const specDto = { id, kind: 'CreateSystem', status: 'Pending', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(), rowVersion: 'AAAA', spec };
    if (method === 'PUT') {
      const reused = spec.entities?.filter((e: { existingKey?: string | null }) => !!e.existingKey).length ?? 0;
      const summary = studioPlanSummary({
        entities: spec.entities.map((e: { displayName: string; fields?: unknown[]; existingKey?: string | null }) => ({
          displayName: e.displayName, fieldCount: e.fields?.length ?? 0, relationCount: 0, existingKey: e.existingKey ?? null
        })),
        steps: reused
          ? [{ key: 'system', label: 'Système', detail: 'Gestion des congés' }, { key: 'reused', label: 'Tables réutilisées', detail: `${reused}` }]
          : undefined
      });
      await fulfilJson(route, ok({
        plan: { id, kind: 'CreateSystem', status: 'Pending', summaryJson: JSON.stringify(summary), createdAt: new Date().toISOString(), expiresAt: specDto.expiresAt },
        spec: specDto
      }));
      return;
    }
    await fulfilJson(route, ok(specDto));
  });

  await page.route('**/api/studio/ai/plans/from-template', async route => {
    record(route);
    const body = route.request().postDataJSON() as { templateKey: string };
    const template = templates.find(t => t.key === body.templateKey);
    const id = `plan-tpl-${body.templateKey}`;
    const summary = studioPlanSummary({ title: template?.displayName ?? body.templateKey });
    await fulfilJson(route, ok({
      plan: { id, kind: 'CreateSystem', status: 'Pending', summaryJson: JSON.stringify(summary), createdAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 30 * 60_000).toISOString() },
      spec: { id, kind: 'CreateSystem', status: 'Pending', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(), rowVersion: 'AAAA', spec: studioSystemSpec() }
    }));
  });


  await page.route(/\/api\/ai\/conversations\/[^/?]+$/, async route => {
    record(route);
    await route.fulfill({ status: 204, body: '' });
  });

  await page.route('**/api/ai/chat', async route => {
    record(route);
    await route.fulfill({
      status: 200,
      headers: { 'Content-Type': 'text/event-stream', 'X-Trace-Id': 'trace-e2e' },
      body: sseBody(chatEvents)
    });
  });

  return {
    calls,
    find: (urlPart, method) => calls.filter(c => c.url.includes(urlPart) && (!method || c.method === method)),
    setChatEvents: events => { chatEvents = events; }
  };
}

/** Auth + mocks API en un appel ; renvoie le contexte d'enregistrement des appels. */
export async function setupStudioAtelier(page: Page, options: StudioMockOptions = {}): Promise<StudioMockContext> {
  await installStudioAuth(page);
  return installStudioApiMocks(page, options);
}

function safeJson(raw: string | null): unknown {
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return raw;
  }
}
