/** Poison is synthetic; never real tenant classification, consent or provenance. */
export function poisonUnknownFields(input: unknown): void {
  const stack: { value: unknown; field: string }[] = [{ value: input, field: '' }];
  while (stack.length) {
    const { value, field } = stack.pop()!;
    if (typeof value !== 'object' || value === null) continue;
    for (const [key, child] of Object.entries(value)) stack.push({ value: child, field: key });
    // These two maps have closed key sets in L0; unknown keys must fail, not pass.
    if (Array.isArray(value) || field === 'styleRecipes' || field === 'variants') continue;
    for (const key of ['companySegment', 'businessDomain', 'consentAcceptedByUserId', 'privateSource', '__proto__', 'constructor']) {
      Object.defineProperty(value, key, { value: { privateMarker: ['never-reemit'] }, enumerable: true, writable: true, configurable: true });
    }
  }
}

export function visualReference(): Record<string, unknown> {
  return { schemaVersion: 1, profileKey: 'synthetic-accepted', editorialRevision: 'r7', catalogVersion: 'v2.synthetic', representationKind: 'illustrative' };
}

export function acceptedRequest(): Record<string, unknown> {
  return { kind: 'accepted', request: {
    scopeId: 'scope-1', generation: 2, slug: 'synthetic-storefront', visualProfile: visualReference(),
    facadeTheme: 4, quality: 'economy', sceneKey: 'synthetic-interior'
  } };
}

/** Selection supplied by a synthetic trusted caller, NOT evidence of G1 approval. */
export function neutralRequest(): Record<string, unknown> {
  return { kind: 'neutral-global', sceneKey: 'synthetic-neutral', catalogVersion: 'v2.synthetic', quality: 'standard', scopeId: 'scope-2', generation: 0 };
}
