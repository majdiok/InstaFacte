import {
  ACCOUNTING_HOME_HIDDEN_MODULE_TITLES,
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
});
