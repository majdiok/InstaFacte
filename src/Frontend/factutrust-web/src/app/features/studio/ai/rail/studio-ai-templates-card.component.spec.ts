import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioTemplateListItemDto } from '../studio-ai.models';
import { RAIL_TEMPLATES_MAX, StudioAiTemplatesCardComponent } from './studio-ai-templates-card.component';

function template(key: string, over: Partial<StudioTemplateListItemDto> = {}): StudioTemplateListItemDto {
  return {
    key, displayName: `Modèle ${key}`, description: 'Description', category: 'Commercial', moduleTag: 'crm',
    source: 'builtin', entityCount: 4, ...over
  };
}

describe('StudioAiTemplatesCardComponent', () => {
  let fixture: ComponentFixture<StudioAiTemplatesCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiTemplatesCardComponent],
      providers: [provideRouter([]), provideNoopAnimations()]
    }).compileComponents();
    fixture = TestBed.createComponent(StudioAiTemplatesCardComponent);
  });

  function rows(): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[data-template-key]'));
  }

  it('shows at most three templates, their entity count and a « Voir tous » link to the library', () => {
    fixture.componentRef.setInput('templates', [template('crm'), template('paie'), template('stock'), template('rh')]);
    fixture.detectChanges();

    expect(rows().length).toBe(RAIL_TEMPLATES_MAX);
    expect(rows().map(r => r.getAttribute('data-template-key'))).toEqual(['crm', 'paie', 'stock']);
    expect(rows()[0].textContent).toContain('Modèle crm');
    expect(rows()[0].textContent).toContain('4 table(s)');
    const link = fixture.nativeElement.querySelector('a.sar-card__link') as HTMLAnchorElement;
    expect(link.textContent).toContain(STUDIO_AI_LABELS.rail.seeAll);
    expect(link.getAttribute('href')).toBe('/studio/ai/templates');
  });

  it('emits « use » with the template key', () => {
    fixture.componentRef.setInput('templates', [template('crm')]);
    fixture.detectChanges();
    const used: string[] = [];
    fixture.componentInstance.use.subscribe(key => used.push(key));

    (rows()[0].querySelector('button') as HTMLButtonElement).click();

    expect(used).toEqual(['crm']);
  });

  it('renders skeletons while loading, the empty text and the error text', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('p-skeleton').length).toBeGreaterThan(0);

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(STUDIO_AI_LABELS.rail.templatesEmpty);

    fixture.componentRef.setInput('error', STUDIO_AI_LABELS.rail.templatesLoadFailed);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sar-error')?.textContent).toContain(STUDIO_AI_LABELS.rail.templatesLoadFailed);
    expect(rows().length).toBe(0);
  });
});
