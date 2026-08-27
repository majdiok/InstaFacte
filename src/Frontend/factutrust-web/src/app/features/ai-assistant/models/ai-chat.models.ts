export interface ChatStreamEvent {
  type:
    | 'content'
    | 'content_replace'
    | 'tool_call_start'
    | 'tool_call_end'
    | 'done'
    | 'error'
    | 'heartbeat'
    | 'client_actions'
    | 'suggested_prompts'
    | 'sources'
    | 'phase'
    | 'dashboard'
    | 'studio_progress'
    /** Plan Studio en attente de validation utilisateur (flux aperçu → confirmation). */
    | 'studio_plan'
    /** Résultat final d'un plan Studio exécuté (émis par l'endpoint de confirmation). */
    | 'studio_result'
    /** Résultat d'un état calculé en lecture seule, à afficher dans la conversation (rien n'est enregistré). */
    | 'studio_report_result';
  content?: string;
  toolName?: string;
  toolCallId?: string;
  conversationId?: string;
  error?: string;
  clientActions?: string;
  /** JSON array of follow-up question strings (type suggested_prompts). */
  suggestedPrompts?: string;
  sources?: string;
  /**
   * JSON-encoded DashboardConfig (already sanitized server-side) emitted right after
   * `generate_dashboard_config` succeeds — lets the UI render the chart deterministically
   * without depending on the LLM copying the JSON into its reply.
   */
  dashboard?: string;
  /** Backend processing phase identifier (type phase). */
  phase?: string;
  /** Running/completed/failed/cancelled state for `phase` events. */
  phaseStatus?: AssistantPhaseStatus;
  /** Elapsed milliseconds reported by the backend for the phase. */
  elapsedMs?: number;
  /** 1-based LLM/tool orchestration round when applicable. */
  round?: number;
  /** First token latency in milliseconds when known. */
  firstTokenMs?: number | null;
  /** Whether the completed phase required tool calls. */
  hadToolCalls?: boolean;
  /** Optional human-readable detail for the phase. */
  detail?: string;
}

export type AssistantPhaseStatus = 'running' | 'completed' | 'failed' | 'cancelled';

export enum AssistantMode {
  Default = 0,
  Compliance = 1,
  ScreenAnalysis = 2,
  /** Focused Studio low-code "AI builder" surface (generation / report / extraction). */
  StudioBuilder = 3
}

/**
 * Expert « métier » optionnel (assistants par module) — miroir numérique de l'enum backend
 * AssistantAgentScope. None = assistant global (comportement historique inchangé).
 */
export enum AssistantAgentScope {
  None = 0,
  Sales = 1,
  Purchases = 2,
  Stock = 3,
  Accounting = 4,
  Treasury = 5,
  Crm = 6,
  /**
   * Agent « Chef de mission » — seul scope au périmètre du CABINET et non d'un dossier.
   * Servi par une surface HTTP distincte (/firm/ai), sous la route /firm/assistant.
   */
  FirmMission = 7
}

export interface ChatUiContext {
  route?: string;
  pathParams?: Record<string, string>;
  queryParams?: Record<string, string>;
  screenId?: string;
  entity?: { type: string; id: string };
  /**
   * Non-contractual JSON snapshot of on-screen data for analysis (volatile, truncated server-side).
   */
  analysisSummary?: string;
}

export interface ChatRequestOptions {
  assistantMode?: AssistantMode;
  /** Suivi conversationnel (clic sur un prompt suggéré) : le backend restreint le catalogue d'outils. */
  conversationalFollowUp?: boolean;
  /** Expert de module (assistants par module). Omis/None = assistant global (comportement historique). */
  agentScope?: AssistantAgentScope;
}

export interface ChatRequest {
  conversationId?: string;
  message: string;
  uiContext?: ChatUiContext;
  options?: ChatRequestOptions;
  /** In-flight attachments (text already in `message`; images for vision models). */
  attachments?: ChatAttachmentRequest[];
}

/** Payload envoyé au backend dans le ChatRequest pour chaque pièce jointe. */
export interface ChatAttachmentRequest {
  fileName: string;
  format: string;
  pageCount: number;
  ocrApplied: boolean;
  truncated: boolean;
  /** Liste de PNG/JPG base64 (sans préfixe data:), uniquement si modèle vision. */
  imagesBase64?: string[];
}

export interface ClientNavAction {
  label: string;
  route: string;
  queryParams?: Record<string, string>;
}

/**
 * Action de relance en attente de confirmation, émise par l'outil cabinet
 * `send_fiscal_deadline_reminder` (PREVIEW) sous la clé `actionEnAttente` du payload d'outil, puis
 * relayée telle quelle dans l'événement SSE `client_actions`. Confirmée via
 * `POST /api/firm/ai/reminders/confirm` avec `{ nonce }`.
 *
 * Consommé par `ai-chat-session.service.ts` (discrimination par `kind` dans le handler
 * `client_actions`) et rendu par `chat-message.component.ts` (carte de confirmation Lot 5).
 * La discrimination par `kind` reste rétro-compatible avec `ClientNavAction`, qui n'a pas de `kind`.
 */
export interface ConfirmFirmReminderAction {
  kind: 'confirm_firm_reminder';
  label: string;
  nonce: string;
  expiresAtUtc: string;
  preview: {
    responsable: string;
    dossier: string;
    echeance: string;
    objet: string;
  };
}

/** La discrimination par `kind` est gérée via les types individuels (plutôt qu'une union exportée). */

/** In-app link for a dashboard table cell (server-sanitized). */
export interface DashboardCellLink {
  route: string;
  queryParams?: Record<string, string>;
}

export interface SourceRef {
  toolName: string;
  toolCallId: string;
}

export interface DailyAiBriefing {
  upcomingRemindersCount: number;
  nextReminders: DailyBriefingReminderItem[];
  totalClientBalances: number;
  clientsWithBalanceCount: number;
  agingOver90: number;
  currency: string;
  generatedAtUtc: string;
}

export interface DailyBriefingReminderItem {
  subject: string;
  dueDate: string | null;
  clientName: string | null;
}

export interface ConversationDto {
  id: string;
  title: string;
  createdAt: string;
  lastMessageAt: string;
  selectedModel?: string;
  messageCount: number;
  /** Expert de module de la conversation (miroir int d'AssistantAgentScope, 0 = global). */
  agentScope?: number;
}

export interface ConversationDetailDto {
  id: string;
  title: string;
  createdAt: string;
  lastMessageAt: string;
  selectedModel?: string;
  messages: ChatMessageDto[];
}

export interface ChatMessageDto {
  id: string;
  role: MessageRole;
  content: string;
  createdAt: string;
  toolName?: string;
  toolCallId?: string;
}

export enum MessageRole {
  System = 0,
  User = 1,
  Assistant = 2,
  Tool = 3
}

export interface ActiveToolCall {
  name: string;
  callId: string;
  status: 'running' | 'completed' | 'cancelled';
  /** Server-reported execution time in milliseconds (tool_call_end). */
  elapsedMs?: number;
}

export interface AssistantProgressStep {
  key: string;
  code: string;
  label: string;
  status: AssistantPhaseStatus;
  elapsedMs?: number;
  round?: number;
  firstTokenMs?: number | null;
  hadToolCalls?: boolean;
  detail?: string;
}

export interface AssistantProgressTimeline {
  status: AssistantPhaseStatus;
  steps: AssistantProgressStep[];
}

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  createdAt: Date;
  toolName?: string;
  toolCalls?: ActiveToolCall[];
  isStreaming?: boolean;
  /** True when the SSE ended before a normal `done` (logout, network, or server error event). */
  generationInterrupted?: boolean;
  /** Parsed dashboard from first valid ```json fence; set on stream `done` and on conversation hydrate. */
  parsedDashboard?: DashboardConfig;
  /** User dismissed inline dashboard; markdown stays stripped when payload is valid. */
  hideInlineDashboard?: boolean;
  /** Validated navigation chips from SSE `client_actions`. */
  clientActions?: ClientNavAction[];
  /**
   * Relance d'échéance en attente de confirmation (SSE `client_actions`, discriminator
   * `confirm_firm_reminder`). Confirmée via `POST /api/firm/ai/reminders/confirm` avec le nonce.
   */
  firmReminderAction?: ConfirmFirmReminderAction;
  /** True après confirmation serveur réussie — la carte bascule sur l'état « Relance envoyée ». */
  firmReminderConfirmed?: boolean;
  /** True pendant l'appel de confirmation (bouton désactivé, état chargement). */
  firmReminderConfirming?: boolean;
  /** Message d'erreur après un échec de confirmation (le nonce peut rester valide tant qu'il n'a pas expiré). */
  firmReminderError?: string;
  /** Follow-up question chips from SSE `suggested_prompts` or persisted `ft-meta`. */
  suggestedPrompts?: string[];
  /** Tool names used for this reply (SSE `sources`). */
  sources?: SourceRef[];
  /** Technical execution timeline shown instead of internal chain-of-thought. */
  progress?: AssistantProgressTimeline;
  /** Pièces jointes attachées à un message utilisateur (texte extrait + métadonnées). */
  attachments?: ChatAttachment[];
  /** True once the local id has been reconciled with the persisted server message id. */
  serverSynced?: boolean;
}

/** Pièce jointe matérialisée côté UI (avec son extrait et son éventuel rendu image). */
export interface ChatAttachment {
  id: string;
  fileName: string;
  format: 'pdf' | 'image' | 'docx' | 'xlsx' | 'csv' | 'txt' | string;
  sizeBytes: number;
  pageCount: number;
  ocrApplied: boolean;
  truncated: boolean;
  fullText: string;
  pages: ChatAttachmentPage[];
  warnings: string[];
}

export interface ChatAttachmentPage {
  pageIndex: number;
  text: string;
  /** PNG/JPG base64 sans préfixe data:. Présent uniquement si le rendu a été demandé. */
  imageBase64?: string;
  width?: number;
  height?: number;
  ocrApplied: boolean;
}

export interface DashboardConfig {
  title: string;
  sections: DashboardSection[];
}

export interface DashboardSection {
  type: 'kpi_card' | 'table' | 'chart';
  title: string;
  data: any;
}

/** Modèle IA effectivement utilisé par l'assistant (configuré dans le back-office Plateforme). */
export interface AiActiveModelDto {
  modelRef: string;
  displayLabel: string;
  supportsVision: boolean;
}

/** Réponse de GET /api/ai/configured-status — disponibilité réelle des fournisseurs IA. */
export interface AiConfiguredStatusDto {
  hasOllamaModels: boolean;
  hasCloudProvider: boolean;
  isFullyConfigured: boolean;
}

// ─────────────────────────────────────────────────────────────────────────────
// PowerPoint export (Assistant IA → .pptx).
// Pure type additions — no existing surface modified.
// ─────────────────────────────────────────────────────────────────────────────

/** Backend enum mirror — keep in sync with `Application.Features.AI.Export.PowerPoint.Models.PowerPointThemeCategory`. */
export enum PowerPointThemeCategory {
  Light = 0,
  Dark = 1,
  Premium = 2,
  Vibrant = 3
}

/** Backend enum mirror — keep in sync with `Application.Features.AI.Export.PowerPoint.Models.PowerPointTemplate`. */
export enum PowerPointTemplate {
  Standard = 0,
  Analyse = 1,
  Executive = 2,
  Pearl = 3,
  Daydream = 4,
  Serene = 5,
  Breeze = 6,
  Kraft = 7,
  Ash = 8,
  Howlite = 9,
  Vortex = 10,
  Indigo = 11,
  Onyx = 12,
  Blueberry = 13,
  Coal = 14,
  Electric = 15,
  Mystique = 16,
  Clementa = 17,
  Stratos = 18,
  Mercury = 19,
  Dialogue = 20,
  Nova = 21,
  Aurora = 22,
  CoralGlow = 23,
  TechReport = 24,
  MinimalBusiness = 25,
  DarkCinematic = 26,
  FinanceGold = 27,
  MedicalClean = 28,
  MagazinePortfolio = 29,
  GradientCloud = 30,
  ArtBold = 31,
  BusinessMgmt = 32,
  VibeCoding = 33,
  TeamStrategy = 34,
  EsgSerene = 35,
  // ── Premium V2 — editorial & executive ──
  EditorialNoir = 36,
  BoardroomCharcoal = 37,
  EmeraldExec = 38,
  LinenScholar = 39,
  ForestRetreat = 40,
  // ── Premium V2 — dark cinematic ──
  NeonBlade = 41,
  MidnightData = 42,
  Obsidian = 43,
  AbyssTeal = 44,
  // ── Premium V2 — vibrant marketing ──
  SunsetGradient = 45,
  CoralLab = 46,
  CitrusBurst = 47,
  // ── Premium V2 — light minimalist ──
  ScandiCalm = 48,
  SapphireBriefing = 49,
  RoseQuartz = 50,
  SlateMinimal = 51
}

/** Backend enum mirror — keep in sync with `PowerPointThemeEngine`. */
export enum PowerPointThemeEngine {
  Legacy = 0,
  Hybrid = 1
}

/** Backend enum mirror — keep in sync with `CoverLayoutStyle`. */
export enum CoverLayoutStyle {
  ClassicBar = 0,
  MinimalSerif = 1,
  DarkCinematic = 2,
  GradientHero = 3,
  SplitPhoto = 4,
  BoldEditorial = 5,
  CorporateBlue = 6,
  MedicalClean = 7,
  /** Twin neon bars (top + bottom) plus a corner glow — cyber/tech feel. */
  NeonAccent = 8,
  /** Narrow side band plus two thin horizontal rules — editorial magazine. */
  EditorialMagazine = 9,
  /** 40% accent block on the right plus a thick accent bar — asymmetric corporate. */
  AsymmetricSplit = 10,
  /** Stacked color stripes simulating a multi-stop gradient hero. */
  GradientWaveHero = 11
}

/** Backend enum mirror — keep in sync with `Application.Features.AI.Export.PowerPoint.Models.SlideOrientation`. */
export enum SlideOrientation {
  Widescreen16x9 = 0,
  Standard4x3 = 1
}

/** Backend flags enum mirror — keep in sync with `Application.Features.AI.Export.PowerPoint.Models.SlideContentBlock`. */
export enum SlideContentBlock {
  None = 0,
  Text = 1,
  KpiCards = 2,
  Tables = 4,
  Charts = 8,
  Sources = 16,
  All = Text | KpiCards | Tables | Charts | Sources
}

/** Single assistant response selected for the deck. */
export interface PowerPointResponseSelection {
  conversationId: string;
  messageId: string;
  customTitle?: string;
  /** Bitmask of `SlideContentBlock` values; omit to include all available blocks. */
  includeOnly?: number;
}

/** Payload for `POST /api/ai/exports/powerpoint`. */
export interface PowerPointExportRequest {
  title: string;
  subtitle?: string;
  authorName?: string;
  template: PowerPointTemplate;
  orientation: SlideOrientation;
  includeCoverSlide: boolean;
  includeAgenda: boolean;
  includeTableOfContents: boolean;
  includeSpeakerNotes: boolean;
  includeSources: boolean;
  includeAppendix: boolean;
  locale?: string;
  responses: PowerPointResponseSelection[];
}

/** Deferred download envelope (large decks). */
export interface PowerPointExportResponse {
  exportId: string;
  fileName: string;
  sizeBytes: number;
  slideCount: number;
  generatedAt: string;
  expiresAt: string;
  downloadUrl: string;
}

/** Template catalogue entry shown in the picker. */
export interface PowerPointTemplateInfo {
  id: PowerPointTemplate;
  key: string;
  name: string;
  description: string;
  category: PowerPointThemeCategory;
  isDark: boolean;
  sortOrder: number;
  primaryColorHex: string;
  accentColorHex: string;
  backgroundColorHex: string;
  surfaceColorHex: string;
  onSurfaceColorHex: string;
  linkColorHex: string;
  titleFontFamily: string;
  bodyFontFamily: string;
  previewGradientCss?: string;
  engine: PowerPointThemeEngine;
  previewThumbnailUrl?: string;
  previewThumbnailUrl4x3?: string;
  requiresAttribution: boolean;
  attributionSummary?: string;
  coverLayoutStyle: CoverLayoutStyle;
}

/** Structural preview of an assistant response before PowerPoint export. */
export interface PowerPointResponsePreview {
  conversationId: string;
  messageId: string;
  title: string;
  kpiCount: number;
  tableCount: number;
  chartCount: number;
  sectionCount: number;
  hasText: boolean;
  slideOutline: string[];
}

export interface PowerPointExportAudit {
  id: string;
  format: string;
  template: string;
  title?: string;
  responseCount: number;
  slideCount: number;
  sizeBytes: number;
  generatedAt: string;
  success: boolean;
}

/** Normalises API catalogue id (string PascalCase or numeric) to `PowerPointTemplate`. */
export function toPowerPointTemplate(value: string | number | PowerPointTemplate): PowerPointTemplate {
  if (typeof value === 'number' && PowerPointTemplate[value] !== undefined) {
    return value;
  }
  if (typeof value === 'string') {
    const byName = (PowerPointTemplate as Record<string, number | string>)[value];
    if (typeof byName === 'number') return byName;
    const parsed = Number(value);
    if (Number.isInteger(parsed) && PowerPointTemplate[parsed] !== undefined) {
      return parsed as PowerPointTemplate;
    }
  }
  return PowerPointTemplate.Standard;
}

/** Serialises a template for the API (`JsonStringEnumConverter` expects PascalCase names). */
export function powerPointTemplateToApi(value: string | number | PowerPointTemplate): string {
  const normalized = toPowerPointTemplate(value);
  return PowerPointTemplate[normalized] ?? 'Standard';
}

/** Serialises orientation for the API. */
export function slideOrientationToApi(value: SlideOrientation): string {
  return SlideOrientation[value] ?? 'Widescreen16x9';
}

/** Serialises content-block flags for the API; omits when all blocks are included. */
export function slideContentBlockToApi(mask?: number): string | undefined {
  if (mask === undefined || mask === SlideContentBlock.All) return undefined;
  if (mask === SlideContentBlock.None) return 'None';

  const flags: string[] = [];
  if (mask & SlideContentBlock.Text) flags.push('Text');
  if (mask & SlideContentBlock.KpiCards) flags.push('KpiCards');
  if (mask & SlideContentBlock.Tables) flags.push('Tables');
  if (mask & SlideContentBlock.Charts) flags.push('Charts');
  if (mask & SlideContentBlock.Sources) flags.push('Sources');
  return flags.length > 0 ? flags.join(', ') : undefined;
}
