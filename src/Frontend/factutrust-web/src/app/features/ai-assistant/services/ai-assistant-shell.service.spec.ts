import { TestBed } from '@angular/core/testing';
import { AiAssistantShellService } from './ai-assistant-shell.service';
import { AssistantAgentScope } from '../models/ai-chat.models';

describe('AiAssistantShellService', () => {
  let service: AiAssistantShellService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AiAssistantShellService);
  });

  it('starts with workspace tab closed on work pane', () => {
    expect(service.aiTabOpen()).toBeFalse();
    expect(service.activePane()).toBe('work');
    expect(service.openPanelTick()).toBe(0);
  });

  it('requestOpenPanel opens singleton tab and focuses AI pane', () => {
    service.requestOpenPanel({ url: '/quotes/new' });

    expect(service.aiTabOpen()).toBeTrue();
    expect(service.activePane()).toBe('ai');
    expect(service.openPanelTick()).toBe(1);
  });

  it('re-open focuses the same tab without resetting scope mid-conversation', () => {
    service.openAiGeneralTab({ url: '/invoices' });
    const scopeAfterFirstOpen = service.workspaceTabScope();

    service.activateWorkPane();
    service.requestOpenPanel({ url: '/quotes' });

    expect(service.aiTabOpen()).toBeTrue();
    expect(service.activePane()).toBe('ai');
    expect(service.workspaceTabScope()).toBe(scopeAfterFirstOpen);
  });

  it('closeAiTab returns to work pane without clearing tick', () => {
    service.requestOpenPanel({ url: '/dashboard' });
    service.closeAiTab();

    expect(service.aiTabOpen()).toBeFalse();
    expect(service.activePane()).toBe('work');
    expect(service.openPanelTick()).toBe(1);
  });

  it('activateWorkPane keeps aiTabOpen true', () => {
    service.requestOpenPanel({ url: '/dashboard' });
    service.activateWorkPane();

    expect(service.aiTabOpen()).toBeTrue();
    expect(service.activePane()).toBe('work');
  });

  it('resolves expert scope from target url on first open', () => {
    service.openAiGeneralTab({ url: '/invoices/unpaid' });
    expect(service.workspaceTabScope()).toBe(AssistantAgentScope.Sales);
  });

  it('firm delegated always resolves accounting scope', () => {
    service.openAiGeneralTab({ url: '/invoices', firmDelegated: true });
    expect(service.workspaceTabScope()).toBe(AssistantAgentScope.Accounting);
  });

  it('openAiGeneralTabFromRoute applies slug scope', () => {
    service.openAiGeneralTabFromRoute('/ai-assistant/stock');
    expect(service.workspaceTabScope()).toBe(AssistantAgentScope.Stock);
    expect(service.activePane()).toBe('ai');
  });

  it('isWorkspaceAiRoute detects assistant routes', () => {
    expect(service.isWorkspaceAiRoute('/ai-assistant')).toBeTrue();
    expect(service.isWorkspaceAiRoute('/ai-assistant/ventes')).toBeTrue();
    expect(service.isWorkspaceAiRoute('/invoices')).toBeFalse();
  });

  it('resetWorkspaceTabForDedicatedRoute closes workspace tab', () => {
    service.requestOpenPanel({ url: '/dashboard' });
    service.resetWorkspaceTabForDedicatedRoute();

    expect(service.aiTabOpen()).toBeFalse();
    expect(service.activePane()).toBe('work');
  });

  it('syncSuggestedScopeFromUrl updates scope only when tab closed', () => {
    service.syncSuggestedScopeFromUrl('/accounting/journal', false);
    expect(service.workspaceTabScope()).toBe(AssistantAgentScope.Accounting);

    service.requestOpenPanel({ url: '/invoices' });
    service.syncSuggestedScopeFromUrl('/accounting/journal', false);
    expect(service.workspaceTabScope()).toBe(AssistantAgentScope.Sales);
  });
});
