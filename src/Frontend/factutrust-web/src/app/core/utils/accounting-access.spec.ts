import { canValidateAccountingEntries } from './accounting-access';
import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

describe('accounting-access', () => {
  function mockAuth(overrides: { perms?: string[]; isAccountingFirm?: boolean; isDelegatedMode?: boolean } = {}): AuthService {
    const perms = overrides.perms ?? [];
    return {
      hasPermission: (p: string) => perms.includes(p),
      isAccountingFirm: () => overrides.isAccountingFirm ?? false,
      isDelegatedMode: () => overrides.isDelegatedMode ?? false
    } as unknown as AuthService;
  }

  it('canValidateAccountingEntries returns false for company users', () => {
    expect(canValidateAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.validate],
      isAccountingFirm: false,
      isDelegatedMode: false
    }))).toBe(false);
  });

  it('canValidateAccountingEntries returns false for accounting firm in native mode', () => {
    expect(canValidateAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.validate],
      isAccountingFirm: true,
      isDelegatedMode: false
    }))).toBe(false);
  });

  it('canValidateAccountingEntries returns false for delegated firm without permission', () => {
    expect(canValidateAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.read],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(false);
  });

  it('canValidateAccountingEntries returns true for delegated firm with accounting:validate', () => {
    expect(canValidateAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.validate],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(true);
  });
});
