import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Company, CompanyService } from '@core/services/company.service';
import { StockService } from '@core/services/stock.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { CompanySectorService } from '@core/services/company-sector.service';
import { CompanyComponent } from './company.component';

function company(partial: Partial<Company> = {}): Company {
  return {
    id: 'c1',
    companyName: 'Ma Société',
    tradeName: null,
    nif: '1234567/A/B/C/000',
    commerceRegistry: null,
    taxRegime: 0,
    taxRegimeDisplay: 'Régime réel',
    address: { street: 'Rue A', streetLine2: null, postalCode: null, city: 'Tunis', governorate: 'Tunis' },
    email: 'contact@example.com',
    phone: '20123456',
    website: null,
    logoUrl: null,
    bankName: null,
    rib: null,
    iban: null,
    invoicePrefix: null,
    defaultPaymentTerms: null,
    invoiceFooter: null,
    warehouseName: null,
    cnssEmployerNumber: null,
    clientPortalEnabled: true,
    ...partial
  } as Company;
}

describe('CompanyComponent — secteur d\'activité (plan v1 §2.3)', () => {
  function setup(sectorOverrides: Partial<{
    companySegment: string | null;
    businessDomain: string | null;
  }> = {}) {
    const companyService = {
      getCompany: () => of({ success: true, data: company() })
    };
    const stockService = {
      getWarehouses: () => of({ success: true, data: [] })
    };
    const sectorService = {
      getSector: jasmine.createSpy('getSector').and.returnValue(
        of({
          success: true,
          data: {
            companySegment: sectorOverrides.companySegment ?? 'commerce',
            businessDomain: sectorOverrides.businessDomain ?? 'textile-habillement',
            availableSegments: [
              { code: 'commerce', labelFr: 'Commerce' },
              { code: 'services', labelFr: 'Services' }
            ],
            availableDomains: [
              { code: 'textile-habillement', labelFr: 'Textile & habillement' },
              { code: 'btp-construction', labelFr: 'BTP & construction' }
            ]
          },
          message: null,
          errors: []
        })
      ),
      previewSector: jasmine.createSpy('previewSector').and.returnValue(
        of({
          success: true,
          data: { modulesToEnable: [{ id: 16, labelFr: 'Projets' }], templates: ['Devis BTP'], warnings: [] },
          message: null,
          errors: []
        })
      ),
      applySector: jasmine.createSpy('applySector').and.returnValue(
        of({
          success: true,
          data: { companySegment: 'services', businessDomain: 'btp-construction', enabledModuleIds: [16], warnings: [] },
          message: null,
          errors: []
        })
      )
    };
    const authService = {
      refreshUserProfile: jasmine.createSpy('refreshUserProfile').and.returnValue(of({ success: true }))
    };
    const toastService = { add: jasmine.createSpy('add') };

    TestBed.configureTestingModule({
      imports: [CompanyComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: CompanyService, useValue: companyService },
        { provide: StockService, useValue: stockService },
        { provide: CompanySectorService, useValue: sectorService },
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toastService }
      ]
    });

    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, sectorService, authService, toastService };
  }

  it('charge le segment/domaine courants au démarrage', () => {
    const { component, sectorService } = setup();

    expect(sectorService.getSector).toHaveBeenCalled();
    expect(component.selectedSegmentCode).toBe('commerce');
    expect(component.selectedDomainCode).toBe('textile-habillement');
    expect(component.isSectorChanged()).toBe(false);
  });

  it('isSectorChanged() devient vrai seulement après une modification du segment ou domaine', () => {
    const { component } = setup();

    component.selectedDomainCode = 'btp-construction';
    expect(component.isSectorChanged()).toBe(true);
  });

  it('previewSectorChange() appelle previewSector et ouvre la boîte de dialogue', () => {
    const { component, sectorService } = setup();
    component.selectedDomainCode = 'btp-construction';

    component.previewSectorChange();

    expect(sectorService.previewSector).toHaveBeenCalledWith({
      companySegment: 'commerce',
      businessDomain: 'btp-construction'
    });
    expect(component.previewDialogVisible).toBe(true);
    expect(component.sectorPreview()?.modulesToEnable[0].labelFr).toBe('Projets');
  });

  it("confirmSectorChange() applique le changement puis rafraîchit le profil sans relogin", () => {
    const { component, sectorService, authService, toastService } = setup();
    component.selectedSegmentCode = 'services';
    component.selectedDomainCode = 'btp-construction';

    component.confirmSectorChange();

    expect(sectorService.applySector).toHaveBeenCalledWith({
      companySegment: 'services',
      businessDomain: 'btp-construction'
    });
    expect(authService.refreshUserProfile).toHaveBeenCalled();
    expect(toastService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
    expect(component.previewDialogVisible).toBe(false);
  });

  it('affiche le message français exact en cas de 429 (une fois par jour)', () => {
    const { component, sectorService, toastService } = setup();
    sectorService.applySector.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            error: { success: false, message: 'Vous ne pouvez changer de secteur qu\'une fois par jour', errors: [] }
          })
      )
    );
    component.selectedSegmentCode = 'services';
    component.selectedDomainCode = 'btp-construction';

    component.confirmSectorChange();

    expect(toastService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: 'Vous ne pouvez changer de secteur qu\'une fois par jour'
      })
    );
  });
});
