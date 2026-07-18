import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';
import { StudioFileService } from './studio-file.service';

describe('StudioFileService', () => {
  let svc: StudioFileService;
  let httpMock: HttpTestingController;

  const tenant = '11111111-2222-3333-4444-555555555555';
  const file = 'a'.repeat(32) + '.png';
  const stored = `/uploads/tenants/${tenant}/studio/contacts/${file}`;
  const endpoint = `${environment.apiUrl}/studio/records/contacts/files/${file}`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    svc = TestBed.inject(StudioFileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it("mappe une valeur stockée vers l'endpoint authentifié", () => {
    expect(svc.endpointUrl(stored)).toBe(endpoint);
  });

  it('rejette les formes inconnues (tenant invalide, clé invalide, traversal, hors studio)', () => {
    expect(svc.endpointUrl('/uploads/tenants/x/studio/contacts/a.png')).toBeNull();
    expect(svc.endpointUrl(`/uploads/tenants/${tenant}/studio/Bad Key/${file}`)).toBeNull();
    expect(svc.endpointUrl(`/uploads/tenants/${tenant}/studio/contacts/../${file}`)).toBeNull();
    expect(svc.endpointUrl(`/uploads/tenants/${tenant}/products/${file}`)).toBeNull();
  });

  it("récupère le blob via l'endpoint et produit une object URL", () => {
    let url: string | null | undefined;
    svc.objectUrl(stored).subscribe(u => (url = u));

    const req = httpMock.expectOne(endpoint);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['x'], { type: 'image/png' }));

    expect(url).toMatch(/^blob:/);
  });

  it('réutilise le cache pour une même valeur (une seule requête)', () => {
    let first: string | null | undefined;
    let second: string | null | undefined;
    svc.objectUrl(stored).subscribe(u => (first = u));
    httpMock.expectOne(endpoint).flush(new Blob(['x'], { type: 'image/png' }));

    svc.objectUrl(stored).subscribe(u => (second = u)); // pas de nouvelle requête
    httpMock.expectNone(endpoint);
    expect(second).toBe(first!);
  });

  it('retourne les URLs absolues (http…) telles quelles', () => {
    let url: string | null | undefined;
    svc.objectUrl('https://exemple.test/x.png').subscribe(u => (url = u));
    expect(url).toBe('https://exemple.test/x.png');
  });
});
