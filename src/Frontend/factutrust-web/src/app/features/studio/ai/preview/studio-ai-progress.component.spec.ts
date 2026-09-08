import { TestBed } from '@angular/core/testing';
import { StudioBuildStep } from '../studio-ai.models';
import { StudioAiProgressComponent } from './studio-ai-progress.component';

const STEPS: StudioBuildStep[] = [
  { phase: 'creating_system', label: 'Système', status: 'done', detail: 'Créer le système' },
  { phase: 'creating_entity', label: 'Table Employés', status: 'running' },
  { phase: 'seeding_data', label: 'Données de référence', status: 'pending' }
];

describe('StudioAiProgressComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioAiProgressComponent] }).compileComponents();
  });

  function create(steps: StudioBuildStep[] = STEPS) {
    const fixture = TestBed.createComponent(StudioAiProgressComponent);
    fixture.componentRef.setInput('steps', steps);
    fixture.detectChanges();
    return fixture;
  }

  it('rend une ligne par étape avec l’icône de son statut', () => {
    const fixture = create();
    const items = fixture.nativeElement.querySelectorAll('.saip__step');
    expect(items.length).toBe(3);
    expect(items[0].querySelector('.fa-circle-check')).toBeTruthy();
    expect(items[1].querySelector('.fa-spinner')).toBeTruthy();
    expect(items[2].querySelector('.fa-circle')).toBeTruthy();
  });

  it('affiche l’avancement, l’étape en cours et le rappel de ne pas fermer la page', () => {
    const fixture = create();
    const text = fixture.nativeElement.textContent as string;
    expect(fixture.nativeElement.querySelector('.saip__count').textContent).toContain('1 / 3');
    expect(text).toContain('Étape 2 sur 3');
    expect(text).toContain('Table Employés');
    expect(text).toContain('Ne fermez pas cette page.');
  });

  it('marque l’étape en échec', () => {
    const fixture = create([{ phase: 'creating_fields', label: 'Champs', status: 'error', detail: 'Champ invalide' }]);
    expect(fixture.nativeElement.querySelector('.saip__step--error')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.fa-circle-xmark')).toBeTruthy();
  });
});
