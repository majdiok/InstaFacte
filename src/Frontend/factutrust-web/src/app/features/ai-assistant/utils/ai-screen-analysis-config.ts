/** Client-side feature flags for screen analysis rollout (mirrors backend ScreenAnalysis options). */
export const SCREEN_ANALYSIS_CONFIG = {
  enabled: true,
  enhancedPromptsEnabled: true,
  enhancedPayloadsEnabled: true,
  deduplicateContextInMessage: true,

  /** Per-screen overrides for gradual rollout. */
  perScreenOverrides: {
    'accounting-income-statement': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-ledger': { enhancedPrompt: true, enhancedPayload: true },
    'cash-desk': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-balance': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-balance-sheet': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-sub-journals': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-aging': { enhancedPrompt: true, enhancedPayload: true },
    dashboard: { enhancedPrompt: true, enhancedPayload: true },
    'invoice-list': { enhancedPrompt: true, enhancedPayload: true },
    'credit-note-list': { enhancedPrompt: true, enhancedPayload: true },
    'stock-simple': { enhancedPrompt: true, enhancedPayload: true },
    'forecasting-replenishment': { enhancedPrompt: true, enhancedPayload: true },
    'forecasting-revenue': { enhancedPrompt: true, enhancedPayload: true },
    'treasury-cash-forecast': { enhancedPrompt: true, enhancedPayload: true },
    'forecasting-abc-xyz': { enhancedPrompt: true, enhancedPayload: true },
    'forecasting-promotions': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-journal': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-lettering': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-closing': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-vat-declaration': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-manual-entry': { enhancedPrompt: true, enhancedPayload: true },
    'accounting-chart': { enhancedPrompt: true, enhancedPayload: true }
  } as Record<string, { enhancedPrompt?: boolean; enhancedPayload?: boolean }>
} as const;

export function isEnhancedPromptForScreen(screenId: string): boolean {
  if (!SCREEN_ANALYSIS_CONFIG.enabled || !SCREEN_ANALYSIS_CONFIG.enhancedPromptsEnabled) {
    return false;
  }
  const override = SCREEN_ANALYSIS_CONFIG.perScreenOverrides[screenId];
  return override?.enhancedPrompt ?? SCREEN_ANALYSIS_CONFIG.enhancedPromptsEnabled;
}

export function isEnhancedPayloadForScreen(screenId: string): boolean {
  if (!SCREEN_ANALYSIS_CONFIG.enabled || !SCREEN_ANALYSIS_CONFIG.enhancedPayloadsEnabled) {
    return false;
  }
  const override = SCREEN_ANALYSIS_CONFIG.perScreenOverrides[screenId];
  return override?.enhancedPayload ?? SCREEN_ANALYSIS_CONFIG.enhancedPayloadsEnabled;
}

export function shouldDeduplicateContextInMessage(): boolean {
  return SCREEN_ANALYSIS_CONFIG.enabled && SCREEN_ANALYSIS_CONFIG.deduplicateContextInMessage;
}
