import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
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
