import {
  auditEventBelongsToRequest,
  auditEventLabel,
  canAssignRequest,
  canCompleteTask,
  canResolveRequest,
  canResumeRequest,
  canSetWaiting,
  documentIconClass,
  initialsFromName,
  isInternalNote,
  isRequestActionable,
  isRequestOpen,
  isThreadClosed,
  isThreadOpen,
  messageDayLabel,
  parseExchangeAuditEventType,
  parseExchangeMessageVisibility,
  parseExchangeRequestStatus,
  parseExchangeThreadStatus,
  participantRoleLabel,
  requestMatchesFilter,
  requestStatusBadge,
  requestStatusLabel,
  threadStatusLabel
} from './exchange-status';

describe('exchange-status', () => {
  it('parses thread Open/Closed from PascalCase and numbers', () => {
    expect(parseExchangeThreadStatus('Open')).toBe('Open');
    expect(parseExchangeThreadStatus('Closed')).toBe('Closed');
    expect(parseExchangeThreadStatus(0)).toBe('Open');
    expect(parseExchangeThreadStatus(1)).toBe('Closed');
    expect(parseExchangeThreadStatus('0')).toBe('Open');
    expect(isThreadOpen('Open')).toBe(true);
    expect(isThreadClosed('Closed')).toBe(true);
    expect(threadStatusLabel('Open')).toBe('En cours');
  });

  it('parses message visibility', () => {
    expect(parseExchangeMessageVisibility('ClientVisible')).toBe('ClientVisible');
    expect(parseExchangeMessageVisibility('InternalNote')).toBe('InternalNote');
    expect(parseExchangeMessageVisibility(1)).toBe('InternalNote');
    expect(isInternalNote('InternalNote')).toBe(true);
    expect(isInternalNote(0)).toBe(false);
  });

  it('parses request status helpers', () => {
    expect(parseExchangeRequestStatus('InProgress')).toBe('InProgress');
    expect(isRequestOpen('Open')).toBe(true);
    expect(isRequestActionable('Resolved')).toBe(true);
    expect(isRequestActionable('Closed')).toBe(false);
    expect(canResolveRequest('WaitingClient')).toBe(true);
    expect(requestStatusLabel('WaitingFirm')).toBe('Attente cabinet');
    expect(requestStatusLabel('Closed')).toBe('Clôturée');
  });

  it('parses task helpers', () => {
    expect(canCompleteTask('Todo')).toBe(true);
    expect(canCompleteTask('Done')).toBe(false);
  });

  it('builds day labels and initials', () => {
    const today = new Date();
    expect(messageDayLabel(today.toISOString(), today)).toBe("Aujourd'hui");
    expect(initialsFromName('Marie Dupont')).toBe('MD');
    expect(initialsFromName('Jean')).toBe('JE');
  });

  it('parses audit event types from PascalCase and numbers', () => {
    expect(parseExchangeAuditEventType('RequestCreated')).toBe('RequestCreated');
    expect(parseExchangeAuditEventType('DocumentShared')).toBe('DocumentShared');
    expect(parseExchangeAuditEventType(3)).toBe('RequestCreated');
    expect(parseExchangeAuditEventType(7)).toBe('DocumentShared');
    expect(auditEventLabel('RequestCreated')).toBe('Demande créée');
    expect(auditEventLabel('MessageSent')).toBe('Message envoyé');
    expect(auditEventLabel(1)).toBe('Message envoyé');
    expect(auditEventLabel('UnknownThing')).toBe('Événement UnknownThing');
  });

  it('formats participant roles and document icons', () => {
    expect(participantRoleLabel('Administrator')).toBe('Administrateur');
    expect(participantRoleLabel('FirmManager')).toBe('Responsable cabinet');
    expect(participantRoleLabel('FirmAccountant')).toBe('Comptable cabinet');
    expect(participantRoleLabel('CustomRole')).toBe('CustomRole');
    expect(documentIconClass('bilan.pdf')).toBe('pi-file-pdf');
    expect(documentIconClass('notes.docx')).toBe('pi-file-word');
    expect(documentIconClass('data.xlsx')).toBe('pi-file-excel');
    expect(documentIconClass('readme.txt')).toBe('pi-file');
  });

  it('maps request badges distinctly and forbids assign on resolved', () => {
    expect(requestStatusBadge('Open')).toBe('overdue');
    expect(requestStatusBadge('Resolved')).toBe('accepted');
    expect(requestStatusBadge('Closed')).toBe('inactive');
    expect(canAssignRequest('Open', true)).toBe(true);
    expect(canAssignRequest('Open', false)).toBe(false);
    expect(canAssignRequest('Resolved', true)).toBe(false);
    expect(canSetWaiting('InProgress')).toBe(true);
    expect(canResumeRequest('WaitingClient')).toBe(true);
    expect(canResumeRequest('Open')).toBe(false);
  });

  it('filters requests by status, search and number', () => {
    expect(requestMatchesFilter('Open', 'active', '', 'TVA', '', 1)).toBe(true);
    expect(requestMatchesFilter('Resolved', 'active', '', 'TVA', '', 1)).toBe(false);
    expect(requestMatchesFilter('Open', 'all', 'tva', 'Réclamation TVA', '', 2)).toBe(true);
    expect(requestMatchesFilter('Open', 'all', '9', 'Autre', '', 2)).toBe(false);
    expect(requestMatchesFilter('Open', 'all', '2', 'Autre', '', 2)).toBe(true);
  });

  it('matches audit events to a request via payload', () => {
    expect(parseExchangeAuditEventType(12)).toBe('RequestCommented');
    expect(parseExchangeAuditEventType(13)).toBe('RequestAssigned');
    expect(auditEventLabel('RequestCommented')).toBe('Commentaire sur une demande');
    expect(
      auditEventBelongsToRequest('RequestStatusChanged', '{"requestId":"r1","status":"Resolved"}', {
        id: 'r1',
        number: 1
      })
    ).toBe(true);
    expect(
      auditEventBelongsToRequest('RequestCreated', '{"number":2}', { id: 'r9', number: 2 })
    ).toBe(true);
    expect(
      auditEventBelongsToRequest('RequestCreated', '{"number":1}', { id: 'r9', number: 2 })
    ).toBe(false);
  });
});
