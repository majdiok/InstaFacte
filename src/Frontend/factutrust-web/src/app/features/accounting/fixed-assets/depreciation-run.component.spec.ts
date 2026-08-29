import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { DepreciationRunComponent } from './depreciation-run.component';

// T12 (bug C8) — les boutons d'action (dont les liens de navigation rendus via routerLink) ne
// doivent jamais s'afficher vides. Reproduit la régression des captures 2401/2410 : les boutons
// "Retour au registre" / "Voir tableau amortissements", rendus via app-button[routerLink], se
// retrouvaient sans libellé visible à cause d'un bug de projection de contenu partagé par tous
// les boutons de navigation de l'app (voir button.component.ts).
describe('DepreciationRunComponent buttons (T12 / C8)', () => {
  function setup() {
    TestBed.configureTestingModule({
      imports: [DepreciationRunComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });
    const fixture = TestBed.createComponent(DepreciationRunComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return { fixture, httpMock };
  }

  it('renders every action button with a visible, non-empty label', () => {
    const { fixture, httpMock } = setup();

    const buttons = fixture.nativeElement.querySelectorAll('button.btn, a.btn') as NodeListOf<HTMLElement>;
    expect(buttons.length).toBeGreaterThanOrEqual(4);
    buttons.forEach(btn => {
      expect(btn.textContent?.trim()).withContext(btn.outerHTML).not.toBe('');
    });
    httpMock.verify();
  });

  it('shows the Excel export button with both a download icon and a text label', () => {
    const { fixture, httpMock } = setup();

    const exportBtn = Array.from(
      fixture.nativeElement.querySelectorAll('button.btn') as NodeListOf<HTMLElement>
    ).find(btn => btn.textContent?.includes('Export Excel dotations'));

    expect(exportBtn).toBeTruthy();
    expect(exportBtn?.querySelector('i.pi-download')).toBeTruthy();
    expect(exportBtn?.textContent?.trim()).toContain('Export Excel dotations');
    httpMock.verify();
  });

  it('renders the router-link navigation buttons with their text label (regression)', () => {
    const { fixture, httpMock } = setup();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a.btn') as NodeListOf<HTMLAnchorElement>);
    expect(links.length).toBe(2);
    expect(links.some(a => a.textContent?.trim() === 'Retour au registre')).toBeTrue();
    expect(links.some(a => a.textContent?.trim() === 'Voir tableau amortissements')).toBeTrue();
    httpMock.verify();
  });
});

// T14 (bug C7 + message de re-run) — garde anti double-clic dans run() (en plus du [disabled]) et
// affichage du champ alreadyPostedCount (T7 backend) quand aucune nouvelle dotation n'est postée.
describe('DepreciationRunComponent run guard & re-run message (T14 / C7)', () => {
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  function setup() {
    TestBed.configureTestingModule({
      imports: [DepreciationRunComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });
    const fixture = TestBed.createComponent(DepreciationRunComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('issues a single POST when run() is called twice synchronously (double-click guard)', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    const httpMock = TestBed.inject(HttpTestingController);

    component.fiscalYear = 2026;
    component.run();
    component.run(); // synchronous second call must be ignored

    const reqs = httpMock.match(`${base}/depreciation-runs`);
    expect(reqs.length).toBe(1);
    expect(component.loading()).toBeTrue();

    reqs[0].flush({ success: true, data: { fiscalYear: 2026, postedCount: 3, skippedCount: 0, totalDepreciationAmount: 1500, errors: [] } });
    fixture.detectChanges();
    expect(component.loading()).toBeFalse();
    httpMock.verify();
  });

  it('does not post again after the first run completes (a fresh second run is allowed)', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    const httpMock = TestBed.inject(HttpTestingController);

    component.fiscalYear = 2026;
    component.run();
    let req = httpMock.expectOne(`${base}/depreciation-runs`);
    req.flush({ success: true, data: { fiscalYear: 2026, postedCount: 1, skippedCount: 0, totalDepreciationAmount: 500, errors: [] } });
    expect(component.loading()).toBeFalse();

    // After completion, a new run() is allowed.
    component.run();
    req = httpMock.expectOne(`${base}/depreciation-runs`);
    req.flush({ success: true, data: { fiscalYear: 2026, postedCount: 0, skippedCount: 0, totalDepreciationAmount: 0, errors: [], alreadyPostedCount: 1 } });
    httpMock.verify();
  });

  it('shows the explicit re-run message when posted===0 and alreadyPostedCount>0', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    const httpMock = TestBed.inject(HttpTestingController);

    component.fiscalYear = 2026;
    component.run();
    const req = httpMock.expectOne(`${base}/depreciation-runs`);
    req.flush({
      success: true,
      data: { fiscalYear: 2026, postedCount: 0, skippedCount: 0, totalDepreciationAmount: 0, errors: [], alreadyPostedCount: 5 }
    });
    fixture.detectChanges();

    expect(component.rerunMessage()).toBe(
      'Aucune nouvelle dotation — 5 dotation(s) déjà comptabilisée(s) pour cet exercice.'
    );
    expect(fixture.nativeElement.textContent).toContain('Aucune nouvelle dotation');
    expect(fixture.nativeElement.textContent).toContain('5 dotation(s) déjà comptabilisée(s)');
    // The cryptic "Dotations comptabilisées : 0" line must be hidden in the re-run case.
    expect(fixture.nativeElement.textContent).not.toContain('Dotations comptabilisées :');
    httpMock.verify();
  });

  it('keeps the normal result summary when new dotations were posted', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    const httpMock = TestBed.inject(HttpTestingController);

    component.fiscalYear = 2026;
    component.run();
    const req = httpMock.expectOne(`${base}/depreciation-runs`);
    req.flush({
      success: true,
      data: { fiscalYear: 2026, postedCount: 3, skippedCount: 1, totalDepreciationAmount: 1500, errors: ['err'], alreadyPostedCount: 2 }
    });
    fixture.detectChanges();

    expect(component.rerunMessage()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Dotations comptabilisées : 3');
    expect(fixture.nativeElement.textContent).toContain('Ignorées / en erreur : 1');
    httpMock.verify();
  });

  it('tolerates an absent alreadyPostedCount (back-compat) as 0 — no re-run message', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    const httpMock = TestBed.inject(HttpTestingController);

    component.fiscalYear = 2026;
    component.run();
    const req = httpMock.expectOne(`${base}/depreciation-runs`);
    // Older backend payload without alreadyPostedCount.
    req.flush({ success: true, data: { fiscalYear: 2026, postedCount: 0, skippedCount: 0, totalDepreciationAmount: 0, errors: [] } });
    fixture.detectChanges();

    expect(component.rerunMessage()).toBeNull();
    httpMock.verify();
  });
});
