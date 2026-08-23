import { aiProviderLabel } from './ai-provider-label';

describe('aiProviderLabel', () => {
  it('labels InstaFact IA, OpenRouter, Modal and Cursor', () => {
    expect(aiProviderLabel('ollama')).toBe('InstaFact IA');
    expect(aiProviderLabel('openrouter')).toBe('OpenRouter');
    expect(aiProviderLabel('modal')).toBe('Modal (Kimi)');
    expect(aiProviderLabel('cursor')).toBe('Cursor');
  });
});
