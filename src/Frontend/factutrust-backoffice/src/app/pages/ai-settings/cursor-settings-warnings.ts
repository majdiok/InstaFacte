/** Réponse de GET /api/ai/configured-status — disponibilité réelle des fournisseurs IA. */
export interface AiConfiguredStatusDto {
  hasOllamaModels: boolean;
  hasCloudProvider: boolean;
  isFullyConfigured: boolean;
}

export function needsCursorModelWarning(params: {
  cursorEnabled: boolean;
  cursorApiKeyConfigured: boolean;
  cursorApiKey: string;
  selectedModelRef: string;
}): boolean {
  const hasKey = params.cursorApiKeyConfigured || !!params.cursorApiKey.trim();
  return params.cursorEnabled && hasKey && !params.selectedModelRef.toLowerCase().startsWith('cursor:');
}

export function needsCursorCatalogWarning(params: {
  cursorEnabled: boolean;
  availableModels: ReadonlyArray<{ providerKey: string }>;
}): boolean {
  if (!params.cursorEnabled) {
    return false;
  }

  return !params.availableModels.some(m => m.providerKey === 'cursor');
}
