import { ComponentFixture, TestBed } from '@angular/core/testing';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioSystemSpec } from '../studio-ai.models';
import { StudioAiViewsTabComponent } from './studio-ai-views-tab.component';
import { studioAiSpecFixture, studioAiSpecWithViewsFixture } from './testing/studio-ai-spec.fixture';

describe('StudioAiViewsTabComponent', () => {
  let fixture: ComponentFixture<StudioAiViewsTabComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioAiViewsTabComponent] }).compileComponents();
    fixture = TestBed.createComponent(StudioAiViewsTabComponent);
  });

  function load(spec: StudioSystemSpec): HTMLElement {
    fixture.componentRef.setInput('spec', spec);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function text(el: Element | null): string {
    return el?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  it('affiche une carte par vue avec le mode normalisé (alias français)', () => {
    const host = load(studioAiSpecWithViewsFixture());

    const cards = Array.from(host.querySelectorAll<HTMLElement>('.sai-view'));
    expect(cards.length).toBe(2);
    expect(cards.map(c => c.dataset['componentId'])).toEqual(['sai-view-kanban', 'sai-view-calendar']);

    expect(text(cards[0])).toContain('Par statut');
    expect(text(cards[0])).toContain(STUDIO_AI_LABELS.views.kanban);
    expect(text(cards[0])).toContain('Demandes de congés');
    expect(text(cards[0])).toContain(`${STUDIO_AI_LABELS.views.groupBy} : statut`);

    // `calendrier` (alias FR) ⇒ p-tag « Calendrier », puces Champ date / Fin résolues en libellés.
    expect(text(cards[1])).toContain(STUDIO_AI_LABELS.views.calendar);
    expect(text(cards[1])).toContain(`${STUDIO_AI_LABELS.views.dateField} : Date de début`);
    expect(text(cards[1])).toContain(`${STUDIO_AI_LABELS.views.endField} : Nombre de jours`);

    const thumbs = Array.from(host.querySelectorAll('[role="img"]'));
    expect(thumbs.length).toBe(2);
    expect(thumbs.every(t => (t.getAttribute('aria-label') ?? '').length > 0)).toBeTrue();
    expect(text(host)).toContain(STUDIO_AI_LABELS.views.count.replace('{count}', '2'));
    expect(host.querySelector('.sai-empty')).toBeNull();
  });

  it('affiche l’état vide sans vue', () => {
    const host = load(studioAiSpecFixture());

    expect(host.querySelectorAll('.sai-view').length).toBe(0);
    expect(text(host.querySelector('.sai-empty'))).toContain(STUDIO_AI_LABELS.views.empty);
    expect(text(host)).toContain(STUDIO_AI_LABELS.views.count.replace('{count}', '0'));
  });

  it('la miniature kanban regroupe le seed par option', () => {
    const spec = studioAiSpecFixture();
    const employes = spec.entities.find(e => e.ref === 'employes')!;
    employes.views = [{ name: 'Par statut', mode: 'kanban', groupBy: 'statut', isDefault: true }];
    spec.seed = [
      {
        entityRef: 'employes',
        records: [
          { matricule: 'E-001', nom: 'Dupont', statut: 'actif' },
          { matricule: 'E-002', nom: 'Martin', statut: 'Actif' },
          { matricule: 'E-003', nom: 'Durand', statut: 'actif' },
          { matricule: 'E-004', nom: 'Petit', statut: 'actif' },
          { matricule: 'E-005', nom: 'Bernard', statut: 'inactif' }
        ]
      }
    ];
    const host = load(spec);

    const columns = Array.from(host.querySelectorAll<HTMLElement>('.th-col'));
    expect(columns.map(c => c.dataset['option'])).toEqual(['actif', 'inactif']);
    expect(columns[0].querySelectorAll('.th-card').length).withContext('4 actifs plafonnés à 3 cartes').toBe(3);
    expect(columns[1].querySelectorAll('.th-card').length).toBe(1);

    const thumb = host.querySelector('[role="img"]');
    expect(thumb?.getAttribute('aria-label')).toContain(STUDIO_AI_LABELS.views.kanban);
    expect(thumb?.getAttribute('aria-label')).toContain('Par statut');
    expect(host.querySelector('[innerHTML]')).toBeNull();
    expect(text(host)).toContain(STUDIO_AI_LABELS.views.isDefault);
  });
});
