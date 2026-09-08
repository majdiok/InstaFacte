import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { STUDIO_AI_CAPABILITIES_FALLBACK, StudioAiCapabilitiesDto } from '../studio-ai.models';
import { StudioAiIntentCardDef } from '../studio-ai-labels';
import { StudioAiIntentCardsComponent } from './studio-ai-intent-cards.component';

const ALL_ENABLED: StudioAiCapabilitiesDto = {
  ...STUDIO_AI_CAPABILITIES_FALLBACK,
  planPreviewEnabled: true,
  systemGenerationEnabled: true,
  modifyToolsEnabled: true,
  viewToolsEnabled: true,
  reportToolsEnabled: true,
  workbenchEnabled: true
};

describe('StudioAiIntentCardsComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiIntentCardsComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();
  });

  function create(capabilities: StudioAiCapabilitiesDto = ALL_ENABLED) {
    const fixture = TestBed.createComponent(StudioAiIntentCardsComponent);
    fixture.componentRef.setInput('capabilities', capabilities);
    fixture.detectChanges();
    return fixture;
  }

  function card(fixture: ReturnType<typeof create>, intent: string): HTMLButtonElement {
    return fixture.nativeElement.querySelector(`button[data-intent="${intent}"]`) as HTMLButtonElement;
  }

  it('rend les 8 cartes d’intention', () => {
    const fixture = create();
    expect(fixture.nativeElement.querySelectorAll('button[data-intent]').length).toBe(8);
    expect(card(fixture, 'system').textContent).toContain('Système complet');
  });

  it('désactive Workflow et Page avec un badge « Bientôt »', () => {
    const fixture = create();
    for (const intent of ['workflow', 'page']) {
      expect(card(fixture, intent).disabled).withContext(intent).toBeTrue();
      expect(card(fixture, intent).textContent).toContain('Bientôt');
    }
    expect(card(fixture, 'table').disabled).toBeFalse();
  });

  it('désactive les cartes dont la capacité serveur est coupée', () => {
    const fixture = create({ ...ALL_ENABLED, modifyToolsEnabled: false, systemGenerationEnabled: false });
    expect(card(fixture, 'reference_data').disabled).toBeTrue();
    expect(card(fixture, 'system').disabled).toBeTrue();
    expect(card(fixture, 'report').disabled).toBeFalse();
  });

  it('émet « pick » avec la définition complète de la carte cliquée', () => {
    const fixture = create();
    const picked: StudioAiIntentCardDef[] = [];
    fixture.componentInstance.pick.subscribe(def => picked.push(def));

    card(fixture, 'table').click();

    expect(picked.length).toBe(1);
    expect(picked[0].intent).toBe('table');
    expect(picked[0].prompt).toContain('Créer une table');
  });
});
