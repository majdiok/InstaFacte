import { TestBed, fakeAsync, tick } from '@angular/core/testing';

import { provideHttpClient } from '@angular/common/http';

import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@environments/environment';

import { GlobalSearchService } from './global-search.service';

import { AuthService } from './auth.service';



describe('GlobalSearchService', () => {

  let service: GlobalSearchService;

  let httpMock: HttpTestingController;



  const authStub = {

    canAccessPlatformSettings: () => true,

    hasAllModules: () => true,

    hasAllPermissions: () => true,

    isAccountingFirm: () => true,

    isDelegatedMode: () => false

  } as unknown as AuthService;



  beforeEach(() => {

    localStorage.clear();

    TestBed.configureTestingModule({

      providers: [

        provideHttpClient(),

        provideHttpClientTesting(),

        { provide: AuthService, useValue: authStub }

      ]

    });

    service = TestBed.inject(GlobalSearchService);

    httpMock = TestBed.inject(HttpTestingController);

  });



  afterEach(() => {

    httpMock.verify();

    localStorage.clear();

  });



  it('matches navigation entries for factures', () => {

    const results = service.searchNavigation('factures');

    expect(results.some(r => r.route === '/invoices')).toBe(true);

    expect(results[0].kind).toBe('page');

  });



  it('normalizes accents when scoring', () => {

    const score = service.scoreNavMatch('devis', {

      id: 'x',

      label: 'Devis',

      route: '/quotes',

      icon: 'fa-file-lines',

      group: 'page'

    });

    expect(score).toBeGreaterThan(0);

  });



  it('merges nav and document results by score', () => {

    const docs = [{

      kind: 'document' as const,

      id: '1',

      title: 'FAC-2026-0001',

      icon: 'fa-solid fa-file-invoice',

      route: '/invoices/1',

      group: 'Factures',

      score: 100

    }];

    const merged = service.searchAll('fac', docs);

    expect(merged.some(r => r.kind === 'document')).toBe(true);

    expect(merged[0].score).toBeGreaterThanOrEqual(merged[merged.length - 1].score);

  });



  it('debounces document search requests', fakeAsync(() => {

    const results: unknown[] = [];

    service.documentResults$.subscribe(r => results.push(r));



    service.requestDocumentSearch('du');

    service.requestDocumentSearch('dup');

    tick(299);

    service.requestDocumentSearch('dupont');

    tick(300);



    const req = httpMock.expectOne(r => r.url.includes('/search'));

    expect(req.request.params.get('q')).toBe('dupont');

    req.flush({

      success: true,

      data: { query: 'dupont', results: [], truncatedTypes: [] }

    });

    tick();

    expect(results.length).toBe(1);

  }));



  it('returns empty documents on HTTP error', fakeAsync(() => {

    const results: unknown[] = [];

    service.documentResults$.subscribe(r => results.push(r));

    service.requestDocumentSearch('abc');

    tick(300);

    const req = httpMock.expectOne(r => r.url.includes('/search'));

    req.flush('error', { status: 500, statusText: 'Server Error' });

    tick();

    expect(results).toEqual([[]]);

  }));



  it('parses route with query params', () => {

    const parsed = service.parseRouteSelection({

      kind: 'page',

      id: 'x',

      title: 'Factures',

      icon: 'fa-file-invoice',

      route: '/invoices?search=dupont',

      group: 'Pages',

      score: 50

    });

    expect(parsed.route).toEqual(['/', 'invoices']);

    expect(parsed.queryParams).toEqual({ search: 'dupont' });

  });



  it('stores recent selections in localStorage', () => {

    service.recordSelection({

      kind: 'page',

      id: 'nav-invoices',

      title: 'Factures',

      icon: 'fa-file-invoice',

      route: '/invoices',

      group: 'Pages',

      score: 80

    });

    const recent = service.searchNavigation('');

    expect(recent.some(r => r.route === '/invoices')).toBe(true);

  });



  it('respects feature flag', () => {

    const original = environment.globalSearchEnabled;

    environment.globalSearchEnabled = false;

    expect(service.isEnabled()).toBe(false);

    environment.globalSearchEnabled = original;

  });

});

