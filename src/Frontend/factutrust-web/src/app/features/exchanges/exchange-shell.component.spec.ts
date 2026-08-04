import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { signal } from '@angular/core';
import { ExchangeShellComponent } from './exchange-shell.component';
import { AuthService } from '@core/services/auth.service';
import {
  ExchangeBootstrap,
  ExchangeService,
  ExchangeThreadListItem,
  PagedExchangeMessages
} from '@core/services/exchange.service';
import { ApiResponse } from '@core/services/auth.service';
import { ExchangeBadgeService } from '@core/services/exchange-badge.service';
import { FirmAssignmentService, FirmClientAssignment, FirmClientDossier } from '@core/services/firm-assignment.service';
import { ALL_NAV_ITEMS } from '@core/config/app-navigation.registry';
import { environment } from '@environments/environment';

function assignment(partial: Partial<FirmClientAssignment> = {}): FirmClientAssignment {
  return {
    id: 'a1',
    companyTenantId: 'c1',
    companyName: 'Ma Société',
    firmTenantId: 'f1',
    firmDisplayName: 'ste compta',
    status: 'Active',
    statusDisplay: 'Active',
    requestedAt: '2026-07-13T10:39:00Z',
    ...partial
  };
}

function threadDetail(status: string | number = 'Open') {
  return {
    id: 't1',
    firmClientAssignmentId: 'a1',
    firmTenantId: 'f1',
    companyTenantId: 'c1',
    companyName: 'ABC SARL',
    firmName: 'ste compta',
    status,
    createdAt: '2026-07-30T00:00:00Z',
    participants: []
  };
}

function emptyPage(items: never[] = []): PagedExchangeMessages {
  return { items, hasMore: false };
}

function bootstrapPayload(partial: Partial<ExchangeBootstrap> = {}): ExchangeBootstrap {
  return {
    threads: [],
    companyAssignment: assignment() as never,
    firmClients: null,
    activeThread: threadDetail('Open') as never,
    messages: emptyPage(),
    requests: null,
    tasks: null,
    documents: null,
    history: null,
    openRequestsCount: 0,
    unreadCount: 0,
    emptyHint: null,
    ...partial
  };
}

describe('ExchangeShellComponent messaging', () => {
  let fixture: ComponentFixture<ExchangeShellComponent>;
  let exchange: jasmine.SpyObj<ExchangeService>;
  let assignments: jasmine.SpyObj<FirmAssignmentService>;
  let previousBootstrapFlag: boolean;

  function setup(opts: {
    firm?: boolean;
    current?: FirmClientAssignment | null;
    clients?: FirmClientDossier[];
    threads?: ExchangeThreadListItem[];
    bootstrapV2?: boolean;
  }) {
    previousBootstrapFlag = environment.featureFlags.exchangeBootstrapV2;
    environment.featureFlags.exchangeBootstrapV2 = opts.bootstrapV2 ?? true;

    const auth = {
      isAccountingFirm: () => !!opts.firm,
      user: () => ({
        id: 'u1',
        role: opts.firm ? 'FirmManager' : 'Administrator',
        fullName: 'Admin'
      }),
      isAdmin: () => !opts.firm
    };

    exchange = jasmine.createSpyObj('ExchangeService', [
      'listThreads',
      'ensureThread',
      'getMessages',
      'listRequests',
      'listTasks',
      'listDocuments',
      'getHistory',
      'getThread',
      'sendMessage',
      'uploadDocument',
      'markRead',
      'markReadBatch',
      'getBootstrap'
    ]);
    exchange.listThreads.and.returnValue(
      of({ success: true, data: opts.threads ?? [], message: null, errors: [] })
    );
    exchange.ensureThread.and.returnValue(
      of({ success: true, data: threadDetail('Open'), message: null, errors: [] })
    );
    exchange.getThread.and.returnValue(
      of({ success: true, data: threadDetail('Open'), message: null, errors: [] })
    );
    exchange.getMessages.and.returnValue(of({ success: true, data: emptyPage(), message: null, errors: [] }));
    exchange.listRequests.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    exchange.listTasks.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    exchange.listDocuments.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    exchange.getHistory.and.returnValue(of({ success: true, data: [], message: null, errors: [] }));
    exchange.sendMessage.and.returnValue(
      of({
        success: true,
        data: {
          id: 'm1',
          threadId: 't1',
          authorUserId: 'u1',
          authorTenantId: 'c1',
          authorDisplayName: 'Admin',
          visibility: 'ClientVisible',
          body: 'Bonjour',
          sentAt: new Date().toISOString(),
          readReceipts: [],
          attachments: []
        },
        message: null,
        errors: []
      })
    );
    exchange.uploadDocument.and.returnValue(
      of({
        success: true,
        data: {
          id: 'd1',
          threadId: 't1',
          messageId: 'm1',
          fileName: 'a.pdf',
          contentType: 'application/pdf',
          sizeBytes: 100,
          uploadedByUserId: 'u1',
          uploadedAt: new Date().toISOString()
        },
        message: null,
        errors: []
      })
    );
    exchange.markRead.and.returnValue(of({ success: true, data: null, message: null, errors: [] }));
    exchange.markReadBatch.and.returnValue(of({ success: true, data: null, message: null, errors: [] }));
    exchange.getBootstrap.and.returnValue(
      of({
        success: true,
        data: bootstrapPayload({
          firmClients: opts.firm
            ? (opts.clients ?? [
                {
                  assignmentId: 'a1',
                  companyTenantId: 'c1',
                  companyName: 'ABC SARL',
                  activeSince: '2026-07-01T00:00:00Z'
                }
              ])
            : null,
          threads: opts.threads ?? []
        }),
        message: null,
        errors: []
      })
    );

    assignments = jasmine.createSpyObj('FirmAssignmentService', ['getCompanyCurrent', 'getActiveClients']);
    assignments.getCompanyCurrent.and.returnValue(
      of({ success: true, data: opts.current === undefined ? assignment() : opts.current, message: null, errors: [] })
    );
    assignments.getActiveClients.and.returnValue(
      of({
        success: true,
        data: opts.clients ?? [
          {
            assignmentId: 'a1',
            companyTenantId: 'c1',
            companyName: 'ABC SARL',
            activeSince: '2026-07-01T00:00:00Z'
          }
        ],
        message: null,
        errors: []
      })
    );

    const badge = {
      invalidate: jasmine.createSpy('invalidate'),
      unreadCount: signal(0),
      openRequests: signal(0)
    };

    TestBed.configureTestingModule({
      imports: [ExchangeShellComponent],
      providers: [
        provideRouter([
          { path: 'exchanges', component: ExchangeShellComponent },
          { path: 'exchanges/:threadId', component: ExchangeShellComponent },
          { path: 'firm/exchanges', component: ExchangeShellComponent },
          { path: 'firm/exchanges/:threadId', component: ExchangeShellComponent }
        ]),
        { provide: AuthService, useValue: auth },
        { provide: ExchangeService, useValue: exchange },
        { provide: FirmAssignmentService, useValue: assignments },
        { provide: ExchangeBadgeService, useValue: badge }
      ]
    });

    fixture = TestBed.createComponent(ExchangeShellComponent);
    fixture.detectChanges();
  }

  afterEach(() => {
    if (previousBootstrapFlag !== undefined) {
      environment.featureFlags.exchangeBootstrapV2 = previousBootstrapFlag;
    }
  });

  it('shows composer when thread status is PascalCase Open', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    fixture.detectChanges();
    fixture.componentInstance['activeThread'].set(threadDetail('Open') as never);
    fixture.componentInstance['loading'].set(false);
    fixture.componentInstance['contentLoading'].set(false);
    fixture.componentInstance['emptyHint'].set(null);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const composer = el.querySelector('.composer');
    expect(composer).toBeTruthy();
    const textarea = el.querySelector('.composer textarea') as HTMLTextAreaElement | null;
    expect(textarea?.getAttribute('placeholder')).toContain('Écrire un message');
  }));

  it('hides composer when thread status is Closed', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    fixture.componentInstance['activeThread'].set(threadDetail('Closed') as never);
    fixture.componentInstance['loading'].set(false);
    fixture.componentInstance['contentLoading'].set(false);
    fixture.componentInstance['emptyHint'].set(null);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.composer')).toBeFalsy();
  }));

  it('company bootstrap V2 does not call listThreads or tab endpoints', fakeAsync(() => {
    setup({ bootstrapV2: true, current: assignment({ status: 'Active' }) });
    tick();
    expect(exchange.getBootstrap).toHaveBeenCalled();
    expect(exchange.listThreads).not.toHaveBeenCalled();
    expect(exchange.listRequests).not.toHaveBeenCalled();
    expect(exchange.listTasks).not.toHaveBeenCalled();
    expect(exchange.listDocuments).not.toHaveBeenCalled();
    expect(exchange.getHistory).not.toHaveBeenCalled();
  }));

  it('legacy company bootstrap skips listThreads and loads messages only', fakeAsync(() => {
    setup({ bootstrapV2: false, current: assignment({ status: 'Active' }) });
    tick();
    expect(exchange.listThreads).not.toHaveBeenCalled();
    expect(assignments.getCompanyCurrent).toHaveBeenCalled();
    expect(exchange.ensureThread).toHaveBeenCalled();
    expect(exchange.getMessages).toHaveBeenCalled();
    expect(exchange.listRequests).not.toHaveBeenCalled();
    expect(exchange.listTasks).not.toHaveBeenCalled();
  }));

  it('firm openFirmCompany without thread calls ensureThread with assignmentId', fakeAsync(() => {
    setup({
      firm: true,
      bootstrapV2: false,
      threads: [],
      clients: [
        {
          assignmentId: 'a99',
          companyTenantId: 'c99',
          companyName: 'DEMO INDUSTRIE',
          activeSince: '2026-07-01T00:00:00Z'
        }
      ]
    });
    tick();
    fixture.detectChanges();
    expect(exchange.ensureThread).toHaveBeenCalledWith('a99');
  }));

  it('send with pending file uploads document with messageId', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    const cmp = fixture.componentInstance;
    cmp['activeThread'].set(threadDetail('Open') as never);
    cmp['loading'].set(false);
    cmp['contentLoading'].set(false);
    cmp.draft.set('Voici le fichier');
    cmp.pendingFiles.set([new File(['x'], 'a.pdf', { type: 'application/pdf' })]);
    cmp.send();
    tick();
    expect(exchange.sendMessage).toHaveBeenCalled();
    expect(exchange.uploadDocument).toHaveBeenCalledWith('t1', jasmine.any(File), 'm1');
  }));

  it('setTab(documents) writes ?tab=documents and updates activeTab', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    const cmp = fixture.componentInstance;
    cmp['activeThread'].set(threadDetail('Open') as never);
    cmp['loading'].set(false);
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    cmp.setTab('documents');
    expect(cmp.activeTab()).toBe('documents');
    expect(navSpy).toHaveBeenCalledWith(
      jasmine.arrayContaining(['/exchanges']),
      jasmine.objectContaining({
        queryParams: { tab: 'documents' },
        queryParamsHandling: 'merge'
      })
    );
  }));

  it('setTab(conversation) clears the tab query param', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    const cmp = fixture.componentInstance;
    cmp['activeThread'].set(threadDetail('Open') as never);
    cmp.activeTab.set('documents');
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    cmp.setTab('conversation');
    expect(cmp.activeTab()).toBe('conversation');
    expect(navSpy).toHaveBeenCalledWith(
      jasmine.anything(),
      jasmine.objectContaining({
        queryParams: { tab: null },
        queryParamsHandling: 'merge'
      })
    );
  }));

  it('auditEventLabel resolves PascalCase event types for history UI', fakeAsync(() => {
    setup({ current: assignment({ status: 'Active' }) });
    tick();
    const cmp = fixture.componentInstance;
    expect(cmp.auditEventLabel('RequestCreated')).toBe('Demande créée');
    expect(cmp.auditEventLabel('DocumentShared')).toBe('Document partagé');
  }));

  // Anti-régression : reproduit la source de la concurrence DbContext sur /api/exchanges/bootstrap.
  // Avant fix : paramMap + queryParamMap s'abonnaient séparément, chacun émettait sa valeur initiale
  // synchroniquement, ce qui déclenchait potentiellement 2 requêtes bootstrap en parallèle.
  // Après fix : combineLatest + debounceTime(0) + distinctUntilChanged garantit une seule requête.
  it('fires a single bootstrap request when paramMap and queryParamMap both emit on init', fakeAsync(() => {
    setup({ firm: true, bootstrapV2: true, current: assignment({ status: 'Active' }) });
    tick();
    fixture.detectChanges();
    expect(exchange.getBootstrap).toHaveBeenCalledTimes(1);
  }));

  // Anti-régression : quand plusieurs bootstrap sont enchaînés rapidement (typiquement
  // une navigation successive entre threads), seule la dernière réponse doit être
  // appliquée à l'état du composant. Les réponses tardives des bootstraps annulés
  // doivent être ignorées (inFlightBootstrapSub?.unsubscribe() joue le rôle de switchMap).
  // Sans ce comportement, le composant pouvait afficher les données d'un ancien thread
  // par-dessus le thread nouvellement demandé.
  it('applies only the last in-flight bootstrap response', fakeAsync(() => {
    setup({ firm: true, bootstrapV2: true, current: assignment({ status: 'Active' }) });
    tick();
    const cmp = fixture.componentInstance as unknown as {
      bootstrap: (threadId: string | null) => void;
      activeThread: () => { id: string } | null;
    };

    // Sujets contrôlés → on maîtrise l'ordre des émissions.
    const first = new Subject<ApiResponse<ExchangeBootstrap>>();
    const second = new Subject<ApiResponse<ExchangeBootstrap>>();
    const third = new Subject<ApiResponse<ExchangeBootstrap>>();
    exchange.getBootstrap.and.returnValues(first, second, third);

    cmp.bootstrap('t-alpha');
    cmp.bootstrap('t-beta');
    cmp.bootstrap('t-gamma');

    // Les 3 bootstrap ont bien été demandés côté API…
    expect(exchange.getBootstrap.calls.count()).toBeGreaterThanOrEqual(3);
    const lastArgs = exchange.getBootstrap.calls.mostRecent().args;
    expect(lastArgs[0]).toBe('t-gamma');

    // …mais seule la dernière subscription reste active (les 2 premières ont été
    // annulées par inFlightBootstrapSub?.unsubscribe() dans bootstrapV2).
    first.next({
      success: true,
      data: bootstrapPayload({ activeThread: { ...threadDetail('Open'), id: 't-alpha-payload' } as never }),
      message: null,
      errors: []
    } as ApiResponse<ExchangeBootstrap>);
    first.complete();
    second.next({
      success: true,
      data: bootstrapPayload({ activeThread: { ...threadDetail('Open'), id: 't-beta-payload' } as never }),
      message: null,
      errors: []
    } as ApiResponse<ExchangeBootstrap>);
    second.complete();
    third.next({
      success: true,
      data: bootstrapPayload({ activeThread: { ...threadDetail('Open'), id: 't-gamma-payload' } as never }),
      message: null,
      errors: []
    } as ApiResponse<ExchangeBootstrap>);
    third.complete();
    tick();

    // Preuve : les payloads périmés (t-alpha, t-beta) n'ont PAS remplacé activeThread.
    expect(cmp.activeThread()?.id).toBe('t-gamma-payload');
  }));
});

describe('Échanges navigation registry', () => {
  it('exposes a single Échanges rail entry without submenu children', () => {
    const exchanges = ALL_NAV_ITEMS.find(i => i.label === 'Échanges');
    expect(exchanges).toBeTruthy();
    expect(exchanges!.route).toBe('/exchanges');
    expect(exchanges!.children).toBeUndefined();
  });
});
