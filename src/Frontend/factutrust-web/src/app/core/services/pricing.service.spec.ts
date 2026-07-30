import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { environment } from '@environments/environment';
import { PricingService } from './pricing.service';

/**
 * Ces specs verrouillent le contrat HTTP du service : URL, verbe et forme du corps.
 * C'est exactement ce qui casse en silence quand le backend bouge — le typage TypeScript
 * ne voit rien passer, et l'écran affiche un prix qui n'est pas celui qui sera facturé.
 */
describe('PricingService', () => {
  let service: PricingService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/pricing`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PricingService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(PricingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  describe('resolve', () => {
    it('envoie productId et quantity, et omet clientId pour une vente sans client', () => {
      service.resolve('prod-1', null, 3).subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/resolve`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('productId')).toBe('prod-1');
      expect(req.request.params.get('quantity')).toBe('3');
      expect(req.request.params.has('clientId')).toBeFalse();

      req.flush({ success: true, data: null });
    });

    it('transmet clientId et date quand ils sont fournis', () => {
      service.resolve('prod-1', 'client-9', 1, '2026-07-20').subscribe();

      const req = httpMock.expectOne(r => r.url === `${base}/resolve`);
      expect(req.request.params.get('clientId')).toBe('client-9');
      expect(req.request.params.get('date')).toBe('2026-07-20');

      req.flush({ success: true, data: null });
    });

    it('remonte le prix résolu et son origine', () => {
      let source: string | undefined;
      let negotiated: boolean | undefined;

      service.resolve('prod-1', 'client-9').subscribe(res => {
        source = res.data?.source;
        negotiated = res.data?.isNegotiated;
      });

      httpMock.expectOne(r => r.url === `${base}/resolve`).flush({
        success: true,
        data: { unitPriceHT: 80, currency: 'TND', source: 'PriceList', isNegotiated: true }
      });

      expect(source).toBe('PriceList');
      expect(negotiated).toBeTrue();
    });
  });

  describe('resolveBatch', () => {
    it('poste les lignes en un seul appel', () => {
      service
        .resolveBatch([{ productId: 'p1', quantity: 2 }, { productId: 'p2', quantity: 1 }], 'client-9')
        .subscribe();

      const req = httpMock.expectOne(`${base}/resolve-batch`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.clientId).toBe('client-9');
      expect(req.request.body.items.length).toBe(2);

      req.flush({ success: true, data: [] });
    });
  });

  describe('grilles tarifaires', () => {
    it('liste les grilles', () => {
      service.getPriceLists().subscribe();

      const req = httpMock.expectOne(`${base}/price-lists`);
      expect(req.request.method).toBe('GET');
      req.flush({ success: true, data: [] });
    });

    it('enregistre le prix d’un produit sur la bonne route', () => {
      service.setPriceListItem('pl-1', 'prod-1', 80).subscribe();

      const req = httpMock.expectOne(`${base}/price-lists/pl-1/items/prod-1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ unitPriceHT: 80 });
      req.flush({ success: true, data: {} });
    });

    it('retire un produit de la grille', () => {
      service.removePriceListItem('pl-1', 'prod-1').subscribe();

      const req = httpMock.expectOne(`${base}/price-lists/pl-1/items/prod-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush({ success: true, data: {} });
    });
  });

  describe('tarification client', () => {
    it('affecte une grille au client', () => {
      service.assignClientPriceList('client-9', 'pl-1').subscribe();

      const req = httpMock.expectOne(`${base}/clients/client-9/price-list`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ priceListId: 'pl-1' });
      req.flush({ success: true, data: {} });
    });

    it('accepte null pour ramener le client au tarif catalogue', () => {
      service.assignClientPriceList('client-9', null).subscribe();

      const req = httpMock.expectOne(`${base}/clients/client-9/price-list`);
      expect(req.request.body).toEqual({ priceListId: null });
      req.flush({ success: true, data: {} });
    });

    it('upsert un prix négocié', () => {
      service
        .upsertClientProductPrice('client-9', 'prod-1', {
          unitPriceHT: 65,
          validFrom: null,
          validUntil: null,
          isActive: true
        })
        .subscribe();

      const req = httpMock.expectOne(`${base}/clients/client-9/products/prod-1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.unitPriceHT).toBe(65);
      req.flush({ success: true, data: 'new-id' });
    });
  });
});
