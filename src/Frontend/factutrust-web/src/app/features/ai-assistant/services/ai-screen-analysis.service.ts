import { Injectable, inject } from '@angular/core';
import { AiAssistantShellService } from './ai-assistant-shell.service';
import { AiVolatileAnalysisStore } from './ai-volatile-analysis.store';
import { AiChatSessionService } from './ai-chat-session.service';
import { buildScreenAnalysisDisplayLabel } from '../utils/ai-screen-labels.util';
import {
  buildVolatileAnalysisJson
} from '../utils/ai-volatile-analysis-payload.util';
import { resolveScreenAnalysisBackendPrompt } from '../utils/ai-screen-analysis-prompts';
import { isEnhancedPromptForScreen } from '../utils/ai-screen-analysis-config';

export interface StartAnalysisOptions {
  /** When true (default), sends the standard analysis prompt once the panel is open. */
  autoSend?: boolean;
}

/**
 * Orchestrates volatile screen context + opening the chat panel + optional first message.
 * Always creates a fresh conversation. The AI model is resolved server-side from the
 * tenant configuration (back-office), so no model choice happens here.
 */
@Injectable({ providedIn: 'root' })
export class AiScreenAnalysisService {
  private readonly shell = inject(AiAssistantShellService);
  private readonly store = inject(AiVolatileAnalysisStore);
  private readonly session = inject(AiChatSessionService);

  startAnalysis(screenId: string, payload: unknown, options?: StartAnalysisOptions): void {
    const summary = buildVolatileAnalysisJson(screenId, payload);
    const volatileContext = { screenId, analysisSummary: summary };

    this.session.startNewConversation();
    this.shell.requestOpenPanel();

    if (options?.autoSend !== false) {
      if (this.session.isStreaming()) {
        this.session.abortActiveStream();
      }

      const backendPrompt = isEnhancedPromptForScreen(screenId)
        ? resolveScreenAnalysisBackendPrompt(screenId)
        : resolveScreenAnalysisBackendPrompt(screenId);

      this.session.setLastScreenAnalysisTrace({
        screenId,
        schemaVersion: '2',
        payloadBytes: summary.length,
        truncated: summary.includes('[tronqué côté client]')
      });

      this.session.sendMessage({
        displayText: buildScreenAnalysisDisplayLabel(screenId),
        backendText: backendPrompt,
        volatileContext,
        screenAnalysis: true
      });
    } else {
      this.store.setPending(volatileContext);
    }
  }
}
