import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { FixedAssetSettingsComponent } from './fixed-asset-settings.component';

// P4 (plan « Exercices décalés ») — écran de paramétrage de l'exercice (mois de début + format de
// libellé), lecture/écriture via GET/PUT /settings, bandeau de limitation transitoire si décalé.
describe('FixedAssetSettingsComponent (P4)', () => {
  const base = `${environment.apiUrl}/accounting/fixed-assets`;

  const civilSettings = {
    success: true,
    data: { fiscalYearStartMonth: 1, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026' }
  };
  const offsetSettings = {
    success: true,
    data: { fiscalYearStartMonth: 7, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026/2027' }
  };

  function setup() {
    TestBed.configureTestingModule({
      imports: [FixedAssetSettingsComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
    });
    const fixture = TestBed.createComponent(FixedAssetSettingsComponent);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → GET /settings
    return { fixture, httpMock };
  }

  it('loads the settings and populates the form (civil exercise)', () => {
    const { fixture, httpMock } = setup();
    httpMock.expectOne(`${base}/settings`).flush(civilSettings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.loading()).toBeFalse();
    expect(component.form.fiscalYearStartMonth).toBe(1);
    expect(component.form.fiscalYearLabelFormat).toBe('N/N+1');
    expect(component.isOffset()).toBeFalse();

    // Month selector + label-format radios are rendered.
    const select = fixture.nativeElement.querySelector('select.accounting-filter-input') as HTMLSelectElement;
    expect(select).toBeTruthy();
    expect(Array.from(select.querySelectorAll('option')).length).toBe(12);
    expect(fixture.nativeElement.querySelectorAll('input[type="radio"]').length).toBe(2);
    // No offset limitation banner for a civil exercise.
    expect(fixture.nativeElement.textContent).not.toContain('Limitation transitoire');
    httpMock.verify();
  });

  it('shows the transitory limitation banner when the exercise is offset', () => {
    const { fixture, httpMock } = setup();
    httpMock.expectOne(`${base}/settings`).flush(offsetSettings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.isOffset()).toBeTrue();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Limitation transitoire');
    expect(text).toContain('Grand-Livre');
    // The live preview surfaces the N/N+1 label and the exercise boundary.
    expect(text).toContain('2026/2027');
    httpMock.verify();
  });

  it('sends a PUT with the edited form values on save and confirms success', () => {
    const { fixture, httpMock } = setup();
    httpMock.expectOne(`${base}/settings`).flush(civilSettings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.fiscalYearStartMonth = 7;
    component.form.fiscalYearLabelFormat = 'N/N+1';
    component.save();

    const req = httpMock.expectOne(`${base}/settings`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.fiscalYearStartMonth).toBe(7);
    expect(req.request.body.fiscalYearLabelFormat).toBe('N/N+1');
    req.flush({
      success: true,
      data: { fiscalYearStartMonth: 7, fiscalYearLabelFormat: 'N/N+1', fiscalYearLabelSample: '2026/2027' }
    });
    fixture.detectChanges();

    expect(component.saving()).toBeFalse();
    expect(component.success()).toContain('enregistrés');
    expect(component.form.fiscalYearStartMonth).toBe(7);
    httpMock.verify();
  });

  it('displays an error and falls back to civil defaults when the load fails', () => {
    const { fixture, httpMock } = setup();
    httpMock.expectOne(`${base}/settings`).flush(
      { success: false, message: 'indisponible' },
      { status: 500, statusText: 'Server Error' }
    );
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.loading()).toBeFalse();
    expect(component.error()).toBeTruthy();
    // Repli civil : pas de bandeau de limitation.
    expect(component.isOffset()).toBeFalse();
    expect(component.form.fiscalYearStartMonth).toBe(1);
    httpMock.verify();
  });

  it('surfaces a server error message when the PUT fails', () => {
    const { fixture, httpMock } = setup();
    httpMock.expectOne(`${base}/settings`).flush(civilSettings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.save();

    const req = httpMock.expectOne(`${base}/settings`);
    req.flush(
      { success: false, message: 'mois invalide' },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(component.saving()).toBeFalse();
    expect(component.error()).toContain('mois invalide');
    httpMock.verify();
  });
});
