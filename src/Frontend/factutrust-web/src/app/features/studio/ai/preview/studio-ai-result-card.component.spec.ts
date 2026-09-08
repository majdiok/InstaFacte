import { TestBed } from '@angular/core/testing';
import { StudioAiNavAction } from '../studio-ai-session.store';
import { StudioAppBuildResult, StudioSystemBuildResult } from '../studio-ai.models';
import { StudioAiResultCardComponent } from './studio-ai-result-card.component';

const SYSTEM_RESULT: StudioSystemBuildResult = {
  success: true,
  systemKey: 'gestion-conges',
  systemUrl: '/studio/systems/gestion-conges',
  displayName: 'Gestion des congés',
  entityCount: 2,
  entities: [
    { refKey: 'employes', entityKey: 'employes', displayName: 'Employés', openUrl: '/studio/d/employes' },
    { refKey: 'demandes', entityKey: 'demandes', displayName: 'Demandes', openUrl: '/studio/d/demandes' }
  ],
  warnings: ['Vérifiez la relation « service_id ».'],
  buildSteps: [
    { phase: 'creating_system', label: 'Système', status: 'done' },
    { phase: 'creating_entity', label: 'Employés', status: 'done' }
  ],
  message: 'Système créé.'
};

const APP_RESULT: StudioAppBuildResult = {
  entityKey: 'conges',
  displayName: 'Congés',
  fieldsCreated: 6,
  reportCreated: false,
  warnings: [],
  openUrl: '/studio/d/conges',
  message: 'Table créée.'
};

describe('StudioAiResultCardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioAiResultCardComponent] }).compileComponents();
  });

  function create(result: StudioSystemBuildResult | StudioAppBuildResult, actions: StudioAiNavAction[] = []) {
    const fixture = TestBed.createComponent(StudioAiResultCardComponent);
    fixture.componentRef.setInput('result', result);
    fixture.componentRef.setInput('actions', actions);
    fixture.detectChanges();
    return fixture;
  }

  it('rend le titre, les compteurs, les entités et les avertissements d’un système', () => {
    const fixture = create(SYSTEM_RESULT);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Système « Gestion des congés » créé');
    expect(text).toContain('2 étapes terminées');
    expect(text).toContain('Employés');
    expect(text).toContain('Vérifiez la relation');
    expect(fixture.nativeElement.querySelectorAll('.sair__chip').length).toBe(2);
  });

  it('émet l’URL du système sur « Ouvrir le système » et celle d’une entité sur « Ouvrir »', () => {
    const fixture = create(SYSTEM_RESULT);
    const urls: string[] = [];
    fixture.componentInstance.open.subscribe(url => urls.push(url));

    (fixture.nativeElement.querySelector('.sair__open') as HTMLButtonElement).click();
    (fixture.nativeElement.querySelector('.sair__chip-link') as HTMLButtonElement).click();

    expect(urls).toEqual(['/studio/systems/gestion-conges', '/studio/d/employes']);
  });

  it('émet « newRequest » et « navigate »', () => {
    const action: StudioAiNavAction = { label: 'Voir les rapports', route: '/studio/reports' };
    const fixture = create(SYSTEM_RESULT, [action]);
    let newRequests = 0;
    const navigations: StudioAiNavAction[] = [];
    fixture.componentInstance.newRequest.subscribe(() => newRequests++);
    fixture.componentInstance.navigate.subscribe(a => navigations.push(a));

    (fixture.nativeElement.querySelector('.sair__new') as HTMLButtonElement).click();
    const actionButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(b => (b.textContent ?? '').includes('Voir les rapports'));
    actionButton?.click();

    expect(newRequests).toBe(1);
    expect(navigations).toEqual([action]);
  });

  it('bascule sur le rendu « table » pour un plan CreateApp', () => {
    const fixture = create(APP_RESULT);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Table « Congés » créée');
    expect(text).toContain('Ouvrir la table');
    expect(fixture.nativeElement.querySelectorAll('.sair__chip').length).toBe(0);
  });
});
