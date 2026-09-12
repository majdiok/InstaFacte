import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiPlanListItemDto } from '../studio-ai.models';
import { StudioAiHistoryCardComponent } from './studio-ai-history-card.component';
import { planStatusSeverity, relativeTime } from './studio-ai-rail.util';

function plan(id: string, status: StudioAiPlanListItemDto['status'], over: Partial<StudioAiPlanListItemDto> = {}): StudioAiPlanListItemDto {
  return {
    id, kind: 'CreateSystem', status, title: `Plan ${id}`, entityCount: 2,
    createdAt: '2026-09-12T08:00:00Z', expiresAt: '2026-09-13T08:00:00Z', ...over
  };
}

describe('StudioAiHistoryCardComponent', () => {
  let fixture: ComponentFixture<StudioAiHistoryCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioAiHistoryCardComponent],
      providers: [provideRouter([]), provideNoopAnimations()]
    }).compileComponents();
    fixture = TestBed.createComponent(StudioAiHistoryCardComponent);
  });

  function rows(): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[data-plan-id]'));
  }

  it('lists the plans with a status tag, the kind and the relative date, plus « Voir tout »', () => {
    fixture.componentRef.setInput('items', [
      plan('a', 'Pending'), plan('b', 'Completed', { systemKey: 'conges' }), plan('c', 'Failed'), plan('d', 'Executing'), plan('e', 'Cancelled')
    ]);
    fixture.detectChanges();

    expect(rows().length).toBe(5);
    expect(rows()[0].textContent).toContain('Plan a');
    expect(rows()[0].textContent).toContain(STUDIO_AI_LABELS.planStatus.Pending);
    expect(rows()[0].textContent).toContain(STUDIO_AI_LABELS.planKind['CreateSystem']);
    expect(rows()[1].textContent).toContain(STUDIO_AI_LABELS.planStatus.Completed);
    expect(rows()[2].textContent).toContain(STUDIO_AI_LABELS.planStatus.Failed);
    const link = fixture.nativeElement.querySelector('a.sar-card__link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/studio/ai/projects');
    expect(link.textContent).toContain(STUDIO_AI_LABELS.rail.seeAllHistory);
  });

  it('only pending plans are clickable and emit « open »', () => {
    fixture.componentRef.setInput('items', [plan('a', 'Pending'), plan('b', 'Completed')]);
    fixture.detectChanges();
    const opened: string[] = [];
    fixture.componentInstance.open.subscribe(p => opened.push(p.id));

    expect(rows()[0].tagName).toBe('BUTTON');
    expect(rows()[1].tagName).toBe('DIV');
    (rows()[0] as HTMLButtonElement).click();

    expect(opened).toEqual(['a']);
  });

  it('shows the empty text, the error text and skeletons while loading', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(STUDIO_AI_LABELS.rail.historyEmpty);

    fixture.componentRef.setInput('error', STUDIO_AI_LABELS.rail.historyLoadFailed);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sar-error')?.textContent).toContain(STUDIO_AI_LABELS.rail.historyLoadFailed);

    fixture.componentRef.setInput('error', null);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('p-skeleton').length).toBeGreaterThan(0);
  });

  describe('rail helpers', () => {
    it('maps statuses to tag severities', () => {
      expect(planStatusSeverity('Completed')).toBe('success');
      expect(planStatusSeverity('Executing')).toBe('info');
      expect(planStatusSeverity('Pending')).toBe('warn');
      expect(planStatusSeverity('Failed')).toBe('danger');
      expect(planStatusSeverity('Cancelled')).toBe('secondary');
      expect(planStatusSeverity('Expired')).toBe('secondary');
    });

    it('formats relative dates in French', () => {
      const now = new Date('2026-09-12T12:00:00Z');
      expect(relativeTime('2026-09-12T11:59:30Z', now)).toBe(STUDIO_AI_LABELS.rail.justNow);
      expect(relativeTime('2026-09-12T11:45:00Z', now)).toBe('il y a 15 min');
      expect(relativeTime('2026-09-12T09:00:00Z', now)).toBe('il y a 3 h');
      expect(relativeTime('2026-09-10T09:00:00Z', now)).toBe('il y a 2 j');
      expect(relativeTime('pas une date', now)).toBe('');
      expect(relativeTime(null, now)).toBe('');
    });
  });
});
