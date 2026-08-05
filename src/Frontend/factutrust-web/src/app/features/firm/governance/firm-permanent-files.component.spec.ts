import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { FirmPermanentFilesComponent } from './firm-permanent-files.component';
import { FirmGovernanceService, PermanentFile } from '@core/services/firm-governance.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { FirmGovernanceActionsService } from '../shared/firm-governance-actions.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';

const files: PermanentFile[] = [
  {
    id: '1',
    firmClientAssignmentId: 'a1',
    companyTenantId: 't1',
    companyName: 'Ste Bouzgarou',
    nif: '1111111/A/B/C/000',
    status: 2,
    statusDisplay: 'Complet',
    wizardStep: 6,
    hasTaxCertificate: false,
    isDigitized: false,
    missionResigned: false,
    labCompleted: true,
    missionAccepted: true,
    annualFeeAmount: 1500,
    billingFrequencyDisplay: 'Mensuel',
    currency: 'TND',
    completionPercent: 100,
    representatives: [],
    shareholders: []
  },
  {
    id: '2',
    firmClientAssignmentId: 'a2',
    companyTenantId: 't2',
    companyName: 'Hadad',
    nif: '2222222/A/B/C/000',
    status: 1,
    statusDisplay: 'En cours',
    wizardStep: 4,
    hasTaxCertificate: false,
    isDigitized: false,
    missionResigned: false,
    labCompleted: false,
    missionAccepted: false,
    completionPercent: 50,
    nextRecommendedStep: 4,
    nextActionLabel: 'Questionnaire LAB',
    representatives: [{ id: 'r1', lastName: 'H', firstName: 'A', role: 'Gérant' }],
    shareholders: []
  },
  {
    id: '3',
    firmClientAssignmentId: 'a3',
    companyTenantId: 't3',
    companyName: 'Archivé SA',
    status: 9,
    statusDisplay: 'Archivé',
    wizardStep: 6,
    hasTaxCertificate: false,
    isDigitized: false,
    missionResigned: false,
    labCompleted: true,
    missionAccepted: true,
    representatives: [],
    shareholders: []
  }
];

describe('FirmPermanentFilesComponent', () => {
  let fixture: ComponentFixture<FirmPermanentFilesComponent>;
  let component: FirmPermanentFilesComponent;
  let listPermanentFiles: jasmine.Spy;

  beforeEach(async () => {
    listPermanentFiles = jasmine.createSpy('listPermanentFiles').and.returnValue(of({ success: true, data: files }));

    await TestBed.configureTestingModule({
      imports: [FirmPermanentFilesComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: { get: (k: string) => k === 'status' ? null : null } }
          }
        },
        {
          provide: FirmGovernanceService,
          useValue: {
            listPermanentFiles,
            archivePermanentFile: () => of({ success: true })
          }
        },
        {
          provide: FirmAssignmentService,
          useValue: { getActiveClients: () => of({ success: true, data: [] }) }
        },
        {
          provide: FirmGovernanceActionsService,
          useValue: { initializePermanentFile: jasmine.createSpy('initializePermanentFile') }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmPermanentFilesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('affiche le tableau même quand la liste est vide', () => {
    listPermanentFiles.and.returnValue(of({ success: true, data: [] }));
    component.reload();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Dossiers permanents existants');
  });

  it('affiche une erreur et le tableau si l’API échoue', () => {
    listPermanentFiles.and.returnValue(throwError(() => ({ error: { message: 'Erreur serveur' } })));
    component.reload();
    fixture.detectChanges();
    expect(component.loadError()).toContain('Erreur serveur');
    expect(fixture.nativeElement.textContent).toContain('Dossiers permanents existants');
  });

  it('filtre par recherche société', () => {
    component.search.set('hadad');
    expect(component.filteredFiles().length).toBe(1);
    expect(component.filteredFiles()[0].companyName).toBe('Hadad');
  });

  it('applique le filtre status depuis queryParams', async () => {
    await TestBed.resetTestingModule();
    listPermanentFiles = jasmine.createSpy('listPermanentFiles').and.returnValue(of({ success: true, data: files }));
    await TestBed.configureTestingModule({
      imports: [FirmPermanentFilesComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: { get: (k: string) => k === 'status' ? '2' : null } }
          }
        },
        {
          provide: FirmGovernanceService,
          useValue: { listPermanentFiles, archivePermanentFile: () => of({ success: true }) }
        },
        { provide: FirmAssignmentService, useValue: { getActiveClients: () => of({ success: true, data: [] }) } },
        { provide: FirmGovernanceActionsService, useValue: { initializePermanentFile: jasmine.createSpy('initializePermanentFile') } },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    const f = TestBed.createComponent(FirmPermanentFilesComponent);
    f.detectChanges();
    expect(f.componentInstance.statusFilter()).toBe(2);
  });

  it('masque les archivés par défaut', () => {
    expect(component.filteredFiles().some(f => f.status === 9)).toBeFalse();
    component.includeArchived.set(true);
    expect(component.filteredFiles().some(f => f.status === 9)).toBeTrue();
  });

  it('Ouvrir utilise mode view pour Complet', () => {
    expect(component.openMode(files[0])).toBe('view');
    expect(component.openMode(files[1])).toBe('edit');
  });

  it('affiche honoraires avec périodicité', () => {
    expect(component.formatHonoraires(files[0])).toContain('Mensuel');
  });

  it('propose step query param pour Compléter', () => {
    expect(component.completeQueryParams(files[1])).toEqual({ mode: 'edit', step: 4 });
  });

  it('affiche la colonne Prochaine action', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Prochaine action');
    expect(text).toContain('Questionnaire LAB');
  });
});
