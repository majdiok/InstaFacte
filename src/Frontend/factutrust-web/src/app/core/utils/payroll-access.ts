import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

/** True when the user may create/edit employees, contracts, leaves and advances. */
export function canManagePayrollEmployees(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.manageEmployees);
}

export function canRunPayroll(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.run);
}

export function canValidatePayroll(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.validate);
}

export function canConfigurePayroll(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.settings);
}

export function canDeclarePayroll(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.declare);
}

export function canExportPayroll(auth: AuthService): boolean {
  return auth.hasPermission(PERMISSIONS.payroll.export);
}

/** Accounting firm in delegated mode without employee management rights. */
export function isPayrollConsultMode(auth: AuthService): boolean {
  return auth.isAccountingFirm() && auth.isDelegatedMode() && !canManagePayrollEmployees(auth);
}
