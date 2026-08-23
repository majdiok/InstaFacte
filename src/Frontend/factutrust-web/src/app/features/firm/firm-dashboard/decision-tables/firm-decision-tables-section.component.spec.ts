import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { FirmDecisionTablesSectionComponent } from './firm-decision-tables-section.component';
import { FirmDashboardService } from '@core/services/firm-dashboard.service';

const decisionPayload = {
  success: true,
  data: {
    criticalFiscalSchedules: [],
    atRiskDossiers: [],
    negativeMargins: [],
    pendingTimeSheets: [],
    honorairesAlerts: [],
    meta: { generatedAt: '2026-03-12T10:00:00Z', partialFailures: [] }
  },
  message: null,
  errors: []
};

describe('FirmDecisionTablesSectionComponent', () => {
  let fixture: ComponentFixture<FirmDecisionTablesSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FirmDecisionTablesSectionComponent, RouterTestingModule],
      providers: [
        {
          provide: FirmDashboardService,
          useValue: {
            getDecisionTables: () => of(decisionPayload)
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmDecisionTablesSectionComponent);
    fixture.detectChanges();
  });

  it('affiche la section pilotage décisionnel', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Pilotage décisionnel');
    expect(text).toContain('Échéances fiscales critiques');
    expect(text).toContain('Dossiers à risque');
    expect(text).not.toContain('Social & paie clients');
  });
});
