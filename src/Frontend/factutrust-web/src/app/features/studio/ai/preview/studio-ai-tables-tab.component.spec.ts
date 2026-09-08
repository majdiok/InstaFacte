import { ComponentFixture, TestBed } from '@angular/core/testing';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiTablesTabComponent } from './studio-ai-tables-tab.component';
import { studioAiSpecFixture } from './testing/studio-ai-spec.fixture';

describe('StudioAiTablesTabComponent', () => {
  let fixture: ComponentFixture<StudioAiTablesTabComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StudioAiTablesTabComponent] }).compileComponents();

    fixture = TestBed.createComponent(StudioAiTablesTabComponent);
    fixture.componentRef.setInput('spec', studioAiSpecFixture());
    fixture.detectChanges();
  });

  function rows(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')).map(
      tr => tr.textContent?.replace(/\s+/g, ' ').trim() ?? ''
    );
  }

  it('selects the first entity by default and renders its fields with French type labels', () => {
    expect(fixture.componentInstance.activeRef()).toBe('employes');

    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th')).map(
      th => th.textContent?.trim()
    );
    expect(headers).toEqual([
      STUDIO_AI_LABELS.preview.colLabel,
      STUDIO_AI_LABELS.preview.colKey,
      STUDIO_AI_LABELS.preview.colType,
      STUDIO_AI_LABELS.preview.colRequired,
      STUDIO_AI_LABELS.preview.colUnique,
      STUDIO_AI_LABELS.preview.colDetails
    ]);

    expect(rows().length).toBe(3);
    expect(rows()[0]).toContain('Matricule');
    expect(rows()[0]).toContain(STUDIO_AI_LABELS.fieldTypes.text);
    expect(rows()[2]).toContain('Actif · Inactif');
  });

  it('expands the entity requested through selectedRef', () => {
    fixture.componentRef.setInput('selectedRef', 'demandes');
    fixture.detectChanges();

    expect(fixture.componentInstance.activeRef()).toBe('demandes');
    expect(rows().length).toBe(5);
  });

  it('describes relation targets, flagging ERP targets', () => {
    fixture.componentRef.setInput('selectedRef', 'demandes');
    fixture.detectChanges();

    const spec = studioAiSpecFixture();
    const entityRelation = spec.entities[1].fields[0];
    const erpRelation = spec.entities[1].fields[1];
    expect(fixture.componentInstance.details(entityRelation)).toBe('→ Employé');
    expect(fixture.componentInstance.details(erpRelation)).toBe(`→ Clients (${STUDIO_AI_LABELS.preview.erpBadge})`);
    expect(fixture.componentInstance.details(spec.entities[1].fields[4])).toBe('EUR');
  });

  it('lets the user pick another entity from the left list', () => {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button.sai-list__item'));
    expect(buttons.length).toBe(2);

    (buttons[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.activeRef()).toBe('demandes');
    expect(rows().length).toBe(5);
  });
});
