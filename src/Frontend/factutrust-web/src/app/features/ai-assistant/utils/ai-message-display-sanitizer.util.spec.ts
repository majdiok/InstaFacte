import {
  extractScreenIdFromPersistedMessage,
  isPersistedScreenAnalysisUserMessage,
  sanitizeUserMessageForDisplay
} from './ai-message-display-sanitizer.util';
import { SCREEN_ANALYSIS_BACKEND_PROMPT } from './ai-volatile-analysis-payload.util';

describe('ai-message-display-sanitizer.util', () => {
  const persistedWithBlock = `${SCREEN_ANALYSIS_BACKEND_PROMPT}\n\n[CONTEXTE ÉCRAN — accounting-ledger — non contractuel]\n{"rows":[]}\n[FIN CONTEXTE ÉCRAN]`;

  it('detects persisted screen analysis messages by prompt prefix', () => {
    expect(isPersistedScreenAnalysisUserMessage(SCREEN_ANALYSIS_BACKEND_PROMPT)).toBe(true);
  });

  it('detects persisted screen analysis messages by context markers', () => {
    expect(isPersistedScreenAnalysisUserMessage('[CONTEXTE ÉCRAN — stock-simple — non contractuel]\n{}')).toBe(
      true
    );
    expect(isPersistedScreenAnalysisUserMessage('hello\n[FIN CONTEXTE ÉCRAN]')).toBe(true);
  });

  it('does not flag normal user messages', () => {
    expect(isPersistedScreenAnalysisUserMessage('Quel est mon CA ce mois-ci ?')).toBe(false);
  });

  it('extracts screenId from persisted message', () => {
    expect(extractScreenIdFromPersistedMessage(persistedWithBlock)).toBe('accounting-ledger');
  });

  it('sanitizeUserMessageForDisplay returns friendly label for analysis messages', () => {
    expect(sanitizeUserMessageForDisplay(persistedWithBlock)).toBe(
      "Analyse de l'écran : Grand livre"
    );
  });

  it('sanitizeUserMessageForDisplay leaves normal messages unchanged', () => {
    const text = 'Bonjour assistant';
    expect(sanitizeUserMessageForDisplay(text)).toBe(text);
  });

  it('sanitizeUserMessageForDisplay falls back when screenId is missing', () => {
    expect(
      sanitizeUserMessageForDisplay(`${SCREEN_ANALYSIS_BACKEND_PROMPT}\n\n[FIN CONTEXTE ÉCRAN]`)
    ).toBe("Analyse de l'écran : écran");
  });
});
