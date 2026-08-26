import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { ExchangeRequestsPanelComponent } from './exchange-requests-panel.component';
import { ExchangeRequest, ExchangeService, ExchangeThreadDetail } from '@core/services/exchange.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';

function thread(status: string = 'Open'): ExchangeThreadDetail {
  return {
    id: 't1',
    firmClientAssignmentId: 'a1',
    firmTenantId: 'f1',
    companyTenantId: 'c1',
    companyName: 'Ste Fatima',
    firmName: 'Cabinet',
    status,
    createdAt: '2026-08-03T00:00:00Z',
    participants: [
      { userId: 'u-firm', displayName: 'Ahmed Boudaya', role: 'FirmManager', side: 'firm', isOnline: false }
    ]
  };
}

function request(partial: Partial<ExchangeRequest> = {}): ExchangeRequest {
  return {
    id: 'r1',
    threadId: 't1',
    number: 2,
    title: 'cqc',
    description: 'cqcqqc',
    category: 1,
    priority: 1,
    status: 'Open',
    createdByUserId: 'u1',
    createdByTenantId: 'c1',
    createdAt: '2026-08-03T00:00:00Z',
    ...partial
  };
}

describe('ExchangeRequestsPanelComponent', () => {
  let fixture: ComponentFixture<ExchangeRequestsPanelComponent>;
  let cmp: ExchangeRequestsPanelComponent;
  let exchange: jasmine.SpyObj<ExchangeService>;
  let toast: jasmine.SpyObj<ToastService>;
  let confirm: jasmine.SpyObj<ConfirmationService>;

  beforeEach(() => {
    exchange = jasmine.createSpyObj('ExchangeService', [
      'createRequest',
      'changeRequestStatus',
      'assignRequest',
      'listRequestComments',
      'addRequestComment',
      'uploadDocument',
      'downloadDocument'
    ]);
    exchange.listRequestComments.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    exchange.createRequest.and.returnValue(
      of({
        success: true,
        data: request({ id: 'r-new', number: 3, title: 'Nouvelle' }),
        message: null,
        errors: []
      })
    );
    exchange.changeRequestStatus.and.returnValue(
      of({ success: true, data: request({ status: 'InProgress' }), message: null, errors: [] })
    );
    toast = jasmine.createSpyObj('ToastService', ['add']);
    confirm = jasmine.createSpyObj('ConfirmationService', ['confirm']);

    TestBed.configureTestingModule({
      imports: [ExchangeRequestsPanelComponent],
      providers: [
        { provide: ExchangeService, useValue: exchange },
        { provide: ToastService, useValue: toast },
        { provide: ConfirmationService, useValue: confirm }
      ]
    });

    fixture = TestBed.createComponent(ExchangeRequestsPanelComponent);
    cmp = fixture.componentInstance;
    cmp.thread = thread();
    cmp.threadOpen = true;
    cmp.participants = thread().participants;
    cmp.requests = [request()];
    fixture.detectChanges();
  });

  it('does not call createRequest when title is empty and shows validation', () => {
    cmp.openCreateForm = true;
    fixture.detectChanges();
    cmp.createRequest();
    expect(exchange.createRequest).not.toHaveBeenCalled();
    expect(cmp.titleError()).toBe('Le titre est obligatoire');
  });

  it('creates a request with selected priority', () => {
    cmp.openCreateForm = true;
    cmp.newTitle.set('Besoin TVA');
    cmp.newPriority.set(2);
    cmp.createRequest();
    expect(exchange.createRequest).toHaveBeenCalledWith('t1', {
      title: 'Besoin TVA',
      description: undefined,
      category: 0,
      priority: 2
    });
  });

  it('shows take-charge and resolve for an open request in the detail pane', () => {
    cmp.selectedRequestId = 'r1';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Prendre en charge');
    expect(el.textContent).toContain('Résoudre');
    expect(el.textContent).toContain('Clôturer');
  });

  it('hides mutations when the thread is closed', () => {
    cmp.thread = thread('Closed');
    cmp.threadOpen = false;
    cmp.selectedRequestId = 'r1';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).not.toContain('Prendre en charge');
    expect(el.textContent).not.toContain('Clôturer');
  });

  it('hides assign for company users and shows it for the firm', () => {
    cmp.selectedRequestId = 'r1';
    cmp.isFirm = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Assigner à');
    cmp.isFirm = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Assigner à');
  });

  it('shows only close for a resolved request', () => {
    cmp.requests = [request({ status: 'Resolved' })];
    cmp.selectedRequestId = 'r1';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).not.toContain('Prendre en charge');
    expect(el.textContent).not.toContain('Résoudre');
    expect(el.textContent).toContain('Clôturer');
  });

  it('hides mutations when the request is closed', () => {
    cmp.requests = [request({ status: 'Closed' })];
    cmp.selectedRequestId = 'r1';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).not.toContain('Prendre en charge');
    expect(el.textContent).not.toContain('Résoudre');
    expect(el.textContent).not.toContain('Clôturer');
    expect(el.textContent).not.toContain('Assigner à');
  });

  it('asks confirmation before closing a request', () => {
    const req = request();
    cmp.changeStatus(req, 'Closed');
    expect(confirm.confirm).toHaveBeenCalled();
    expect(exchange.changeRequestStatus).not.toHaveBeenCalled();
    const cfg = confirm.confirm.calls.mostRecent().args[0];
    cfg.accept?.();
    expect(exchange.changeRequestStatus).toHaveBeenCalledWith('t1', 'r1', 'Closed');
  });

  it('filters the list by search', () => {
    cmp.requests = [request(), request({ id: 'r2', number: 1, title: 'sggzz', status: 'Resolved' })];
    cmp.search.set('sgg');
    expect(cmp.filteredRequests().map(r => r.id)).toEqual(['r2']);
  });

  it('emits selectedChange when a card is chosen', fakeAsync(() => {
    const spy = jasmine.createSpy('selected');
    cmp.selectedChange.subscribe(spy);
    fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.req-card') as HTMLButtonElement;
    card.click();
    tick();
    expect(spy).toHaveBeenCalledWith('r1');
  }));
});
