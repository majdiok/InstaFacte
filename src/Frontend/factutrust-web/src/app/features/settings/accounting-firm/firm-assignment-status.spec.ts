import {
  FirmAssignmentStatusCode,
  firmAssignmentStatusView,
  isActiveAssignment,
  isOpenAssignment,
  isPendingAssignment,
  isRejectedAssignment,
  parseFirmAssignmentStatus
} from './firm-assignment-status';

describe('firm-assignment-status', () => {
  const allPascal = [
    'PendingFirmApproval',
    'Active',
    'Rejected',
    'RevokedByCompany',
    'RevokedByFirm',
    'CancelledByCompany'
  ] as const;

  it('maps each known numeric status to a French label and a design-system badge', () => {
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.PendingFirmApproval)).toEqual({
      label: "En attente d'acceptation",
      badge: 'pending'
    });
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.Active)).toEqual({
      label: 'Liaison active',
      badge: 'active'
    });
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.Rejected).badge).toBe('rejected');
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.CancelledByCompany).label).toBe('Annulée');
  });

  it('maps PascalCase API strings the same way as numeric codes', () => {
    expect(firmAssignmentStatusView('PendingFirmApproval')).toEqual({
      label: "En attente d'acceptation",
      badge: 'pending'
    });
    expect(firmAssignmentStatusView('Active')).toEqual({
      label: 'Liaison active',
      badge: 'active'
    });
    expect(firmAssignmentStatusView('Rejected').badge).toBe('rejected');
    expect(firmAssignmentStatusView('CancelledByCompany').label).toBe('Annulée');
  });

  it('parses numeric strings 0–5', () => {
    expect(parseFirmAssignmentStatus('0')).toBe(FirmAssignmentStatusCode.PendingFirmApproval);
    expect(parseFirmAssignmentStatus('1')).toBe(FirmAssignmentStatusCode.Active);
    expect(parseFirmAssignmentStatus('5')).toBe(FirmAssignmentStatusCode.CancelledByCompany);
  });

  it('parses every PascalCase enum name', () => {
    allPascal.forEach((name, index) => {
      expect(parseFirmAssignmentStatus(name)).toBe(index);
    });
  });

  it('does not surface the backend "Active" wording — uses "Liaison active"', () => {
    expect(firmAssignmentStatusView('Active').label).not.toBe('Active');
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.Active).label).not.toBe('Active');
  });

  it('falls back to a safe view for an unknown status code', () => {
    expect(firmAssignmentStatusView(999)).toEqual({ label: 'Inconnu', badge: 'inactive' });
    expect(firmAssignmentStatusView('Nope')).toEqual({ label: 'Inconnu', badge: 'inactive' });
    expect(parseFirmAssignmentStatus(null)).toBeNull();
    expect(parseFirmAssignmentStatus(undefined)).toBeNull();
  });

  it('treats only pending and active as open assignments (number and string)', () => {
    expect(isOpenAssignment(FirmAssignmentStatusCode.PendingFirmApproval)).toBe(true);
    expect(isOpenAssignment(FirmAssignmentStatusCode.Active)).toBe(true);
    expect(isOpenAssignment('PendingFirmApproval')).toBe(true);
    expect(isOpenAssignment('Active')).toBe(true);
    expect(isOpenAssignment(FirmAssignmentStatusCode.Rejected)).toBe(false);
    expect(isOpenAssignment('Rejected')).toBe(false);
    expect(isOpenAssignment(FirmAssignmentStatusCode.RevokedByCompany)).toBe(false);
    expect(isOpenAssignment(FirmAssignmentStatusCode.CancelledByCompany)).toBe(false);
  });

  it('isActiveAssignment / isPendingAssignment / isRejectedAssignment', () => {
    expect(isActiveAssignment('Active')).toBe(true);
    expect(isActiveAssignment(1)).toBe(true);
    expect(isActiveAssignment('PendingFirmApproval')).toBe(false);

    expect(isPendingAssignment('PendingFirmApproval')).toBe(true);
    expect(isPendingAssignment(0)).toBe(true);
    expect(isPendingAssignment('Active')).toBe(false);

    expect(isRejectedAssignment('Rejected')).toBe(true);
    expect(isRejectedAssignment(2)).toBe(true);
    expect(isRejectedAssignment('Active')).toBe(false);
  });
});
