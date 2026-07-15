import {
  canConfigurePayroll,
  canDeclarePayroll,
  canExportPayroll,
  canManagePayrollEmployees,
  canRunPayroll,
  canValidatePayroll,
  isPayrollConsultMode
} from './payroll-access';
import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

describe('payroll-access', () => {
  function mockAuth(overrides: { perms?: string[]; isAccountingFirm?: boolean; isDelegatedMode?: boolean } = {}): AuthService {
    const perms = overrides.perms ?? [];
    return {
      hasPermission: (p: string) => perms.includes(p),
      isAccountingFirm: () => overrides.isAccountingFirm ?? false,
      isDelegatedMode: () => overrides.isDelegatedMode ?? false
    } as unknown as AuthService;
  }

  it('canManagePayrollEmployees returns true when user has payroll:manage_employees', () => {
    expect(canManagePayrollEmployees(mockAuth({ perms: [PERMISSIONS.payroll.manageEmployees, PERMISSIONS.payroll.read] }))).toBe(true);
  });

  it('canManagePayrollEmployees returns false when user lacks payroll:manage_employees', () => {
    expect(canManagePayrollEmployees(mockAuth({ perms: [PERMISSIONS.payroll.read, PERMISSIONS.payroll.run] }))).toBe(false);
  });

  it('canRunPayroll returns true when user has payroll:run', () => {
    expect(canRunPayroll(mockAuth({ perms: [PERMISSIONS.payroll.run] }))).toBe(true);
  });

  it('canRunPayroll returns false when user lacks payroll:run', () => {
    expect(canRunPayroll(mockAuth({ perms: [PERMISSIONS.payroll.read] }))).toBe(false);
  });

  it('canValidatePayroll returns true when user has payroll:validate', () => {
    expect(canValidatePayroll(mockAuth({ perms: [PERMISSIONS.payroll.validate] }))).toBe(true);
  });

  it('canValidatePayroll returns false when user lacks payroll:validate', () => {
    expect(canValidatePayroll(mockAuth({ perms: [PERMISSIONS.payroll.run] }))).toBe(false);
  });

  it('canConfigurePayroll returns true when user has payroll:settings', () => {
    expect(canConfigurePayroll(mockAuth({ perms: [PERMISSIONS.payroll.settings] }))).toBe(true);
  });

  it('canDeclarePayroll returns true when user has payroll:declare', () => {
    expect(canDeclarePayroll(mockAuth({ perms: [PERMISSIONS.payroll.declare] }))).toBe(true);
  });

  it('canExportPayroll returns true when user has payroll:export', () => {
    expect(canExportPayroll(mockAuth({ perms: [PERMISSIONS.payroll.export] }))).toBe(true);
  });

  it('isPayrollConsultMode returns true for accounting firm in delegated mode without manage rights', () => {
    expect(isPayrollConsultMode(mockAuth({
      perms: [PERMISSIONS.payroll.read],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(true);
  });

  it('isPayrollConsultMode returns false when user can manage employees', () => {
    expect(isPayrollConsultMode(mockAuth({
      perms: [PERMISSIONS.payroll.manageEmployees],
      isAccountingFirm: true,
      isDelegatedMode: true
    }))).toBe(false);
  });
});
