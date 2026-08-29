import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AuthService, User } from '@core/services/auth.service';
import {
  AccountingService,
  FiscalAdjustmentCatalogEntryDto,
  FiscalResultDeclarationDto
} from '../services/accounting.service';
import { FiscalResultComponent } from './fiscal-result.component';

const baseComputation = {
  accountingResult: 1000,
  totalReintegrations: 0,
  totalDeductions: 0,
  resultBeforeCarryForward: 1000,
  deficitsImputed: 0,
  deferredDepreciationImputed: 0,
  taxableResult: 1000,
  deficitGeneratedThisYear: 0,
  taxpayerKind: 0,
  appliedIsRate: 0.15,
  taxOnResult: 150,
  minimumTaxRegime: 0,
  minimumTax: 0,
  taxDue: 150,
  css: 0,
  totalTaxDue: 150
};

function makeDto(overrides: Partial<FiscalResultDeclarationDto> = {}): FiscalResultDeclarationDto {
  return {
    fiscalYear: 2025,
    taxpayerKind: 0,
    status: 0,
    accountingResult: 1000,
    appliedIsRate: 0.15,
    localTurnoverTtc: 10000,
    minimumTaxRegime: 0,
    suggestedLocalTurnoverTtc: 10000,
    suggestedAccountingResult: null,
    acomptesPaid: 0,
    withholdingSuffered: 0,
    priorTaxCredit: 0,
    adjustments: [],
    carryForwards: [],
    computation: baseComputation as unknown as FiscalResultDeclarationDto['computation'],
    isFinalized: false,
    finalizedAt: null,
    isNew: false,
    fiscalLiasseEnabled: true,
    warnings: [],
    ...overrides
  };
}

const catalog: FiscalAdjustmentCatalogEntryDto[] = [];

const companyUser: User = {
  id: 'u1',
  email: 'company@test.c',
  firstName: 'A',
  lastName: 'B',
  fullName: 'A B',
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: '00000000-0000-0000-0000-000000000001',
  companyName: 'Société Test',
  tenantKind: 'Company',
  twoFactorEnabled: false,
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
  effectivePermissions: ['accounting:read']
} as User;

const companyUserWithCreate: User = {
  ...companyUser,
  id: 'u1b',
  effectivePermissions: ['accounting:read', 'accounting:create']
} as User;

const firmDelegatedUser: User = {
  id: 'u2',
  email: 'firm@test.c',
  firstName: 'O',
  lastName: 'G',
  fullName: 'O G',
  role: 'FirmManager',
  roleDisplay: 'Responsable cabinet',
  tenantId: '00000000-0000-0000-0000-000000000002',
  companyName: 'Cabinet Test',
  tenantKind: 'AccountingFirm',
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou',
  twoFactorEnabled: false,
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
  effectivePermissions: ['accounting:read', 'accounting:create', 'accounting:validate', 'firm:manage']
} as User;

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('FiscalResultComponent', () => {
  let fixture: ComponentFixture<FiscalResultComponent>;
  let accountingMock: {
    getFiscalResult: jasmine.Spy;
    getFiscalAdjustmentCatalog: jasmine.Spy;
    upsertFiscalResult: jasmine.Spy;
    finalizeFiscalResult: jasmine.Spy;
  };

  beforeEach(async () => {
    accountingMock = {
      getFiscalResult: jasmine.createSpy('getFiscalResult').and.returnValue(of({ success: true, data: makeDto() })),
      getFiscalAdjustmentCatalog: jasmine
        .createSpy('getFiscalAdjustmentCatalog')
        .and.returnValue(of({ success: true, data: catalog })),
      upsertFiscalResult: jasmine.createSpy('upsertFiscalResult'),
      finalizeFiscalResult: jasmine.createSpy('finalizeFiscalResult')
    };

    await TestBed.configureTestingModule({
      imports: [FiscalResultComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AccountingService, useValue: accountingMock }
      ]
    }).compileComponents();
  });

  async function renderForUser(user: User): Promise<void> {
    setUser(TestBed.inject(AuthService), user);
    fixture = TestBed.createComponent(FiscalResultComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  // T18 : masquage des contrôles de mutation selon la permission.
  it('masque le bouton Enregistrer pour un profil lecture seule (accounting:read uniquement)', async () => {
    await renderForUser(companyUser);
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Enregistrer et recalculer');
  });

  it('affiche le bouton Enregistrer pour un profil avec accounting:create', async () => {
    await renderForUser(companyUserWithCreate);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Enregistrer et recalculer');
  });

  it('affiche le bouton Finaliser pour un cabinet en mode dossier délégué habilité', async () => {
    await renderForUser(firmDelegatedUser);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Finaliser');
  });

  it("masque le bouton Finaliser pour un profil sans capacité de validation déléguée", async () => {
    await renderForUser(companyUserWithCreate);
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Finaliser');
  });

  // T19 : bornes d'exercice.
  it('désactive le bouton Charger et affiche un message pour une année hors bornes (2150)', async () => {
    await renderForUser(companyUserWithCreate);
    fixture.componentInstance.onYearInputChange(2150);
    fixture.detectChanges();
    expect(fixture.componentInstance.isYearValid()).toBeFalse();
    const button: HTMLButtonElement | null = fixture.nativeElement.querySelector('[aria-label="Charger la feuille de détermination"]');
    expect(button?.disabled).toBeTrue();
    expect((fixture.nativeElement.textContent as string)).toContain('Exercice invalide');
  });

  // T20 : course sur Charger — seule la dernière réponse doit être rendue.
  it('deux appels rapprochés à load() ne rendent que la dernière réponse (switchMap)', async () => {
    await renderForUser(companyUserWithCreate);
    const first$ = new Subject<{ success: boolean; data: FiscalResultDeclarationDto }>();
    const second$ = new Subject<{ success: boolean; data: FiscalResultDeclarationDto }>();
    accountingMock.getFiscalResult.and.returnValues(first$, second$);

    fixture.componentInstance.fiscalYear = 2024;
    fixture.componentInstance.load();
    fixture.componentInstance.fiscalYear = 2023;
    fixture.componentInstance.load();

    first$.next({ success: true, data: makeDto({ fiscalYear: 2024, accountingResult: 111 }) });
    second$.next({ success: true, data: makeDto({ fiscalYear: 2023, accountingResult: 222 }) });
    fixture.detectChanges();

    expect(fixture.componentInstance.model.accountingResult).toBe(222);
  });

  // BUG #007 : avertissement d'écart CA.
  it("affiche un avertissement non bloquant quand le CA saisi s'écarte de plus de 5 % du CA calculé", async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ localTurnoverTtc: 8500, suggestedLocalTurnoverTtc: 10000 }) })
    );
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.caVarianceWarning()).toContain('%');
    fixture.detectChanges();
    expect((fixture.nativeElement.textContent as string)).toContain("chiffre d'affaires saisi s'écarte");
  });

  it("n'affiche aucun avertissement quand l'écart de CA est inférieur à 5 %", async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ localTurnoverTtc: 9800, suggestedLocalTurnoverTtc: 10000 }) })
    );
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.caVarianceWarning()).toBeNull();
  });

  // BUG #009 : libellés vides bloquent la sauvegarde.
  it('bloque la sauvegarde si une ligne de réintégration/déduction a un libellé vide', async () => {
    await renderForUser(companyUserWithCreate);
    fixture.componentInstance.model.adjustments.push({
      catalogCode: null,
      kind: 0,
      label: '   ',
      amount: 100,
      isAutoSuggested: false
    });
    fixture.componentInstance.save();
    expect(accountingMock.upsertFiscalResult).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('libellé');
  });

  // BUG #010 : bornes originYear + expiryYear en lecture seule.
  it('signale une année d’origine de report invalide (≥ exercice courant)', async () => {
    await renderForUser(companyUserWithCreate);
    const cf = { kind: 0, originYear: fixture.componentInstance.fiscalYear, initialAmount: 100, imputedThisYear: 0, expiryYear: null };
    expect(fixture.componentInstance.isOriginYearValid(cf)).toBeFalse();
  });

  it('le champ expiryYear est en lecture seule dans le tableau des reports', async () => {
    await renderForUser(companyUserWithCreate);
    fixture.componentInstance.model.carryForwards.push({
      kind: 0,
      originYear: fixture.componentInstance.fiscalYear - 1,
      initialAmount: 100,
      imputedThisYear: 0,
      expiryYear: 2030
    });
    fixture.detectChanges();
    const expiryInput: HTMLInputElement | null = fixture.nativeElement.querySelector('input[placeholder="calculée automatiquement"]');
    expect(expiryInput?.disabled).toBeTrue();
  });

  // T23 : garde de modifications non enregistrées.
  it("isDirty() est faux juste après le chargement puis vrai après modification du modèle", async () => {
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.isDirty()).toBeFalse();
    fixture.componentInstance.model.accountingResult = 999999;
    expect(fixture.componentInstance.isDirty()).toBeTrue();
  });

  it('canDeactivate() ne demande pas confirmation quand le modèle est propre', async () => {
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.canDeactivate()).toBeTrue();
  });

  it('canDeactivate() demande confirmation quand le modèle est modifié', async () => {
    await renderForUser(companyUserWithCreate);
    fixture.componentInstance.model.accountingResult = 42;
    const confirmSpy = spyOn(window, 'confirm').and.returnValue(false);
    expect(fixture.componentInstance.canDeactivate()).toBeFalse();
    expect(confirmSpy).toHaveBeenCalled();
  });

  // T24 : bouton « Reprendre » le résultat comptable suggéré (résout BUG #006).
  it('affiche le résultat comptable suggéré et un bouton Reprendre à côté du champ', async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ accountingResult: 1000, suggestedAccountingResult: 8400 }) })
    );
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.suggestedAccountingResult()).toBe(8400);
    const btn: HTMLButtonElement | null = fixture.nativeElement.querySelector('[aria-label="Reprendre le résultat comptable calculé"]');
    expect(btn).toBeTruthy();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Résultat comptable = résultat avant impôt − IS comptabilisé');
    expect(text).toContain('Résultat comptable calculé');
  });

  it('cliquer « Reprendre » met à jour le champ résultat comptable et marque l’état dirty', async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ accountingResult: 1000, suggestedAccountingResult: 8400 }) })
    );
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.model.accountingResult).toBe(1000);
    expect(fixture.componentInstance.isDirty()).toBeFalse();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-label="Reprendre le résultat comptable calculé"]');
    btn?.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.model.accountingResult).toBe(8400);
    expect(fixture.componentInstance.isDirty()).toBeTrue();
  });

  it("ne propose pas le bouton Reprendre quand aucun résultat comptable n'est suggéré", async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ suggestedAccountingResult: null }) })
    );
    await renderForUser(companyUserWithCreate);
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Résultat comptable calculé');
  });

  it("n'écrase pas le résultat comptable saisi sans action utilisateur", async () => {
    accountingMock.getFiscalResult.and.returnValue(
      of({ success: true, data: makeDto({ accountingResult: 5000, suggestedAccountingResult: 8400 }) })
    );
    await renderForUser(companyUserWithCreate);
    expect(fixture.componentInstance.model.accountingResult).toBe(5000);
    expect(fixture.componentInstance.isDirty()).toBeFalse();
  });
});
