import { TestBed } from '@angular/core/testing';
import {
  MessageSelectionService,
  MESSAGE_SELECTION_MAX,
  SelectedAssistantMessage
} from './message-selection.service';

function makeMessage(id: string, conversationId = 'c-1'): SelectedAssistantMessage {
  return {
    conversationId,
    messageId: id,
    conversationTitle: 'Conversation ' + conversationId,
    preview: 'Preview for ' + id,
    createdAt: new Date('2026-05-21T12:00:00Z').toISOString()
  };
}

describe('MessageSelectionService', () => {
  let service: MessageSelectionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(MessageSelectionService);
  });

  it('starts empty and not in selection mode', () => {
    expect(service.count()).toBe(0);
    expect(service.isSelectionMode()).toBe(false);
    expect(service.hasSelection()).toBe(false);
    expect(service.atLimit()).toBe(false);
  });

  it('toggles a message in and out of the selection', () => {
    const m = makeMessage('m-1');
    service.toggle(m);
    expect(service.count()).toBe(1);
    expect(service.isSelected('c-1', 'm-1')).toBe(true);

    service.toggle(m);
    expect(service.count()).toBe(0);
    expect(service.isSelected('c-1', 'm-1')).toBe(false);
  });

  it('supports cross-conversation selection', () => {
    service.add(makeMessage('m-1', 'c-1'));
    service.add(makeMessage('m-2', 'c-2'));
    expect(service.count()).toBe(2);
    expect(service.selections().map(s => s.conversationId).sort()).toEqual(['c-1', 'c-2']);
  });

  it('does not exceed the hard limit', () => {
    for (let i = 0; i < MESSAGE_SELECTION_MAX + 5; i++) {
      service.add(makeMessage('m-' + i));
    }
    expect(service.count()).toBe(MESSAGE_SELECTION_MAX);
    expect(service.atLimit()).toBe(true);
  });

  it('clears all selections', () => {
    service.add(makeMessage('m-1'));
    service.add(makeMessage('m-2'));
    service.clear();
    expect(service.count()).toBe(0);
  });

  it('toggles selection mode independently of the selection set', () => {
    service.add(makeMessage('m-1'));
    expect(service.isSelectionMode()).toBe(false);

    service.enterSelectionMode();
    expect(service.isSelectionMode()).toBe(true);
    // entering mode must NOT clear the prior selection
    expect(service.count()).toBe(1);

    service.exitSelectionMode();
    expect(service.isSelectionMode()).toBe(false);
    // exiting mode resets the selection
    expect(service.count()).toBe(0);
  });

  it('reorders preserving every entry', () => {
    service.add(makeMessage('a'));
    service.add(makeMessage('b'));
    service.add(makeMessage('c'));

    service.reorder([
      { conversationId: 'c-1', messageId: 'c' },
      { conversationId: 'c-1', messageId: 'a' },
      { conversationId: 'c-1', messageId: 'b' }
    ]);

    expect(service.selections().map(s => s.messageId)).toEqual(['c', 'a', 'b']);
  });

  it('ignores invalid reorder calls', () => {
    service.add(makeMessage('a'));
    service.add(makeMessage('b'));

    const before = service.selections().map(s => s.messageId);
    service.reorder([
      { conversationId: 'c-1', messageId: 'a' },
      { conversationId: 'c-1', messageId: 'unknown' }
    ]);
    expect(service.selections().map(s => s.messageId)).toEqual(before);
  });

  it('remaps message ids after server reconciliation', () => {
    service.add(makeMessage('client-id-1', 'c-1'));
    service.add(makeMessage('client-id-2', 'c-2'));

    service.remapMessageId('client-id-1', 'server-id-1');

    expect(service.isSelected('c-1', 'client-id-1')).toBe(false);
    expect(service.isSelected('c-1', 'server-id-1')).toBe(true);
    expect(service.isSelected('c-2', 'client-id-2')).toBe(true);
    expect(service.selections().find(s => s.conversationId === 'c-1')?.messageId).toBe('server-id-1');
  });
});
