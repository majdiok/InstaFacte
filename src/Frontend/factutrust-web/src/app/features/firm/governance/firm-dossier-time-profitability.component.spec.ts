import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { FirmDossierTimeProfitabilityComponent } from './firm-dossier-time-profitability.component';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { FirmCollaboratorsService } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { signal } from '@angular/core';

describe('FirmDossierTimeProfitabilityComponent', () => {
  let component: FirmDossierTimeProfitabilityComponent;
  let fixture: ComponentFixture<FirmDossierTimeProfitabilityComponent>;
  let getSpy: jasmine.Spy;

  beforeEach(async () => {
    getSpy = jasmine.createSpy('getDossierTimeProfitability').and.returnValue(of({
      success: true,
      data: {
        rows: [],
        totalHours: 12.5,
        uniqueBudgetSum: 1000,
        hoursByYear: [{ label: '2026', value: 12.5 }],
        hoursByCompany: []
      }
    }));

    await TestBed.configureTestingModule({
      imports: [FirmDossierTimeProfitabilityComponent],
      providers: [
        provideRouter([]),
        {
          provide: FirmGovernanceService,
          useValue: {
            getDossierTimeProfitability: getSpy,
            exportDossierTimeProfitabilityPdf: jasmine.createSpy('exportPdf').and.returnValue(of(new Blob()))
          }
        },
        {
          provide: FirmCollaboratorsService,
          useValue: { list: () => of([]) }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: AuthService, useValue: { isFirmManager: signal(true) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmDossierTimeProfitabilityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('charge le rapport au démarrage', () => {
    expect(getSpy).toHaveBeenCalled();
    expect(component.report()?.totalHours).toBe(12.5);
  });

  it('applique les filtres lors du reload', () => {
    component.company = 'Acme';
    component.selectedYear = 2026;
    component.selectedMargin = 1;
    component.load();
    expect(getSpy).toHaveBeenCalledWith(jasmine.objectContaining({
      company: 'Acme',
      year: 2026,
      margin: 1
    }));
  });
});
