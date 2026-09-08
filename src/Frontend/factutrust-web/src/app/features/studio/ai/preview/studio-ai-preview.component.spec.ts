import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AiStreamService } from '@features/ai-assistant/services/ai-stream.service';
import { StudioAiBuildService } from '../../studio-ai-build.service';
import { StudioNavService } from '../../studio-nav.service';
import { StudioAiSessionStore } from '../studio-ai-session.store';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiPreviewComponent } from './studio-ai-preview.component';
import { studioAiSpecFixture } from './testing/studio-ai-spec.fixture';

describe('StudioAiPreviewComponent', () => {
  let fixture: ComponentFixture<StudioAiPreviewComponent>;
  let store: StudioAiSessionStore;

  beforeEach(async () => {
    const stream = jasmine.createSpyObj<AiStreamService>('AiStreamService', ['streamChat']);
    stream.streamChat.and.returnValue(of());
    const builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', [
      'getPlanSpec', 'confirm', 'cancel', 'getCapabilities'
    ]);
    builds.getPlanSpec.and.returnValue(of());
    builds.cancel.and.returnValue(of());
    builds.getCapabilities.and.returnValue(of());
    const nav = jasmine.createSpyObj<StudioNavService>('StudioNavService', ['refresh']);

    await TestBed.configureTestingModule({
      imports: [StudioAiPreviewComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        StudioAiSessionStore,
        { provide: AiStreamService, useValue: stream },
        { provide: StudioAiBuildService, useValue: builds },
        { provide: StudioNavService, useValue: nav }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StudioAiPreviewComponent);
    store = fixture.componentInstance.store;
    fixture.detectChanges();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  /** Amène le store dans l'état « proposition reçue, en attente de validation ». */
  function loadPlan(): void {
    const spec = studioAiSpecFixture();
    store.plan.set({
      planId: 'plan-1',
      kind: 'CreateSystem',
      expiresAt: null,
      rowVersion: 'v1',
      summary: {
        kind: 'CreateSystem',
        title: 'Gestion des congés',
        steps: [],
        entities: [],
        warnings: ['Le champ « Indemnité » n’a pas de devise explicite.']
      }
    });
    store.spec.set(spec);
    store.draft.set(spec);
    store.phase.set('awaiting_confirmation');
    fixture.detectChanges();
  }

  it('shows the empty state while no plan has been received', () => {
    expect(text()).toContain(STUDIO_AI_LABELS.preview.emptyTitle);
    expect(text()).toContain(STUDIO_AI_LABELS.preview.emptyHint);
    expect((fixture.nativeElement as HTMLElement).querySelector('p-tabs')).toBeNull();
  });

  it('shows the capability banner when the preview is disabled by the administrator', () => {
    expect(text()).toContain(STUDIO_AI_LABELS.capabilities.previewDisabled);
  });

  it('renders the header, the counters and the plan warnings once a plan is loaded', () => {
    loadPlan();

    expect(text()).toContain('Gestion des congés');
    const chips = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.sai-chip')
    ).map(el => el.textContent?.replace(/\s+/g, ' ').trim());
    expect(chips).toContain(`2 ${STUDIO_AI_LABELS.preview.tables}`);
    expect(chips).toContain(`8 ${STUDIO_AI_LABELS.preview.fields}`);
    expect(chips).toContain(`2 ${STUDIO_AI_LABELS.preview.relations}`);
    expect(text()).toContain('devise explicite');
  });

  it('renders the nine tabs with Workflow and Pages disabled and flagged « Bientôt »', () => {
    loadPlan();

    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('p-tab'));
    expect(tabs.length).toBe(9);

    const disabled = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('p-tab.p-disabled')
    ).map(tab => tab.textContent?.replace(/\s+/g, ' ').trim() ?? '');
    expect(disabled.length).toBe(2);
    expect(disabled.some(l => l.includes('Workflow'))).toBeTrue();
    expect(disabled.some(l => l.includes('Pages'))).toBeTrue();
    expect(disabled.every(l => l.includes(STUDIO_AI_LABELS.soon))).toBeTrue();
  });

  it('emits confirmRequested instead of creating anything, and keeps « Modifier » disabled', () => {
    loadPlan();
    const emitted: number[] = [];
    fixture.componentInstance.confirmRequested.subscribe(() => emitted.push(1));

    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    const create = buttons.find(b => b.textContent?.includes(STUDIO_AI_LABELS.preview.createNow));
    const edit = buttons.find(b => b.textContent?.includes(STUDIO_AI_LABELS.preview.edit));
    expect(edit?.disabled).toBeTrue();
    expect(create?.disabled).withContext('canConfirm est vrai en attente de validation').toBeFalse();

    create?.click();
    expect(emitted.length).toBe(1);
  });

  it('switches to the Tables tab when the overview asks to open an entity', () => {
    loadPlan();
    const opened: string[] = [];
    fixture.componentInstance.openEntity.subscribe(ref => opened.push(ref));

    fixture.componentInstance.selectEntity('demandes');
    fixture.detectChanges();

    expect(fixture.componentInstance.activeTab()).toBe('tables');
    expect(fixture.componentInstance.selectedRef()).toBe('demandes');
    expect(opened).toEqual(['demandes']);
  });

  it('shows a skeleton while the detailed spec is still loading', () => {
    store.plan.set({
      planId: 'plan-1',
      kind: 'CreateSystem',
      expiresAt: null,
      rowVersion: null,
      summary: { kind: 'CreateSystem', title: 'Gestion des congés', steps: [], entities: [], warnings: [] }
    });
    store.specLoading.set(true);
    fixture.detectChanges();

    expect(text()).toContain(STUDIO_AI_LABELS.preview.loadingSpec);
    expect((fixture.nativeElement as HTMLElement).querySelector('p-skeleton')).not.toBeNull();
  });
});
