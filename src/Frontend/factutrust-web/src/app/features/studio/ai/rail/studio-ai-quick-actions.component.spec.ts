import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiQuickActionsComponent } from './studio-ai-quick-actions.component';

describe('StudioAiQuickActionsComponent', () => {
  let fixture: ComponentFixture<StudioAiQuickActionsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiQuickActionsComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();
    fixture = TestBed.createComponent(StudioAiQuickActionsComponent);
  });

  function action(name: string): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector(`button[data-action="${name}"]`);
  }

  it('without systemExportEnabled: hides Import / Dupliquer, disables Exporter and Partager with « Bientôt », keeps Réinitialiser', () => {
    fixture.detectChanges();

    expect(action('import')).toBeNull();
    expect(action('duplicate')).toBeNull();
    expect(action('export')!.disabled).toBeTrue();
    expect(action('export')!.textContent).toContain(STUDIO_AI_LABELS.soon);
    expect(action('share')!.disabled).toBeTrue();
    expect(action('share')!.textContent).toContain(STUDIO_AI_LABELS.soon);
    expect(action('reset')!.disabled).toBeFalse();
    expect(action('reset')!.textContent).toContain(STUDIO_AI_LABELS.rail.resetConversation);
  });

  it('with systemExportEnabled: shows Import / Dupliquer and enables Exporter (Partager stays « Bientôt »)', () => {
    fixture.componentRef.setInput('exportEnabled', true);
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.importTemplate.subscribe(() => emitted.push('import'));
    fixture.componentInstance.duplicate.subscribe(() => emitted.push('duplicate'));
    fixture.componentInstance.exportSystem.subscribe(() => emitted.push('export'));

    expect(action('export')!.disabled).toBeFalse();
    expect(action('export')!.textContent).not.toContain(STUDIO_AI_LABELS.soon);
    expect(action('share')!.disabled).toBeTrue();
    action('import')!.click();
    action('duplicate')!.click();
    action('export')!.click();

    expect(emitted).toEqual(['import', 'duplicate', 'export']);
  });

  it('emits « reset » and disables every action while busy', () => {
    fixture.detectChanges();
    let resets = 0;
    fixture.componentInstance.reset.subscribe(() => resets++);

    action('reset')!.click();
    expect(resets).toBe(1);

    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(action('reset')!.disabled).toBeTrue();
  });
});
