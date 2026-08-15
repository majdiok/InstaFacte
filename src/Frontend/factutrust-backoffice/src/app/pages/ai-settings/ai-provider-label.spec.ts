import { aiProviderLabel } from './ai-provider-label';

describe('aiProviderLabel', () => {
  it('labels InstaFact IA, OpenRouter and Cursor', () => {
    expect(aiProviderLabel('ollama')).toBe('InstaFact IA');
    expect(aiProviderLabel('openrouter')).toBe('OpenRouter');
    expect(aiProviderLabel('cursor')).toBe('Cursor');
  });
});
