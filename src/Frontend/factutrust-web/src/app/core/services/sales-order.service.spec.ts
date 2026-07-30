import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { environment } from '@environments/environment';
import { SalesOrderService } from './sales-order.service';

/**
 * Verrouille le contrat HTTP : URL, verbe, paramètres. Une divergence ici ne fait échouer
 * aucun typage TypeScript — elle se voit seulement à l'exécution, sur un écran vide.
 */
describe('SalesOrderService', () => {
  let service: SalesOrderService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/sales-orders`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SalesOrderService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SalesOrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  describe('getSalesOrders', () => {
    it('n’envoie aucun paramètre quand aucun filtre n’est posé', () => {
      service.getSalesOrders().subscribe();

      const req = httpMock.expectOne(r => r.url === base);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.keys().length).toBe(0);

      req.flush({ success: true, data: { items: [], totalCount: 0 } });
    });

    it('transmet les filtres, dont openOnly qui restreint au carnet', () => {
      service
        .getSalesOrders({
          search: 'CDE-2026',
          status: 'Confirmed',
          fromDate: '2026-01-01',
          toDate: '2026-12-31',
          openOnly: true,
          page: 2,
          pageSize: 50
        })
        .subscribe();

      const req = httpMock.expectOne(r => r.url === base);
      expect(req.request.params.get('search')).toBe('CDE-2026');
      expect(req.request.params.get('status')).toBe('Confirmed');
      expect(req.request.params.get('openOnly')).toBe('true');
      expect(req.request.params.get('page')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('50');

      req.flush({ success: true, data: { items: [], totalCount: 0 } });
    });

    it('omet openOnly quand il est faux, pour ne pas restreindre par mégarde', () => {
      service.getSalesOrders({ openOnly: false }).subscribe();

      const req = httpMock.expectOne(r => r.url === base);
      expect(req.request.params.has('openOnly')).toBeFalse();

      req.flush({ success: true, data: { items: [], totalCount: 0 } });
    });
  });

  describe('getSalesOrdersSummary', () => {
    it('reprend les filtres mais pas la pagination — les totaux portent sur tout le jeu', () => {
      service.getSalesOrdersSummary({ search: 'ACME', page: 3, pageSize: 20 }).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/summary`);
      expect(req.request.params.get('search')).toBe('ACME');
      expect(req.request.params.has('page')).toBeFalse();
      expect(req.request.params.has('pageSize')).toBeFalse();

      req.flush({ success: true, data: null });
    });
  });

  describe('getBacklog', () => {
    it('appelle le carnet sans paramètre par défaut', () => {
      service.getBacklog().subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/backlog`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.keys().length).toBe(0);

      req.flush({ success: true, data: [] });
    });

    it('transmet clientId et dueBefore', () => {
      service.getBacklog('client-9', '2026-08-31').subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/backlog`);
      expect(req.request.params.get('clientId')).toBe('client-9');
      expect(req.request.params.get('dueBefore')).toBe('2026-08-31');

      req.flush({ success: true, data: [] });
    });
  });

  describe('transitions', () => {
    it('confirme par POST sans corps', () => {
      service.confirmSalesOrder('so-1').subscribe();

      const req = httpMock.expectOne(`${base}/so-1/confirm`);
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: {} });
    });

    it('annule en transmettant le motif', () => {
      service.cancelSalesOrder('so-1', 'Client désisté').subscribe();

      const req = httpMock.expectOne(`${base}/so-1/cancel`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ reason: 'Client désisté' });
      req.flush({ success: true, data: {} });
    });

    it('solde sur une route distincte de l’annulation', () => {
      service.closeSalesOrder('so-1', 'Reliquat abandonné').subscribe();

      const req = httpMock.expectOne(`${base}/so-1/close`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ reason: 'Reliquat abandonné' });
      req.flush({ success: true, data: {} });
    });
  });

  describe('createSalesOrder', () => {
    it('poste l’en-tête et ses lignes', () => {
      service
        .createSalesOrder({
          clientId: 'client-9',
          orderDate: '2026-07-30',
          lines: [{ productId: 'p1', quantity: 5, unitPrice: 80 }]
        })
        .subscribe();

      const req = httpMock.expectOne(base);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.clientId).toBe('client-9');
      expect(req.request.body.lines.length).toBe(1);
      expect(req.request.body.lines[0].unitPrice).toBe(80);

      req.flush({ success: true, data: 'new-id' });
    });
  });
});
