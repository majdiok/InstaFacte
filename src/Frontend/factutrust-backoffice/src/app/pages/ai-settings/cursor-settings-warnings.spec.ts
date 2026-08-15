import { needsCursorCatalogWarning, needsCursorModelWarning } from './cursor-settings-warnings';

describe('cursor-settings-warnings', () => {
  describe('needsCursorModelWarning', () => {
    it('warns when Cursor is enabled with a key but assistant model is not cursor:', () => {
      expect(
        needsCursorModelWarning({
          cursorEnabled: true,
          cursorApiKeyConfigured: true,
          cursorApiKey: '',
          selectedModelRef: 'ollama:mistral'
        })
      ).toBeTrue();

      expect(
        needsCursorModelWarning({
          cursorEnabled: true,
          cursorApiKeyConfigured: false,
          cursorApiKey: 'cursor_key',
          selectedModelRef: ''
        })
      ).toBeTrue();
    });

    it('does not warn when assistant uses a cursor model', () => {
      expect(
        needsCursorModelWarning({
          cursorEnabled: true,
          cursorApiKeyConfigured: true,
          cursorApiKey: '',
          selectedModelRef: 'cursor:composer-2.5'
        })
      ).toBeFalse();
    });
  });

  describe('needsCursorCatalogWarning', () => {
    it('warns when Cursor is enabled but catalog has no cursor models', () => {
      expect(
        needsCursorCatalogWarning({
          cursorEnabled: true,
          availableModels: [{ providerKey: 'ollama' }, { providerKey: 'openrouter' }]
        })
      ).toBeTrue();
    });

    it('does not warn when cursor models are listed', () => {
      expect(
        needsCursorCatalogWarning({
          cursorEnabled: true,
          availableModels: [{ providerKey: 'cursor' }]
        })
      ).toBeFalse();
    });
  });
});
