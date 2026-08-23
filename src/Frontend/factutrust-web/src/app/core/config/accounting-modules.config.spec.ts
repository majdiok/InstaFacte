import {
  ACCOUNTING_HOME_HIDDEN_MODULE_TITLES,
  ACCOUNTING_MODULES,
  getFirmDelegatedAccountingModules,
  isAccountingModuleHiddenFromHome
} from './accounting-modules.config';

describe('accounting-modules.config', () => {
  describe('ACCOUNTING_HOME_HIDDEN_MODULE_TITLES', () => {
    it('hides Budgétaire and Declarations from the accounting home hub', () => {
      expect(ACCOUNTING_HOME_HIDDEN_MODULE_TITLES.has('Budgétaire')).toBe(true);
      expect(ACCOUNTING_HOME_HIDDEN_MODULE_TITLES.has('Declarations')).toBe(true);
    });

    it('isAccountingModuleHiddenFromHome reflects the hub hidden set', () => {
      expect(isAccountingModuleHiddenFromHome('Budgétaire')).toBe(true);
      expect(isAccountingModuleHiddenFromHome('Declarations')).toBe(true);
      expect(isAccountingModuleHiddenFromHome('Gestion immobilisations')).toBe(false);
    });
  });

  describe('getFirmDelegatedAccountingModules', () => {
    it('includes Budgétaire and Declarations in delegated sidebar modules', () => {
      const titles = getFirmDelegatedAccountingModules().map(m => m.title);

      expect(titles).not.toContain('Configuration');
      expect(titles).not.toContain('Traitements');
      expect(titles).not.toContain('États');
      expect(titles).not.toContain('Liasse fiscale');
      expect(titles).toContain('Budgétaire');
      expect(titles).toContain('Declarations');
      expect(titles).toContain('Gestion immobilisations');
    });
  });

  describe('États icons', () => {
    it('uses a Free Font Awesome glyph for Journaux auxiliaires and unique icons', () => {
      const etatsModule = ACCOUNTING_MODULES.find(m => m.title === 'États');
      expect(etatsModule).toBeDefined();
      expect(etatsModule!.links.length).toBe(11);

      const subJournals = etatsModule!.links.find(l => l.route === '/accounting/sub-journals');
      expect(subJournals?.icon).toBe('fa-solid fa-book-bookmark');
      expect(etatsModule!.links.every(l => !l.icon.includes('fa-books'))).toBe(true);

      const icons = etatsModule!.links.map(l => l.icon);
      expect(new Set(icons).size).toBe(icons.length);
      expect(icons.every(icon => /^fa-solid fa-[a-z0-9-]+$/.test(icon))).toBe(true);
    });
  });
});
