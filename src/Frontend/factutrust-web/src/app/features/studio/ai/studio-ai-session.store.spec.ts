import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { ChatStreamEvent } from '@features/ai-assistant/models/ai-chat.models';
import { StudioAiBuildService, StudioPlanSummary } from '../studio-ai-build.service';
import { StudioNavService } from '../studio-nav.service';
import { STUDIO_AI_LABELS } from './studio-ai-labels';
import { StudioSystemSpec } from './studio-ai.models';
import {
  StudioAiSessionStore,
  normalizeSummary,
  parseActions,
  parsePlanSummary
} from './studio-ai-session.store';

/** Spec renvoyée par `GET {id}/spec` dans les tests. */
function spec(): StudioSystemSpec {
  return {
    system: { displayName: 'Congés' },
    entities: [
      {
        ref: 'employe',
        displayName: 'Employé',
        displayNamePlural: 'Employés',
        fields: [{ key: 'nom', label: 'Nom', type: 'text', required: true, unique: false }]
      }
    ]
  };
}

function summary(over: Partial<StudioPlanSummary> = {}): StudioPlanSummary {
  return {
    kind: 'CreateSystem',
    title: 'Gestion des congés',
    steps: [],
    entities: [],
    warnings: [],
    ...over
  } as StudioPlanSummary;
}

function planEvent(planId = 'p-1'): ChatStreamEvent {
  return { type: 'studio_plan', content: JSON.stringify({ planId, summary: summary() }) };
}

describe('StudioAiSessionStore', () => {
  let store: StudioAiSessionStore;
  let stream: jasmine.SpyObj<AiStreamService>;
  let builds: jasmine.SpyObj<StudioAiBuildService>;
  let nav: jasmine.SpyObj<StudioNavService>;

  const specResponse = (rowVersion = 'rv-1') => ({
    success: true,
    data: { id: 'p-1', kind: 'CreateSystem', status: 'Pending', expiresAt: '2026-09-09T10:00:00Z', rowVersion, spec: spec() },
    message: null,
    errors: []
  });

  beforeEach(() => {
    stream = jasmine.createSpyObj<AiStreamService>('AiStreamService', ['streamChat']);
    builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', [
      'confirm', 'cancel', 'cancelPending', 'getPlanSpec', 'updatePlanSpec'
    ]);
    nav = jasmine.createSpyObj<StudioNavService>('StudioNavService', ['refresh']);

    stream.streamChat.and.returnValue(of());
    builds.getPlanSpec.and.returnValue(of(specResponse()) as never);
    builds.confirm.and.returnValue(of() as never);
    builds.cancel.and.returnValue(of({ success: true, data: null, message: null, errors: [] }) as never);
    builds.cancelPending.and.returnValue(of({ success: true, data: 1, message: null, errors: [] }) as never);

    TestBed.configureTestingModule({
      providers: [
        StudioAiSessionStore,
        { provide: AiStreamService, useValue: stream },
        { provide: StudioAiBuildService, useValue: builds },
        { provide: StudioNavService, useValue: nav },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate'), navigateByUrl: jasmine.createSpy('navigateByUrl') } }
      ]
    });
    store = TestBed.inject(StudioAiSessionStore);
  });

  describe('send', () => {
    it('ignores an empty message', () => {
      store.send('   ');
      expect(stream.streamChat).not.toHaveBeenCalled();
      expect(store.phase()).toBe('idle');
    });

    it('switches to planning and pushes the user message', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Créer un système de congés', { intent: 'system' });

      expect(store.phase()).toBe('planning');
      expect(store.status()).toBe(STUDIO_AI_LABELS.status.analyzing);
      expect(store.intent()).toBe('system');
      expect(store.timeline()).toEqual([{ kind: 'text', role: 'user', text: 'Créer un système de congés' }]);
      expect(stream.streamChat.calls.mostRecent().args[0].message).toBe('Créer un système de congés');
    });

    it('moves to awaiting_confirmation on studio_plan and loads the spec', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Créer un système de congés');
      events.next(planEvent());

      expect(store.phase()).toBe('awaiting_confirmation');
      expect(store.status()).toBe(STUDIO_AI_LABELS.status.awaitingValidation);
      expect(builds.getPlanSpec).toHaveBeenCalledWith('p-1');
      expect(store.plan()?.planId).toBe('p-1');
      expect(store.plan()?.rowVersion).toBe('rv-1');
      expect(store.plan()?.expiresAt).toBe('2026-09-09T10:00:00Z');
      expect(store.spec()?.entities.length).toBe(1);
      expect(store.draft()).toEqual(store.spec());
      expect(store.draft()).not.toBe(store.spec());
      expect(store.counters().entities).toBe(1);
      expect(store.dirty()).toBeFalse();
      expect(store.canConfirm()).toBeTrue();
      expect(store.canEdit()).toBeTrue();
    });

    it('reports an unreadable spec payload', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());
      builds.getPlanSpec.and.returnValue(of({ success: true, data: { spec: 'pas du json' }, message: null, errors: [] }) as never);

      store.send('Créer');
      events.next(planEvent());

      expect(store.error()).toBe(STUDIO_AI_LABELS.errors.invalidSpec);
      expect(store.spec()).toBeNull();
    });

    it('keeps the plan when the chat stream fails after the plan arrived', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Créer');
      events.next(planEvent());
      events.error(new Error('flux coupé'));

      expect(store.phase()).toBe('awaiting_confirmation');
      expect(store.error()).toBe('flux coupé');
      expect(store.plan()?.planId).toBe('p-1');
    });

    it('fails when the chat stream errors before any plan', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Créer');
      events.error(new Error('flux coupé'));

      expect(store.phase()).toBe('failed');
      expect(store.plan()).toBeNull();
    });

    it('flushes the assistant text and refreshes the nav when no plan was produced', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Bonjour');
      events.next({ type: 'content', content: 'Voici ' });
      events.next({ type: 'content', content: 'la réponse.' });
      events.next({ type: 'done', conversationId: 'c-1' });
      events.complete();

      expect(store.conversationId()).toBe('c-1');
      expect(store.timeline()[1]).toEqual({ kind: 'text', role: 'assistant', text: 'Voici la réponse.' });
      expect(nav.refresh).toHaveBeenCalled();
      expect(store.phase()).toBe('idle');
    });

    it('collects suggestions and client actions', () => {
      const events = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(events.asObservable());

      store.send('Créer');
      events.next({ type: 'suggested_prompts', suggestedPrompts: JSON.stringify(['Ajouter un rapport', 42]) });
      events.next({ type: 'client_actions', clientActions: JSON.stringify([{ label: 'Ouvrir', route: '/studio' }, { label: 'Non', route: 'studio' }]) });

      expect(store.suggestions()).toEqual(['Ajouter un rapport']);
      expect(store.actions()).toEqual([{ label: 'Ouvrir', route: '/studio' }]);
    });
  });

  describe('confirm', () => {
    function withPlan(): Subject<ChatStreamEvent> {
      const chat = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(chat.asObservable());
      store.send('Créer');
      chat.next(planEvent());
      return chat;
    }

    it('executes the plan then completes and refreshes the nav', () => {
      withPlan();
      const exec = new Subject<ChatStreamEvent>();
      builds.confirm.and.returnValue(exec.asObservable() as never);

      store.confirm();
      expect(store.phase()).toBe('executing');
      expect(store.status()).toBe(STUDIO_AI_LABELS.status.creating);
      expect(builds.confirm).toHaveBeenCalledWith('p-1');

      exec.next({ type: 'studio_progress', content: JSON.stringify({ phase: 'entities', label: 'Tables', status: 'running' }) });
      exec.next({ type: 'studio_progress', content: JSON.stringify({ phase: 'entities', label: 'Tables', status: 'done' }) });
      exec.next({
        type: 'studio_result',
        content: JSON.stringify({
          success: true, systemKey: 'conges', systemUrl: '/studio/systems/conges', displayName: 'Congés',
          entityCount: 1, entities: [], warnings: [], message: 'ok'
        })
      });
      exec.complete();

      expect(store.buildSteps().length).toBe(1);
      expect(store.buildSteps()[0].status).toBe('done');
      expect(store.phase()).toBe('completed');
      expect(store.resultUrl()).toBe('/studio/systems/conges');
      expect(store.timeline().some(i => i.kind === 'system' && i.text === STUDIO_AI_LABELS.status.created)).toBeTrue();
      expect(nav.refresh).toHaveBeenCalled();
    });

    it('fails and clears the plan when the execution stream errors', () => {
      withPlan();
      builds.confirm.and.returnValue(throwError(() => new Error('boom')) as never);

      store.confirm();

      expect(store.phase()).toBe('failed');
      expect(store.error()).toBe('boom');
      expect(store.plan()).toBeNull();
      expect(store.spec()).toBeNull();
      expect(store.draft()).toBeNull();
    });

    it('fails on an error event inside the execution stream', () => {
      withPlan();
      const exec = new Subject<ChatStreamEvent>();
      builds.confirm.and.returnValue(exec.asObservable() as never);

      store.confirm();
      exec.next({ type: 'error', error: 'quota' });

      expect(store.phase()).toBe('failed');
      expect(store.error()).toBe('quota');
    });

    it('does nothing while the draft is dirty', () => {
      withPlan();
      const draft = store.draft()!;
      store.startEditing();
      store.updateDraft({ ...draft, system: { displayName: 'Autre' } });

      expect(store.dirty()).toBeTrue();
      expect(store.canConfirm()).toBeFalse();
      store.confirm();
      expect(builds.confirm).not.toHaveBeenCalled();
    });
  });

  describe('cancelPlan / resetConversation', () => {
    function withPlan(): void {
      const chat = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(chat.asObservable());
      store.send('Créer');
      chat.next(planEvent());
    }

    it('cancels the plan, returns to idle and logs a system item', () => {
      withPlan();

      store.cancelPlan();

      expect(builds.cancel).toHaveBeenCalledWith('p-1');
      expect(store.phase()).toBe('idle');
      expect(store.plan()).toBeNull();
      expect(store.timeline().pop()).toEqual({ kind: 'system', text: STUDIO_AI_LABELS.status.planCancelled });
    });

    it('closes the preview even when the cancel call fails', () => {
      withPlan();
      builds.cancel.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })) as never);

      store.cancelPlan();

      expect(store.phase()).toBe('idle');
      expect(store.plan()).toBeNull();
    });

    it('cancels the pending plans before resetting the conversation', () => {
      withPlan();

      store.resetConversation();

      expect(builds.cancelPending).toHaveBeenCalled();
      expect(store.phase()).toBe('idle');
      expect(store.timeline()).toEqual([]);
      expect(store.plan()).toBeNull();
      expect(store.conversationId()).toBeUndefined();
      expect(store.lastPrompt()).toBe('');
    });

    it('does not call cancel-pending when no plan is pending', () => {
      store.resetConversation();
      expect(builds.cancelPending).not.toHaveBeenCalled();
      expect(store.phase()).toBe('idle');
    });
  });

  describe('saveDraft', () => {
    function editedPlan(): StudioSystemSpec {
      const chat = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(chat.asObservable());
      store.send('Créer');
      chat.next(planEvent());
      store.startEditing();
      const next: StudioSystemSpec = { ...store.draft()!, system: { displayName: 'Congés 2026' } };
      store.updateDraft(next);
      return next;
    }

    it('saves the draft, refreshes the rowVersion and logs the confirmation', () => {
      const next = editedPlan();
      const saved = { ...next, system: { displayName: 'Congés 2026' } };
      builds.updatePlanSpec.and.returnValue(of({
        success: true,
        data: {
          plan: { summaryJson: JSON.stringify(summary({ title: 'Congés 2026', warnings: ['Vérifiez les soldes.'] })) },
          spec: { rowVersion: 'rv-2', expiresAt: '2026-09-09T12:00:00Z', spec: saved }
        },
        message: null,
        errors: []
      }) as never);

      store.saveDraft();

      expect(builds.updatePlanSpec).toHaveBeenCalled();
      const args = builds.updatePlanSpec.calls.mostRecent().args;
      expect(args[0]).toBe('p-1');
      expect(JSON.parse(args[1] as string).system.displayName).toBe('Congés 2026');
      expect(args[2]).toBe('rv-1');
      expect(store.plan()?.rowVersion).toBe('rv-2');
      expect(store.plan()?.summary.title).toBe('Congés 2026');
      expect(store.dirty()).toBeFalse();
      expect(store.phase()).toBe('awaiting_confirmation');
      expect(store.validation()).toEqual({ warnings: ['Vérifiez les soldes.'], errors: [], pending: false });
      expect(store.warnings()).toEqual(['Vérifiez les soldes.']);
      expect(store.timeline().pop()).toEqual({ kind: 'system', text: STUDIO_AI_LABELS.status.draftSaved });
    });

    it('surfaces the conflict message on 409 without losing the draft', () => {
      const next = editedPlan();
      builds.updatePlanSpec.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })) as never);

      store.saveDraft();

      expect(store.validation().errors).toEqual([STUDIO_AI_LABELS.errors.conflict]);
      expect(store.validation().pending).toBeFalse();
      expect(store.draft()?.system.displayName).toBe(next.system.displayName);
      expect(store.dirty()).toBeTrue();
    });

    it('reports a non successful envelope', () => {
      editedPlan();
      builds.updatePlanSpec.and.returnValue(of({ success: false, data: null, message: 'ko', errors: [] }) as never);

      store.saveDraft();

      expect(store.validation().errors).toEqual([STUDIO_AI_LABELS.errors.generic]);
    });

    it('does nothing when the draft is unchanged', () => {
      const chat = new Subject<ChatStreamEvent>();
      stream.streamChat.and.returnValue(chat.asObservable());
      store.send('Créer');
      chat.next(planEvent());

      store.saveDraft();

      expect(builds.updatePlanSpec).not.toHaveBeenCalled();
    });

    it('resetDraft restores the server spec and leaves editing mode', () => {
      editedPlan();
      expect(store.dirty()).toBeTrue();

      store.resetDraft();

      expect(store.dirty()).toBeFalse();
      expect(store.phase()).toBe('awaiting_confirmation');
      expect(store.draft()).toEqual(store.spec());
    });
  });

  describe('openPlan', () => {
    it('reopens a pending plan and loads its spec', () => {
      store.openPlan('p-7', 'CreateSystem', summary({ title: 'Congés' }), '2026-09-10T10:00:00Z');

      expect(store.phase()).toBe('awaiting_confirmation');
      expect(store.plan()?.planId).toBe('p-7');
      expect(builds.getPlanSpec).toHaveBeenCalledWith('p-7');
      expect(store.spec()).not.toBeNull();
    });

    it('maps an HTTP failure to a French message', () => {
      builds.getPlanSpec.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })) as never);

      store.openPlan('p-7', 'CreateSystem', summary());

      expect(store.error()).toBe(STUDIO_AI_LABELS.errors.planNotFound);
      expect(store.specLoading()).toBeFalse();
    });
  });

  describe('pure helpers', () => {
    it('normalizeSummary fills the missing collections', () => {
      const normalized = normalizeSummary({ title: 'X' } as StudioPlanSummary);
      expect(normalized).toEqual({ kind: '', title: 'X', steps: [], entities: [], warnings: [] });
    });

    it('parsePlanSummary reads a JSON summary and rejects junk', () => {
      expect(parsePlanSummary(JSON.stringify({ title: 'X' }))?.title).toBe('X');
      expect(parsePlanSummary('pas du json')).toBeNull();
      expect(parsePlanSummary(null)).toBeNull();
      expect(parsePlanSummary('')).toBeNull();
    });

    it('parseActions keeps only absolute routes', () => {
      expect(parseActions(JSON.stringify([
        { label: 'Ouvrir', route: '/studio/systems/conges' },
        { label: 'Relatif', route: 'studio' },
        { label: 42, route: '/x' }
      ]))).toEqual([{ label: 'Ouvrir', route: '/studio/systems/conges' }]);
      expect(parseActions('pas du json')).toEqual([]);
      expect(parseActions(undefined)).toEqual([]);
    });
  });
});
