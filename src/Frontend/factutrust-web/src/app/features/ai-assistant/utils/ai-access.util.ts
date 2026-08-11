/** Minimal auth surface for AI assistant access checks (shared by layout and screen-analysis button). */
export interface AiAccessContext {
  hasAllPermissions(permissions: readonly string[]): boolean;
  isAccountingFirm(): boolean;
  isDelegatedMode(): boolean;
}

/** True when the user may open the AI assistant panel or trigger screen analysis. */
export function canUseAiAssistant(auth: AiAccessContext): boolean {
  if (!auth.hasAllPermissions(['ai:chat'])) {
    return false;
  }
  if (auth.isAccountingFirm()) {
    return auth.isDelegatedMode();
  }
  return true;
}
