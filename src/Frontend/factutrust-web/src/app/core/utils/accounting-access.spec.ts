import { canDeleteDraftAccountingEntries, canEditDraftAccountingEntries, canValidateAccountingEntries } from './accounting-access';
import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

describe('accounting-access', () => {
  function mockAuth(overrides: {
    perms?: string[];
    isAccountingFirm?: boolean;
    isDelegatedMode?: boolean;
    isFirmManager?: boolean;
    isFirmAccountant?: boolean;
  } = {}): AuthService {
    const perms = overrides.perms ?? [];
    return {
      hasPermission: (p: string) => perms.includes(p),
      isAccountingFirm: () => overrides.isAccountingFirm ?? false,
      isDelegatedMode: () => overrides.isDelegatedMode ?? false,
      isFirmManager: () => overrides.isFirmManager ?? false,
      isFirmAccountant: () => overrides.isFirmAccountant ?? false
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

  it('canDeleteDraftAccountingEntries returns false for company users', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.delete],
      isAccountingFirm: false,
      isDelegatedMode: false,
      isFirmManager: true
    }))).toBe(false);
  });

  it('canDeleteDraftAccountingEntries returns false for accounting firm in native mode', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.delete],
      isAccountingFirm: true,
      isDelegatedMode: false,
      isFirmManager: true
    }))).toBe(false);
  });

  it('canDeleteDraftAccountingEntries returns false for delegated firm manager without permission', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.read],
      isAccountingFirm: true,
      isDelegatedMode: true,
      isFirmManager: true
    }))).toBe(false);
  });

  it('canDeleteDraftAccountingEntries returns true for delegated firm manager with accounting:delete', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.delete],
      isAccountingFirm: true,
      isDelegatedMode: true,
      isFirmManager: true
    }))).toBe(true);
  });

  it('canDeleteDraftAccountingEntries returns true for delegated assigned accountant with accounting:delete', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.delete],
      isAccountingFirm: true,
      isDelegatedMode: true,
      isFirmAccountant: true
    }))).toBe(true);
  });

  it('canDeleteDraftAccountingEntries returns false for delegated user with permission but neither manager nor accountant role', () => {
    expect(canDeleteDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.delete],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(false);
  });

  it('canEditDraftAccountingEntries returns false for company users', () => {
    expect(canEditDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.create],
      isAccountingFirm: false,
      isDelegatedMode: false
    }))).toBe(false);
  });

  it('canEditDraftAccountingEntries returns false for accounting firm in native mode', () => {
    expect(canEditDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.create],
      isAccountingFirm: true,
      isDelegatedMode: false
    }))).toBe(false);
  });

  it('canEditDraftAccountingEntries returns false for delegated firm without accounting:create', () => {
    expect(canEditDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.read],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(false);
  });

  it('canEditDraftAccountingEntries returns true for delegated firm with accounting:create', () => {
    expect(canEditDraftAccountingEntries(mockAuth({
      perms: [PERMISSIONS.accounting.create],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(true);
  });
});
