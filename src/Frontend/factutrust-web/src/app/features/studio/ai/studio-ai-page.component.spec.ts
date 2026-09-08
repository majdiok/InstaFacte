import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { AiChatService } from '@features/ai-assistant/services/ai-chat.service';
import { StudioAiBuildService } from '../studio-ai-build.service';
import { StudioNavService } from '../studio-nav.service';
import { StudioAiPageComponent } from './studio-ai-page.component';
import { STUDIO_AI_LABELS } from './studio-ai-labels';
import { studioAiSpecFixture } from './preview/testing/studio-ai-spec.fixture';

describe('StudioAiPageComponent', () => {
  let fixture: ComponentFixture<StudioAiPageComponent>;
  let stream: jasmine.SpyObj<AiStreamService>;

  beforeEach(async () => {
    stream = jasmine.createSpyObj<AiStreamService>('AiStreamService', ['streamChat']);
    stream.streamChat.and.returnValue(of());
    const builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', [
      'getPlanSpec', 'confirm', 'cancel', 'cancelPending', 'getCapabilities'
    ]);
    builds.getPlanSpec.and.returnValue(of());
    builds.cancel.and.returnValue(of());
    builds.cancelPending.and.returnValue(of());
    builds.getCapabilities.and.returnValue(of());
    const nav = jasmine.createSpyObj<StudioNavService>('StudioNavService', ['refresh']);
    const chat = jasmine.createSpyObj<AiChatService>('AiChatService', ['extractDocument']);

    await TestBed.configureTestingModule({
      imports: [StudioAiPageComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AiStreamService, useValue: stream },
        { provide: StudioAiBuildService, useValue: builds },
        { provide: StudioNavService, useValue: nav },
        { provide: AiChatService, useValue: chat }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StudioAiPageComponent);
    fixture.detectChanges();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('affiche le composer et les cartes d’intention au repos, sans rail de conversation', () => {
    expect(fixture.componentInstance.mainView()).toBe('compose');
    expect(fixture.nativeElement.querySelector('app-studio-ai-composer')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-studio-ai-intent-cards')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-studio-ai-conversation')).toBeNull();
    expect(text()).toContain(STUDIO_AI_LABELS.page.heroTitle);
  });

  it('préremplit le composer et présélectionne l’onglet quand une carte est choisie', () => {
    const card = STUDIO_AI_LABELS.intents.find(c => c.intent === 'report')!;
    fixture.componentInstance.pickIntent(card);
    expect(fixture.componentInstance.prefill()).toBe(card.prompt);
    expect(fixture.componentInstance.pendingIntent()).toBe('report');
    expect(fixture.componentInstance.previewTab()).toBe('reports');
  });

  it('ignore les cartes « Bientôt »', () => {
    const card = STUDIO_AI_LABELS.intents.find(c => !c.available)!;
    fixture.componentInstance.pickIntent(card);
    expect(fixture.componentInstance.prefill()).toBeNull();
  });

  it('envoie la demande au store avec l’intention courante et affiche le rail', () => {
    const card = STUDIO_AI_LABELS.intents.find(c => c.intent === 'table')!;
    fixture.componentInstance.pickIntent(card);
    fixture.componentInstance.submit({ text: 'Créer une table contrats', attachments: [] });
    fixture.detectChanges();

    expect(stream.streamChat).toHaveBeenCalledTimes(1);
    const request = stream.streamChat.calls.mostRecent().args[0];
    expect(request.message).toBe('Créer une table contrats');
    expect(request.attachments).toBeUndefined();
    expect(fixture.componentInstance.store.intent()).toBe('table');
    expect(fixture.componentInstance.showConversation()).toBeTrue();
    expect(fixture.nativeElement.querySelector('app-studio-ai-conversation')).not.toBeNull();
  });

  it('bascule sur l’aperçu quand un plan est présent, puis sur la progression et le résultat', () => {
    const store = fixture.componentInstance.store;
    store.plan.set({
      planId: 'p1', kind: 'CreateSystem', expiresAt: null, rowVersion: 'v1',
      summary: { kind: 'CreateSystem', title: 'Congés', steps: [], entities: [], warnings: [] }
    });
    const spec = studioAiSpecFixture();
    store.spec.set(spec);
    store.draft.set(structuredClone(spec));
    store.phase.set('awaiting_confirmation');
    fixture.detectChanges();
    expect(fixture.componentInstance.mainView()).toBe('preview');
    expect(fixture.nativeElement.querySelector('app-studio-ai-preview')).not.toBeNull();

    store.phase.set('executing');
    fixture.detectChanges();
    expect(fixture.componentInstance.mainView()).toBe('progress');

    store.result.set({
      success: true, systemKey: 'conges', systemUrl: '/studio/systems/conges', displayName: 'Congés',
      entityCount: 2, entities: [], warnings: [], message: ''
    });
    store.phase.set('completed');
    fixture.detectChanges();
    expect(fixture.componentInstance.mainView()).toBe('result');
    expect(fixture.nativeElement.querySelector('app-studio-ai-result-card')).not.toBeNull();
  });

  it('n’ouvre le dialogue de confirmation que si le plan est confirmable', () => {
    fixture.componentInstance.openConfirm();
    expect(fixture.componentInstance.confirmVisible()).toBeFalse();

    const store = fixture.componentInstance.store;
    store.plan.set({
      planId: 'p1', kind: 'CreateSystem', expiresAt: null, rowVersion: 'v1',
      summary: { kind: 'CreateSystem', title: 'Congés', steps: [], entities: [], warnings: [] }
    });
    store.phase.set('awaiting_confirmation');
    fixture.componentInstance.openConfirm();
    expect(fixture.componentInstance.confirmVisible()).toBeTrue();
  });
});
