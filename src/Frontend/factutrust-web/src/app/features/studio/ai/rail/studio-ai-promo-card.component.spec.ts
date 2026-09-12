import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiPromoCardComponent } from './studio-ai-promo-card.component';

describe('StudioAiPromoCardComponent', () => {
  let fixture: ComponentFixture<StudioAiPromoCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiPromoCardComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();
    fixture = TestBed.createComponent(StudioAiPromoCardComponent);
    fixture.detectChanges();
  });

  it('shows the promo title and emits the example prompt on the CTA', () => {
    const prompts: string[] = [];
    fixture.componentInstance.tryPrompt.subscribe(p => prompts.push(p));
    expect(fixture.nativeElement.textContent).toContain(STUDIO_AI_LABELS.rail.promoTitle);

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();

    expect(prompts).toEqual([STUDIO_AI_LABELS.rail.promoPrompt]);
  });

  it('disables the CTA while busy', () => {
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('button') as HTMLButtonElement).disabled).toBeTrue();
  });
});
