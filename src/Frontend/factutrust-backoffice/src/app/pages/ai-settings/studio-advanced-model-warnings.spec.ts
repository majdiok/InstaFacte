import { isStudioAdvancedModelSameAsStandard } from './studio-advanced-model-warnings';

describe('studio-advanced-model-warnings', () => {
  it('signale un modèle avancé identique au modèle Studio standard', () => {
    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: 'ollama:qwen2.5:7b-instruct',
        advancedModelRef: 'ollama:qwen2.5:7b-instruct'
      })
    ).toBeTrue();
  });

  it('ignore la casse et les espaces superflus', () => {
    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: ' ollama:Qwen2.5:7B-Instruct ',
        advancedModelRef: 'ollama:qwen2.5:7b-instruct'
      })
    ).toBeTrue();
  });

  it('accepte un modèle avancé différent', () => {
    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: 'ollama:qwen2.5:7b-instruct',
        advancedModelRef: 'openrouter:qwen/qwen-2.5-72b-instruct'
      })
    ).toBeFalse();
  });

  it("n'alerte pas quand l'un des deux réglages est vide", () => {
    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: '',
        advancedModelRef: 'openrouter:qwen/qwen-2.5-72b-instruct'
      })
    ).toBeFalse();

    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: 'ollama:qwen2.5:7b-instruct',
        advancedModelRef: null
      })
    ).toBeFalse();

    expect(
      isStudioAdvancedModelSameAsStandard({
        standardModelRef: undefined,
        advancedModelRef: undefined
      })
    ).toBeFalse();
  });
});
