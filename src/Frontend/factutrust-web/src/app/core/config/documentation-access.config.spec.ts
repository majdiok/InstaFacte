import { isDocumentationRoute } from './documentation-access.config';

describe('documentation-access.config', () => {
  describe('isDocumentationRoute', () => {
    it('matches the documentation root', () => {
      expect(isDocumentationRoute('/documentation')).toBe(true);
    });

    it('matches documentation chapter paths', () => {
      expect(isDocumentationRoute('/documentation/glossaire')).toBe(true);
      expect(isDocumentationRoute('/documentation/rapports')).toBe(true);
    });

    it('ignores query strings', () => {
      expect(isDocumentationRoute('/documentation/rapports?from=search')).toBe(true);
    });

    it('does not match unrelated routes', () => {
      expect(isDocumentationRoute('/dashboard')).toBe(false);
      expect(isDocumentationRoute('/firm/dashboard')).toBe(false);
      expect(isDocumentationRoute('/reports')).toBe(false);
    });
  });
});
