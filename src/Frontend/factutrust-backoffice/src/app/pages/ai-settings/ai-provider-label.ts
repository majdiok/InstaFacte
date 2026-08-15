/** Libellé dropdown Configuration IA (providerKey du catalogue unifié). */
export function aiProviderLabel(providerKey: string | null | undefined): string {
  switch (providerKey) {
    case 'openrouter':
      return 'OpenRouter';
    case 'cursor':
      return 'Cursor';
    default:
      return 'InstaFact IA';
  }
}
