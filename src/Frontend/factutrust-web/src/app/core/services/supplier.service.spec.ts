import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';
import {
  parseSupplierCreateConflict,
  SupplierService,
  SupplierType
} from './supplier.service';

describe('SupplierService', () => {
  let service: SupplierService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/suppliers`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SupplierService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createSupplier poste avec SKIP_ERROR_TOAST (le formulaire gère le 409)', () => {
    service
      .createSupplier({
        name: 'Ste test',
        type: SupplierType.Individual,
        street: '1 rue',
        city: 'Tunis',
        governorate: 'Tunis',
        email: 'dup@test.com',
        paymentTermDays: 30
      })
      .subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ success: true, data: '11111111-1111-1111-1111-111111111111' });
  });

  it('getSupplier garde le traitement d’erreur global', () => {
    service.getSupplier('11111111-1111-1111-1111-111111111111').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/11111111-1111-1111-1111-111111111111`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(false);
    req.flush({ success: true, data: null });
  });
});

describe('parseSupplierCreateConflict', () => {
  const existingId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

  it('lit field et existingSupplierId depuis data', () => {
    const result = parseSupplierCreateConflict(
      {
        error: {
          data: { existingSupplierId: existingId, field: 'email' }
        }
      },
      'Un fournisseur existe déjà avec cet email.'
    );
    expect(result.field).toBe('email');
    expect(result.existingSupplierId).toBe(existingId);
  });

  it('déduit nif depuis le message si field est absent', () => {
    const result = parseSupplierCreateConflict(
      { error: { data: { existingSupplierId: existingId } } },
      'Un fournisseur existe déjà avec ce matricule fiscal.'
    );
    expect(result.field).toBe('nif');
    expect(result.existingSupplierId).toBe(existingId);
  });

  it('ignore le GUID vide', () => {
    const result = parseSupplierCreateConflict(
      { error: { data: '00000000-0000-0000-0000-000000000000' } },
      'Un fournisseur existe déjà avec cet email.'
    );
    expect(result.field).toBe('email');
    expect(result.existingSupplierId).toBeNull();
  });
});
