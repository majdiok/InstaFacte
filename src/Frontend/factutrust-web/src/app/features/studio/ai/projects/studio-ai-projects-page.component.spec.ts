import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { ToastService } from '@core/services/toast.service';
import { of, throwError } from 'rxjs';
import { StudioAiBuildService } from '../../studio-ai-build.service';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiPlanListItemDto, StudioPagedResult } from '../studio-ai.models';
import { STUDIO_AI_PROJECTS_PAGE_SIZE, StudioAiProjectsPageComponent } from './studio-ai-projects-page.component';

function plan(id: string, status: StudioAiPlanListItemDto['status'], over: Partial<StudioAiPlanListItemDto> = {}): StudioAiPlanListItemDto {
  return {
    id, kind: 'CreateSystem', status, title: `Plan ${id}`, entityCount: 3,
    createdAt: '2026-09-12T08:00:00Z', expiresAt: '2026-09-13T08:00:00Z', ...over
  };
}

function page(items: StudioAiPlanListItemDto[], totalCount = items.length, pageNo = 1): StudioPagedResult<StudioAiPlanListItemDto> {
  const totalPages = Math.max(1, Math.ceil(totalCount / STUDIO_AI_PROJECTS_PAGE_SIZE));
  return { items, page: pageNo, pageSize: STUDIO_AI_PROJECTS_PAGE_SIZE, totalCount, totalPages, hasNextPage: pageNo < totalPages, hasPreviousPage: pageNo > 1 };
}

describe('StudioAiProjectsPageComponent', () => {
  let fixture: ComponentFixture<StudioAiProjectsPageComponent>;
  let builds: jasmine.SpyObj<StudioAiBuildService>;
  let toast: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    builds = jasmine.createSpyObj<StudioAiBuildService>('StudioAiBuildService', ['listPlans', 'replayPlan']);
    builds.listPlans.and.returnValue(of({ success: true, data: page([]), message: null, errors: [] }) as never);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['add']);

    await TestBed.configureTestingModule({
      imports: [StudioAiProjectsPageComponent],
      providers: [provideRouter([]), provideNoopAnimations(), { provide: StudioAiBuildService, useValue: builds }, { provide: ToastService, useValue: toast }]
    }).compileComponents();
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
  });

  function create(): void {
    fixture = TestBed.createComponent(StudioAiProjectsPageComponent);
    fixture.detectChanges();
  }

  function rows(): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('tr[data-plan-id]'));
  }

  it('loads the first page with 20 rows per page and renders the columns', () => {
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: page([plan('a', 'Pending'), plan('b', 'Completed', { systemKey: 'conges' }), plan('c', 'Failed')])
    }) as never);
    create();

    expect(builds.listPlans).toHaveBeenCalledWith({ status: null, kind: null, page: 1, pageSize: 20 });
    expect(rows().length).toBe(3);
    const text = fixture.nativeElement.textContent as string;
    for (const col of Object.values(STUDIO_AI_LABELS.history.columns)) expect(text).toContain(col);
    expect(text).toContain(STUDIO_AI_LABELS.history.title);
    expect(fixture.nativeElement.querySelector('[data-testid="projects-count"]').textContent).toContain('3 projet(s)');
    expect(rows()[0].textContent).toContain(STUDIO_AI_LABELS.planStatus.Pending);
    expect(rows()[1].textContent).toContain('conges');
  });

  it('offers « Reprendre » for pending plans (→ /studio/ai?plan=id) and « Ouvrir le système » for completed ones', () => {
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: page([plan('a', 'Pending'), plan('b', 'Completed', { systemKey: 'conges' }), plan('c', 'Cancelled')])
    }) as never);
    create();

    const resume = rows()[0].querySelector('a[data-action="resume"]') as HTMLAnchorElement;
    expect(resume).not.toBeNull();
    expect(resume.textContent).toContain(STUDIO_AI_LABELS.history.resume);
    expect(resume.getAttribute('href')).toBe('/studio/ai?plan=a');
    const open = rows()[1].querySelector('a[data-action="open-system"]') as HTMLAnchorElement;
    expect(open).not.toBeNull();
    expect(open.textContent).toContain(STUDIO_AI_LABELS.history.openSystem);
    expect(open.getAttribute('href')).toBe('/studio/systems/conges');
    expect(rows()[2].querySelector('[data-action]')).toBeNull();
  });

  it('reloads from page 1 when a filter changes and sends status / kind to the API', () => {
    create();
    fixture.componentInstance.page.set(3);

    fixture.componentInstance.setStatus('Pending');
    expect(builds.listPlans.calls.mostRecent().args[0]).toEqual({ status: 'Pending', kind: null, page: 1, pageSize: 20 });

    fixture.componentInstance.setKind('Report');
    expect(builds.listPlans.calls.mostRecent().args[0]).toEqual({ status: 'Pending', kind: 'Report', page: 1, pageSize: 20 });
  });

  it('paginates server-side', () => {
    const many = Array.from({ length: 20 }, (_, i) => plan(`p${i}`, 'Completed'));
    builds.listPlans.and.returnValue(of({ success: true, data: page(many, 45), message: null, errors: [] }) as never);
    create();

    expect(fixture.nativeElement.querySelector('p-paginator')).not.toBeNull();
    fixture.componentInstance.onPage({ page: 1, first: 20, rows: 20 });
    expect(builds.listPlans.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({ page: 2 }));
    expect(fixture.componentInstance.page()).toBe(2);
  });

  it('shows the empty message when there is nothing', () => {
    create();
    expect(rows().length).toBe(0);
    expect(fixture.nativeElement.querySelector('[data-testid="projects-empty"]').textContent).toContain(STUDIO_AI_LABELS.history.empty);
    expect(fixture.nativeElement.querySelector('p-paginator')).toBeNull();
  });

  it('shows the French error on HTTP failure and lets the user retry', () => {
    builds.listPlans.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })) as never);
    create();

    expect(fixture.componentInstance.error()).toBe(STUDIO_AI_LABELS.history.loadFailed);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(STUDIO_AI_LABELS.history.loadFailed);

    builds.listPlans.and.returnValue(of({ success: true, data: page([plan('a', 'Pending')]), message: null, errors: [] }) as never);
    fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.componentInstance.error()).toBeNull();
    expect(rows().length).toBe(1);
  });
  it('Rejouer ⇒ POST replay puis navigation vers /studio/ai?plan=<nouvel id>', () => {
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: page([plan('a', 'Completed', { systemKey: 'conges', replayable: true })])
    }) as never);
    builds.replayPlan.and.returnValue(of({
      success: true, message: null, errors: [],
      data: { plan: { id: 'p-new', kind: 'CreateSystem', status: 'Pending' }, spec: {} }
    }) as never);
    create();

    const button = rows()[0].querySelector('button[data-action="replay"]') as HTMLButtonElement;
    expect(button).not.toBeNull();
    expect(button.textContent).toContain(STUDIO_AI_LABELS.history.replay);
    button.click();

    expect(builds.replayPlan).toHaveBeenCalledWith('a');
    expect(router.navigate).toHaveBeenCalledWith(['/studio/ai'], { queryParams: { plan: 'p-new' } });
    expect(toast.add).not.toHaveBeenCalled();
    expect(fixture.componentInstance.replaying()).toBeNull();
  });

  it('409 ⇒ toast « conflit », reste sur la page', () => {
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: page([plan('a', 'Failed', { replayable: true, errorMessage: 'Quota dépassé' })])
    }) as never);
    builds.replayPlan.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409, statusText: 'Conflict' })));
    create();

    (rows()[0].querySelector('button[data-action="replay"]') as HTMLButtonElement).click();

    expect(builds.replayPlan).toHaveBeenCalledWith('a');
    expect(toast.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'warn', summary: STUDIO_AI_LABELS.replay.conflict }));
    expect(router.navigate).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toBeNull();
    expect(fixture.componentInstance.replaying()).toBeNull();
    expect(rows().length).toBe(1);
  });

  it('Rejouer absent quand replayable est faux', () => {
    builds.listPlans.and.returnValue(of({
      success: true, message: null, errors: [],
      data: page([plan('a', 'Completed', { systemKey: 'conges', replayable: false }), plan('b', 'Cancelled'), plan('c', 'Failed', { replayable: true })])
    }) as never);
    create();

    expect(rows()[0].querySelector('[data-action="replay"]')).toBeNull();
    expect(rows()[0].querySelector('a[data-action="open-system"]')).not.toBeNull();
    expect(rows()[1].querySelector('[data-action="replay"]')).toBeNull();
    expect(rows()[2].querySelector('button[data-action="replay"]')).not.toBeNull();
    expect(builds.replayPlan).not.toHaveBeenCalled();
  });
});
