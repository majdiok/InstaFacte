import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { environment } from '@environments/environment';
import { StudioAiBuildService, StudioAiPlanDto } from './studio-ai-build.service';

describe('StudioAiBuildService', () => {
  let service: StudioAiBuildService;
  let http: HttpTestingController;

  const plan: StudioAiPlanDto = {
    id: 'p-1',
    kind: 'CreateSystem',
    status: 'Pending',
    summaryJson: '{"title":"Congés","steps":[],"entities":[],"warnings":[]}',
    createdAt: '2026-07-28T10:00:00Z',
    expiresAt: '2026-07-28T11:00:00Z'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(StudioAiBuildService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads a plan by id', () => {
    let received: StudioAiPlanDto | undefined;
    service.getPlan('p-1').subscribe(r => (received = r.data ?? undefined));

    const req = http.expectOne(`${environment.apiUrl}/studio/ai/plans/p-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: plan, message: null, errors: [] });

    expect(received?.status).toBe('Pending');
  });

  it('cancels a plan without executing it', () => {
    let cancelled: StudioAiPlanDto | undefined;
    service.cancel('p-1').subscribe(r => (cancelled = r.data ?? undefined));

    const req = http.expectOne(`${environment.apiUrl}/studio/ai/plans/p-1/cancel`);
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: { ...plan, status: 'Cancelled' }, message: null, errors: [] });

    expect(cancelled?.status).toBe('Cancelled');
  });

  it('confirms over a streaming response, emitting progress then result', async () => {
    // La confirmation utilise fetch (streaming SSE), hors pipeline HttpClient : on simule le corps.
    const body = [
      'data: {"type":"studio_progress","content":"{\\"phase\\":\\"creating_system\\",\\"label\\":\\"Création\\",\\"status\\":\\"running\\"}"}\n\n',
      'data: {"type":"studio_result","content":"{\\"id\\":\\"p-1\\"}"}\n\n',
      'data: {"type":"done"}\n\n'
    ].join('');

    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve(new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } }))
    );

    const types: string[] = [];
    await new Promise<void>((resolve, reject) => {
      service.confirm('p-1').subscribe({
        next: ev => types.push(ev.type),
        error: reject,
        complete: resolve
      });
    });

    expect(types).toEqual(['studio_progress', 'studio_result', 'done']);
  });

  it('surfaces a French message when the plan is already handled', async () => {
    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve(new Response('', { status: 409, statusText: 'Conflict' }))
    );

    const message = await new Promise<string>(resolve => {
      service.confirm('p-1').subscribe({ error: (e: Error) => resolve(e.message) });
    });

    expect(message).toContain('déjà été traité');
  });
});
