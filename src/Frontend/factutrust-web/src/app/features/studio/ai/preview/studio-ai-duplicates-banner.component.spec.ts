import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { StudioAiDuplicatesBannerComponent } from './studio-ai-duplicates-banner.component';
import { StudioDuplicateHint } from '../studio-ai.models';

describe('StudioAiDuplicatesBannerComponent', () => {
  const hints: StudioDuplicateHint[] = [
    { specRef: 'clients', specDisplayName: 'Clients', existingKey: 'clients', existingDisplayName: 'Clients', reason: 'same_key' },
    { specRef: 'employes', specDisplayName: 'Employé', existingKey: 'employes', existingDisplayName: 'Employés', reason: 'singular_plural' }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiDuplicatesBannerComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();
  });

  function create(input: StudioDuplicateHint[], actionsEnabled = true) {
    const fixture = TestBed.createComponent(StudioAiDuplicatesBannerComponent);
    fixture.componentRef.setInput('hints', input);
    fixture.componentRef.setInput('actionsEnabled', actionsEnabled);
    fixture.detectChanges();
    return fixture;
  }

  it('reste masqué sans doublon', () => {
    const fixture = create([]);
    expect(fixture.nativeElement.querySelector('.sai-dup')).toBeNull();
  });

  it('rend un bloc par indice avec la clé existante, la raison et les deux décisions', () => {
    const fixture = create(hints);
    const root = fixture.nativeElement.querySelector('.sai-dup') as HTMLElement;
    expect(root.getAttribute('role')).toBe('status');
    const items = root.querySelectorAll('.sai-dup__item');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('La table « Clients » existe déjà');
    expect(items[0].textContent).toContain('(clé clients)');
    expect(items[0].textContent).toContain('même clé');
    expect(items[0].textContent).toContain('« Clients (2) »');
    expect(items[1].textContent).toContain('singulier / pluriel');
    expect(root.querySelectorAll('button').length).toBe(4);
  });

  it('émet la décision de l’utilisateur pour le bon indice', () => {
    const fixture = create(hints);
    const reused: StudioDuplicateHint[] = [];
    const created: StudioDuplicateHint[] = [];
    fixture.componentInstance.reuse.subscribe(h => reused.push(h));
    fixture.componentInstance.createAnyway.subscribe(h => created.push(h));

    const second = fixture.nativeElement.querySelectorAll('.sai-dup__item')[1] as HTMLElement;
    const buttons = second.querySelectorAll('button');
    (buttons[0] as HTMLButtonElement).click();
    (buttons[1] as HTMLButtonElement).click();

    expect(reused.map(h => h.specRef)).toEqual(['employes']);
    expect(created.map(h => h.specRef)).toEqual(['employes']);
  });

  it('masque les boutons quand les décisions ne sont pas disponibles', () => {
    const fixture = create(hints, false);
    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(0);
  });
});
