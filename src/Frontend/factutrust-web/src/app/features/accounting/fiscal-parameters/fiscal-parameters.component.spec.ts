import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AccountingService, IncomeTaxYearParameterDto } from '../services/accounting.service';
import { FiscalParametersComponent } from './fiscal-parameters.component';

function makeParams(overrides: Partial<IncomeTaxYearParameterDto> = {}): IncomeTaxYearParameterDto {
  return {
    fiscalYear: 2025,
    isStandardRate: 0.15,
    isReducedRate: 0.1,
    isSectorRate: 0.35,
    minTaxRate: 0.002,
    minTaxReducedRate: 0.001,
    minTaxFloorTnd: 300,
    minTaxFloorReducedTnd: 100,
    cssApplies: true,
    cssRate: 0.01,
    cssFloorTnd: 200,
    acompteRate: 0.3,
    acompteCount: 3,
    deficitCarryForwardYears: 5,
    roundTaxableToDinar: true,
    irppBracketsJson: '[{"lower":0,"rate":0},{"lower":5000,"rate":26}]',
    isUserModified: true,
    ...overrides
  };
}

describe('FiscalParametersComponent', () => {
  let fixture: ComponentFixture<FiscalParametersComponent>;
  let accountingMock: {
    getIncomeTaxParameters: jasmine.Spy;
    updateIncomeTaxParameters: jasmine.Spy;
  };

  beforeEach(async () => {
    accountingMock = {
      getIncomeTaxParameters: jasmine
        .createSpy('getIncomeTaxParameters')
        .and.returnValue(of({ success: true, data: makeParams() })),
      updateIncomeTaxParameters: jasmine.createSpy('updateIncomeTaxParameters')
    };

    await TestBed.configureTestingModule({
      imports: [FiscalParametersComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AccountingService, useValue: accountingMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FiscalParametersComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  // T19 : bornes d'exercice.
  it('désactive le bouton Charger pour une année hors bornes (1900)', () => {
    fixture.componentInstance.onYearInputChange(1900);
    fixture.detectChanges();
    expect(fixture.componentInstance.isYearValid()).toBeFalse();
    const button: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      "[aria-label=\"Charger les paramètres de l'exercice\"]"
    );
    expect(button?.disabled).toBeTrue();
    expect((fixture.nativeElement.textContent as string)).toContain('Exercice invalide');
  });

  // T20 : course sur Charger — seule la dernière réponse rendue.
  it('deux appels rapprochés à load() ne rendent que la dernière réponse (switchMap)', () => {
    const first$ = new Subject<{ success: boolean; data: IncomeTaxYearParameterDto }>();
    const second$ = new Subject<{ success: boolean; data: IncomeTaxYearParameterDto }>();
    accountingMock.getIncomeTaxParameters.and.returnValues(first$, second$);

    fixture.componentInstance.fiscalYear = 2024;
    fixture.componentInstance.load();
    fixture.componentInstance.fiscalYear = 2023;
    fixture.componentInstance.load();

    first$.next({ success: true, data: makeParams({ fiscalYear: 2024, acompteCount: 1 }) });
    second$.next({ success: true, data: makeParams({ fiscalYear: 2023, acompteCount: 9 }) });

    expect(fixture.componentInstance.model?.acompteCount).toBe(9);
  });

  // T22 : validation client du barème IRPP.
  it('bloque la sauvegarde si le barème IRPP est un JSON invalide', () => {
    fixture.componentInstance.model!.irppBracketsJson = '{not valid json';
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('Barème IRPP');
  });

  it('bloque la sauvegarde si une tranche du barème IRPP a un rate hors [0;100]', () => {
    fixture.componentInstance.model!.irppBracketsJson = '[{"lower":0,"rate":0},{"lower":5000,"rate":150}]';
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('Barème IRPP');
  });

  it('bloque la sauvegarde si les bornes lower du barème IRPP ne sont pas strictement croissantes', () => {
    fixture.componentInstance.model!.irppBracketsJson = '[{"lower":0,"rate":0},{"lower":0,"rate":26}]';
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('Barème IRPP');
  });

  it('accepte un barème IRPP valide', () => {
    accountingMock.updateIncomeTaxParameters.and.returnValue(of({ success: true, data: makeParams() }));
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).toHaveBeenCalled();
  });

  // BUG #015 : bannière d'erreur si le JSON reçu du serveur est corrompu.
  it("affiche une bannière d'erreur si le barème IRPP reçu du serveur est un JSON cassé", async () => {
    accountingMock.getIncomeTaxParameters.and.returnValue(
      of({ success: true, data: makeParams({ irppBracketsJson: '{broken' }) })
    );
    fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.componentInstance.jsonError()).toBeTruthy();
    expect((fixture.nativeElement.textContent as string)).toContain('illisible');
  });

  // BUG #016 : texte du bandeau reformulé sans double négation.
  it('affiche le nouveau texte du bandeau "paramètres standards" sans double négation', () => {
    accountingMock.getIncomeTaxParameters.and.returnValue(
      of({ success: true, data: makeParams({ isUserModified: false }) })
    );
    fixture.componentInstance.load();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Paramètres standards en vigueur');
    expect(text).not.toContain('ne seront plus réinitialisés automatiquement');
  });

  // BUG #017 : re-validation des bornes numériques dans save().
  it("bloque la sauvegarde si un taux collé dépasse 1 (ex. 500 au lieu de 0,5 %)", () => {
    fixture.componentInstance.model!.isStandardRate = 500;
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('Taux');
  });

  it("bloque la sauvegarde si le nombre d'acomptes est hors [0;12]", () => {
    fixture.componentInstance.model!.acompteCount = 24;
    fixture.componentInstance.save();
    expect(accountingMock.updateIncomeTaxParameters).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain("acomptes");
  });

  // T23 : garde de modifications non enregistrées.
  it('canDeactivate() ne demande pas confirmation quand le modèle est propre', () => {
    expect(fixture.componentInstance.canDeactivate()).toBeTrue();
  });

  it('canDeactivate() demande confirmation quand le modèle est modifié', () => {
    fixture.componentInstance.model!.acompteCount = 7;
    const confirmSpy = spyOn(window, 'confirm').and.returnValue(false);
    expect(fixture.componentInstance.canDeactivate()).toBeFalse();
    expect(confirmSpy).toHaveBeenCalled();
  });

  it("aucune confirmation n'est requise après une sauvegarde réussie", () => {
    accountingMock.updateIncomeTaxParameters.and.returnValue(of({ success: true, data: makeParams({ acompteCount: 7 }) }));
    fixture.componentInstance.model!.acompteCount = 7;
    fixture.componentInstance.save();
    expect(fixture.componentInstance.isDirty()).toBeFalse();
  });
});
