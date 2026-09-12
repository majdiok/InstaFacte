import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { CurrenciesComponent } from './currencies.component';
import { AccountingService, CurrencyDto } from '../services/accounting.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

function currency(overrides: Partial<CurrencyDto> = {}): CurrencyDto {
  return {
    id: 'cur-1',
    code: 'EUR',
    label: 'Euro',
    decimalPlaces: 2,
    ratePeriodicity: 1,
    isActive: true,
    isFunctional: false,
    configuredRateCount: 12,
    expectedRateCount: 12,
    ...overrides
  };
}

const FUNCTIONAL = currency({
  id: 'cur-tnd',
  code: 'TND',
  label: 'Dinar Tunisien',
  decimalPlaces: 3,
  ratePeriodicity: 0,
  isFunctional: true,
  configuredRateCount: 0,
  expectedRateCount: 0
});

async function build(currencies: CurrencyDto[], permissions: string[]): Promise<ComponentFixture<CurrenciesComponent>> {
  await TestBed.resetTestingModule();
  await TestBed.configureTestingModule({
    imports: [CurrenciesComponent, RouterTestingModule],
    providers: [
      provideNoopAnimations(),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: AccountingService,
        useValue: {
          getCurrencies: () => of({ success: true, data: currencies }),
          toggleCurrency: () => of({ success: true, data: true }),
          createCurrency: () => of({ success: true, data: 'new-id' }),
          updateCurrency: () => of({ success: true, data: true })
        }
      },
      {
        provide: AuthService,
        useValue: {
          hasPermission: (p: string) => permissions.includes(p),
          hasAllPermissions: (ps: string[]) => ps.every(p => permissions.includes(p)),
          user: signal(null)
        }
      }
    ]
  }).compileComponents();

  const fixture = TestBed.createComponent(CurrenciesComponent);
  fixture.detectChanges();
  return fixture;
}

describe('CurrenciesComponent', () => {
  const READ_ONLY = [PERMISSIONS.accounting.read];
  const MANAGER = [PERMISSIONS.accounting.read, PERMISSIONS.accounting.currenciesManage];

  it('affiche la devise de tenue sans état de taux', async () => {
    const fixture = await build([FUNCTIONAL], MANAGER);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('TND');
    expect(text).toContain('Devise de tenue');
    // La devise de tenue n'a pas de taux : ni « Non configuré », ni « Complet ».
    expect(text).not.toContain('Non configuré');
    expect(text).not.toContain('Complet');
  });

  it('signale une devise dont aucun taux n’est saisi', async () => {
    const fixture = await build([currency({ configuredRateCount: 0 })], MANAGER);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Non configuré');
  });

  it('signale une table de taux incomplète avec le décompte', async () => {
    const fixture = await build([currency({ configuredRateCount: 7 })], MANAGER);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('7 / 12');
  });

  it('signale une table de taux complète', async () => {
    const fixture = await build([currency()], MANAGER);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Complet');
  });

  describe('verrouillage par permission', () => {
    it('masque la création sans accounting:currencies_manage', async () => {
      const fixture = await build([currency()], READ_ONLY);

      expect(fixture.componentInstance.canManage()).toBeFalse();
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter une devise');
    });

    it('expose la création avec la permission', async () => {
      const fixture = await build([currency()], MANAGER);

      expect(fixture.componentInstance.canManage()).toBeTrue();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ajouter une devise');
    });
  });

  describe('remontée du refus serveur', () => {
    // Le symptôme d'origine : un 400 métier porteur d'un message précis était affiché
    // « Erreur réseau », ce qui laissait l'utilisateur sans explication actionnable.
    const SERVER_MESSAGE = "La gestion multi-devises n'est pas activée.";

    function badRequest(): HttpErrorResponse {
      return new HttpErrorResponse({
        status: 400,
        statusText: 'Bad Request',
        url: '/api/accounting/currencies',
        error: { success: false, data: null, message: null, error: SERVER_MESSAGE }
      });
    }

    function networkDown(): HttpErrorResponse {
      return new HttpErrorResponse({ status: 0, statusText: 'Unknown Error', url: '/api/accounting/currencies' });
    }

    async function buildWith(failure: HttpErrorResponse): Promise<ComponentFixture<CurrenciesComponent>> {
      await TestBed.resetTestingModule();
      await TestBed.configureTestingModule({
        imports: [CurrenciesComponent, RouterTestingModule],
        providers: [
          provideNoopAnimations(),
          provideHttpClient(),
          provideHttpClientTesting(),
          {
            provide: AccountingService,
            useValue: {
              getCurrencies: () => of({ success: true, data: [currency()] }),
              createCurrency: () => throwError(() => failure),
              updateCurrency: () => throwError(() => failure),
              toggleCurrency: () => throwError(() => failure)
            }
          },
          {
            provide: AuthService,
            useValue: {
              hasPermission: () => true,
              hasAllPermissions: () => true,
              user: signal(null)
            }
          }
        ]
      }).compileComponents();

      const fixture = TestBed.createComponent(CurrenciesComponent);
      fixture.detectChanges();
      return fixture;
    }

    it('affiche le message du serveur plutôt que « Erreur réseau » à la création', async () => {
      const fixture = await buildWith(badRequest());
      const cmp = fixture.componentInstance;

      cmp.startCreate();
      Object.assign(cmp.form()!, { code: 'EUR', label: 'Euro' });
      cmp.save();

      expect(cmp.error()).toBe(SERVER_MESSAGE);
      expect(cmp.error()).not.toBe('Erreur réseau');
    });

    it('affiche le message du serveur sur le basculement actif / inactif', async () => {
      const fixture = await buildWith(badRequest());
      const cmp = fixture.componentInstance;

      cmp.toggle(currency());

      expect(cmp.error()).toBe(SERVER_MESSAGE);
    });

    it('signale bien une panne réseau réelle comme telle', async () => {
      // L'inverse compte autant : le status 0 doit continuer d'être distingué. Comme les 104
      // autres emplacements du module, l'écran passe son propre message en repli réseau plutôt
      // que de laisser remonter le message générique du service, qui cite une URL localhost.
      const fixture = await buildWith(networkDown());
      const cmp = fixture.componentInstance;

      cmp.startCreate();
      Object.assign(cmp.form()!, { code: 'EUR', label: 'Euro' });
      cmp.save();

      expect(cmp.error()).not.toBe(SERVER_MESSAGE);
      expect(cmp.error()).toBe('Erreur réseau');
    });
  });

  describe('exercice', () => {
    it('ignore un exercice hors bornes et conserve le précédent', async () => {
      const fixture = await build([currency()], MANAGER);
      const cmp = fixture.componentInstance;
      const initial = cmp.fiscalYear();

      cmp.onYearChange(1999);
      expect(cmp.fiscalYear()).toBe(initial);

      cmp.onYearChange(2101);
      expect(cmp.fiscalYear()).toBe(initial);
    });

    it('accepte un exercice valide', async () => {
      const fixture = await build([currency()], MANAGER);
      const cmp = fixture.componentInstance;

      cmp.onYearChange(2026);

      expect(cmp.fiscalYear()).toBe(2026);
    });
  });
});
