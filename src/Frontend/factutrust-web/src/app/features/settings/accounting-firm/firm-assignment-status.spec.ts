import {
  FirmAssignmentStatusCode,
  firmAssignmentStatusView,
  isOpenAssignment
} from './firm-assignment-status';

describe('firm-assignment-status', () => {
  it('maps each known status to a French label and a design-system badge', () => {
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

  it('does not surface the backend "Active" wording — uses "Liaison active"', () => {
    expect(firmAssignmentStatusView(FirmAssignmentStatusCode.Active).label).not.toBe('Active');
  });

  it('falls back to a safe view for an unknown status code', () => {
    const view = firmAssignmentStatusView(999);
    expect(view.label).toBe('Inconnu');
    expect(view.badge).toBe('inactive');
  });

  it('treats only pending and active as open assignments', () => {
    expect(isOpenAssignment(FirmAssignmentStatusCode.PendingFirmApproval)).toBe(true);
    expect(isOpenAssignment(FirmAssignmentStatusCode.Active)).toBe(true);
    expect(isOpenAssignment(FirmAssignmentStatusCode.Rejected)).toBe(false);
    expect(isOpenAssignment(FirmAssignmentStatusCode.RevokedByCompany)).toBe(false);
    expect(isOpenAssignment(FirmAssignmentStatusCode.CancelledByCompany)).toBe(false);
  });
});
