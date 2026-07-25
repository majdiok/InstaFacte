import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RouterTestingModule } from '@angular/router/testing';

import { ActivatedRoute } from '@angular/router';

import { of, throwError } from 'rxjs';

import { FirmPermanentFileWizardComponent } from './firm-permanent-file-wizard.component';

import { FirmGovernanceService } from '@core/services/firm-governance.service';

import { FirmAssignmentService } from '@core/services/firm-assignment.service';

import { ToastService } from '@core/services/toast.service';

import { ConfirmationService } from '@core/services/confirmation.service';

import { HttpClient } from '@angular/common/http';

import { NoopAnimationsModule } from '@angular/platform-browser/animations';



const baseFile = {

  id: 'pf1',

  firmClientAssignmentId: 'a1',

  companyTenantId: 't1',

  companyName: 'Ste X',

  status: 1,

  statusDisplay: 'En cours',

  wizardStep: 1,

  hasTaxCertificate: false,

  isDigitized: false,

  missionResigned: false,

  labCompleted: false,

  missionAccepted: false,

  currency: 'TND',

  representatives: [{ id: 'r1', lastName: 'Hadad', firstName: 'Ali', role: 'Gérant' }] as unknown[],

  shareholders: [] as unknown[]

};



describe('FirmPermanentFileWizardComponent', () => {

  let fixture: ComponentFixture<FirmPermanentFileWizardComponent>;

  let component: FirmPermanentFileWizardComponent;

  let upsertSpy: jasmine.Spy;

  let queryParams$: { subscribe: (fn: (v: Map<string, string | null>) => void) => { unsubscribe: () => void } };

  let queryParamMap: Map<string, string | null>;



  beforeEach(async () => {

    queryParamMap = new Map([['mode', null]]);

    queryParams$ = {

      subscribe: fn => {

        fn(queryParamMap);

        return { unsubscribe: () => undefined };

      }

    };

    upsertSpy = jasmine.createSpy('upsertPermanentFile').and.returnValue(of({

      success: true,

      data: { ...baseFile, wizardStep: 5, annualFeeAmount: 1200, billingFrequency: 2 }

    }));



    await TestBed.configureTestingModule({

      imports: [FirmPermanentFileWizardComponent, RouterTestingModule, NoopAnimationsModule],

      providers: [

        {

          provide: ActivatedRoute,

          useValue: {

            snapshot: {

              paramMap: { get: (k: string) => k === 'assignmentId' ? 'a1' : null },

              queryParamMap: { get: (k: string) => queryParamMap.get(k) ?? null }

            },

            queryParamMap: queryParams$

          }

        },

        {

          provide: FirmGovernanceService,

          useValue: {

            getPermanentFile: () => of({ success: true, data: { ...baseFile } }),

            upsertPermanentFile: upsertSpy,

            syncPermanentFile: () => of({ success: true }),

            archivePermanentFile: () => of({ success: true }),

            addRepresentative: () => of({ success: true, data: {} }),

            updateRepresentative: () => of({ success: true, data: {} }),

            addShareholder: () => of({ success: true, data: {} }),

            updateShareholder: () => of({ success: true, data: {} }),

            deactivateRepresentative: () => of({ success: true }),

            deactivateShareholder: () => of({ success: true })

          }

        },

        { provide: FirmAssignmentService, useValue: { getActiveClients: () => of({ success: true, data: [] }) } },

        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },

        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },

        { provide: HttpClient, useValue: { get: () => of({ success: true, data: [] }) } }

      ]

    }).compileComponents();



    fixture = TestBed.createComponent(FirmPermanentFileWizardComponent);

    component = fixture.componentInstance;

    fixture.detectChanges();

  });



  it('affiche les libellés du stepper', () => {

    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Identité');

    expect(text).toContain('Dirigeants');

    expect(text).toContain('Confirmation');

  });



  it('bloque Suivant si NIF invalide', () => {

    component.form.patchValue({ companyName: 'Ste X', nif: 'BAD', legalForm: 0 });

    component.next();

    expect(upsertSpy).not.toHaveBeenCalled();

  });



  it('affiche le récapitulatif à l’étape 6 avec honoraires', () => {

    component.form.patchValue({ annualFeeAmount: 900, billingFrequency: 2, currency: 'TND' });

    component.step.set(6);

    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Récapitulatif');

    expect(text).toContain('Honoraires');

    expect(text).toContain('Annuel');

  });



  it('enregistre les honoraires à l’étape 5', () => {

    component.step.set(5);

    component.form.patchValue({

      annualFeeAmount: 1200,

      billingFrequency: 2,

      currency: 'TND',

      billingNotes: 'Forfait'

    });

    component.save(false);

    expect(upsertSpy).toHaveBeenCalled();

    const body = upsertSpy.calls.mostRecent().args[1] as Record<string, unknown>;

    expect(body['wizardStep']).toBe(5);

    expect(body['annualFeeAmount']).toBe(1200);

    expect(body['billingFrequency']).toBe(2);

  });



  it('mode view n’affiche pas les inputs éditables', () => {

    component.modeOverride.set('view');

    component.file.set({

      ...baseFile,

      status: 2,

      statusDisplay: 'Complet',

      annualFeeAmount: 500,

      billingFrequencyDisplay: 'Mensuel'

    } as never);

    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Consultation dossier permanent');

    expect(text).toContain('Modifier le dossier');

    expect(fixture.nativeElement.querySelector('form')).toBeNull();

  });



  it('enterEditMode restaure wizardStep pour un dossier Complet', () => {

    component.file.set({ ...baseFile, status: 2, wizardStep: 6 } as never);

    component.modeOverride.set('view');

    component.enterEditMode();

    expect(component.step()).toBe(6);

    expect(component.modeOverride()).toBe('edit');

  });



  it('formatAddress affiche un libellé explicite si siège vide', () => {

    expect(component.formatAddress({ ...baseFile } as never)).toBe('Siège non renseigné');

    expect(component.formatAddress({ ...baseFile, street: '12 rue X', city: 'Tunis' } as never)).toContain('Tunis');

  });



  it('finalize envoie requestCompletion', () => {

    component.step.set(6);

    component.form.patchValue({

      companyName: 'Ste X',

      nif: '1234567/A/B/C/000',

      legalForm: 0,

      labCompleted: true,

      missionAccepted: true

    });

    upsertSpy.and.returnValue(of({ success: true, data: { ...baseFile, status: 2, wizardStep: 6 } }));

    component.finalize();

    const body = upsertSpy.calls.mostRecent().args[1] as Record<string, unknown>;

    expect(body['requestCompletion']).toBeTrue();

    expect(body['wizardStep']).toBe(6);

  });

  it('mode view : erreur API affiche Consultation impossible', async () => {
    queryParamMap.set('mode', 'view');
    await TestBed.resetTestingModule();
    upsertSpy = jasmine.createSpy('upsertPermanentFile').and.returnValue(of({ success: true, data: baseFile }));
    await TestBed.configureTestingModule({
      imports: [FirmPermanentFileWizardComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: (k: string) => k === 'assignmentId' ? 'a1' : null },
              queryParamMap: { get: (k: string) => queryParamMap.get(k) ?? null }
            },
            queryParamMap: queryParams$
          }
        },
        {
          provide: FirmGovernanceService,
          useValue: {
            getPermanentFile: () => throwError(() => ({ error: { message: 'Erreur serveur' } })),
            upsertPermanentFile: upsertSpy,
            syncPermanentFile: () => of({ success: true }),
            archivePermanentFile: () => of({ success: true }),
            addRepresentative: () => of({ success: true, data: {} }),
            updateRepresentative: () => of({ success: true, data: {} }),
            addShareholder: () => of({ success: true, data: {} }),
            updateShareholder: () => of({ success: true, data: {} }),
            deactivateRepresentative: () => of({ success: true }),
            deactivateShareholder: () => of({ success: true })
          }
        },
        { provide: FirmAssignmentService, useValue: { getActiveClients: () => of({ success: true, data: [] }) } },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },
        { provide: HttpClient, useValue: { get: () => of({ success: true, data: [] }) } }
      ]
    }).compileComponents();
    const f = TestBed.createComponent(FirmPermanentFileWizardComponent);
    f.detectChanges();
    expect(f.componentInstance.loadError()).toContain('Erreur serveur');
    expect(f.nativeElement.textContent).toContain('Consultation impossible');
  });

});

