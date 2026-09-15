import { ComponentFixture, TestBed } from '@angular/core/testing';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiWorkflowsTabComponent } from './studio-ai-workflows-tab.component';
import { studioAiSpecFixture } from './testing/studio-ai-spec.fixture';

describe('StudioAiWorkflowsTabComponent', () => {
  let fixture: ComponentFixture<StudioAiWorkflowsTabComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioAiWorkflowsTabComponent] }).compileComponents();
    fixture = TestBed.createComponent(StudioAiWorkflowsTabComponent);
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  it('Workflows affiche l’état vide quand la spec n’en contient pas', () => {
    fixture.componentRef.setInput('spec', studioAiSpecFixture());
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;

    const empty = host.querySelector('[data-component-id="sai-workflows-empty"]');
    expect(empty).not.toBeNull();
    expect(empty?.querySelector('.fa-route')).not.toBeNull();
    expect(text()).toContain(STUDIO_AI_LABELS.workflows.emptyTitle);
    expect(text()).toContain(STUDIO_AI_LABELS.workflows.emptyHint);
    expect(host.querySelector('.sai-list')).toBeNull();

    // Rendu générique dès qu'un workflow est présent (forme non contractuelle, lue de façon défensive).
    const spec = studioAiSpecFixture();
    spec.entities[1]['workflow'] = { name: 'Validation', steps: ['Soumis', 'Validé', 'Refusé'] };
    fixture.componentRef.setInput('spec', spec);
    fixture.detectChanges();
    expect(host.querySelector('[data-component-id="sai-workflows-empty"]')).toBeNull();
    expect(text()).toContain('Validation');
    expect(text()).toContain('Demande de congé');
    expect(text()).toContain(STUDIO_AI_LABELS.workflows.steps.replace('{count}', '3'));
  });
});
