import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { StudioAiComposerComponent, StudioAiComposerSubmit } from './studio-ai-composer.component';

describe('StudioAiComposerComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiComposerComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(StudioAiComposerComponent);
    fixture.detectChanges();
    return fixture;
  }

  function textarea(fixture: ReturnType<typeof create>): HTMLTextAreaElement {
    return fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
  }

  function type(fixture: ReturnType<typeof create>, value: string): void {
    const el = textarea(fixture);
    el.value = value;
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('rend la zone de saisie et désactive l’envoi tant qu’elle est vide', () => {
    const fixture = create();
    const send = fixture.nativeElement.querySelector('.sac__send') as HTMLButtonElement;
    expect(textarea(fixture).placeholder).toContain('Décrivez');
    expect(send.disabled).toBeTrue();
  });

  it('émet « submitted » sur Entrée et vide la zone (Maj+Entrée ne fait rien)', async () => {
    const fixture = create();
    const submits: StudioAiComposerSubmit[] = [];
    fixture.componentInstance.submitted.subscribe(payload => submits.push(payload));

    type(fixture, '  Créer un système de congés  ');
    textarea(fixture).dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true }));
    expect(submits.length).toBe(0);

    textarea(fixture).dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    // `ngModel` réécrit la vue dans une micro-tâche : sans attente, la zone semble encore remplie.
    await fixture.whenStable();

    expect(submits.length).toBe(1);
    expect(submits[0].text).toBe('Créer un système de congés');
    expect(submits[0].attachments).toEqual([]);
    expect(textarea(fixture).value).toBe('');
  });

  it('prérempli et focalise la zone quand une carte d’intention est choisie', async () => {
    const fixture = create();
    fixture.componentRef.setInput('prefill', 'Créer une table « … »');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(textarea(fixture).value).toBe('Créer une table « … »');
    expect(document.activeElement).toBe(textarea(fixture));
  });

  it('extrait le document côté serveur puis émet la pièce jointe', () => {
    const fixture = create();
    const submits: StudioAiComposerSubmit[] = [];
    fixture.componentInstance.submitted.subscribe(payload => submits.push(payload));

    const file = new File(['clé;valeur'], 'valeurs.csv', { type: 'text/csv' });
    fixture.componentInstance['extract'](file);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Extraction de valeurs.csv');

    const req = http.expectOne(r => r.url.endsWith('/document-extract'));
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: { fileName: 'valeurs.csv', format: 'csv', text: 'clé;valeur', pages: [], warnings: [] } });
    fixture.detectChanges();

    fixture.componentInstance.submit();
    expect(submits.length).toBe(1);
    expect(submits[0].attachments.length).toBe(1);
    expect(submits[0].attachments[0].fileName).toBe('valeurs.csv');
  });

  it('refuse la 6e pièce jointe avec un message en ligne', () => {
    const fixture = create();
    for (let i = 0; i < 5; i++) {
      fixture.componentInstance['extract'](new File(['x'], `f${i}.txt`, { type: 'text/plain' }));
    }
    http.match(r => r.url.endsWith('/document-extract')).forEach(req =>
      req.flush({ success: true, data: { fileName: 'f.txt', format: 'txt', text: 'x', pages: [], warnings: [] } })
    );

    fixture.componentInstance['extract'](new File(['x'], 'trop.txt', { type: 'text/plain' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('5 pièces jointes');
  });

  it('masque le toggle « Modèle avancé » quand l’administrateur ne l’a pas configuré', () => {
    const fixture = create();
    expect(fixture.nativeElement.querySelector('.sac__model')).toBeNull();
  });

  it('affiche le toggle avec le libellé du modèle, relié à son <label>, et émet le changement', () => {
    const fixture = TestBed.createComponent(StudioAiComposerComponent);
    fixture.componentRef.setInput('advancedModelAvailable', true);
    fixture.componentRef.setInput('advancedModelLabel', 'GPT-4.1');
    fixture.componentRef.setInput('useAdvancedModel', false);
    fixture.detectChanges();

    const changes: boolean[] = [];
    fixture.componentInstance.advancedModelChange.subscribe(v => changes.push(v));

    const model = fixture.nativeElement.querySelector('.sac__model') as HTMLElement;
    expect(model.textContent).toContain('Modèle avancé');
    expect(model.textContent).toContain('GPT-4.1');
    const input = model.querySelector('input[type="checkbox"]') as HTMLInputElement;
    const label = model.querySelector('label') as HTMLLabelElement;
    expect(input.id).toBeTruthy();
    expect(label.htmlFor).toBe(input.id);
    expect(input.checked).toBeFalse();

    input.click();
    fixture.detectChanges();
    expect(changes).toEqual([true]);
  });

  it('rend le micro désactivé avec l’indication « Bientôt »', () => {
    const fixture = create();
    const mic = fixture.nativeElement.querySelector('.sac__mic-button') as HTMLButtonElement;
    expect(mic.disabled).toBeTrue();
    expect(mic.getAttribute('aria-label')).toContain('Dicter la demande');
    expect(mic.getAttribute('aria-label')).toContain('prochaine version');
  });
});
