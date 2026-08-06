import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { FirmCollaboratorFormComponent } from './firm-collaborator-form.component';
import { FirmCollaboratorsService, FirmUser } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';

describe('FirmCollaboratorFormComponent', () => {
  const firmUser: FirmUser = {
    id: 'user-1',
    email: 'collab@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    role: 12,
    roleDisplay: 'Comptable cabinet',
    isActive: true,
    emailConfirmed: true,
    civility: 1,
    qualification: 'Expert',
    phoneNumber: '+21620000000',
    phoneLandline: null,
    useFirmAddress: true,
    addressLine: '1 rue Test',
    postalCode: '1000',
    city: 'Tunis',
    country: 'Tunisie',
    hasCni: false,
    binomes: []
  };

  function setup(options: {
    data?: Record<string, unknown>;
    id?: string | null;
    url?: string;
    createResult?: ReturnType<typeof of> | ReturnType<typeof throwError>;
  }): ComponentFixture<FirmCollaboratorFormComponent> {
    const id = options.id ?? null;
    const url = options.url ?? (id ? `/firm/collaborateurs/${id}` : '/firm/collaborateurs/new');

    TestBed.configureTestingModule({
      imports: [FirmCollaboratorFormComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        ErrorHandlerService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: options.data ?? {},
              paramMap: convertToParamMap(id ? { id } : {})
            }
          }
        },
        {
          provide: Router,
          useValue: {
            url,
            navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true))
          }
        },
        {
          provide: FirmCollaboratorsService,
          useValue: {
            getFirmAddress: jasmine.createSpy('getFirmAddress').and.returnValue(
              of({
                addressLine: 'Cabinet Ave',
                postalCode: '1001',
                city: 'Tunis',
                country: 'Tunisie'
              })
            ),
            getById: jasmine.createSpy('getById').and.returnValue(of(firmUser)),
            list: jasmine.createSpy('list').and.returnValue(of([firmUser])),
            create: jasmine.createSpy('create').and.returnValue(
              options.createResult ?? of(firmUser)
            ),
            update: jasmine.createSpy('update')
          }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } }
      ]
    });

    const fixture = TestBed.createComponent(FirmCollaboratorFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('enters create mode when route data mode is create', () => {
    const fixture = setup({ data: { mode: 'create' } });
    const component = fixture.componentInstance;

    expect(component.mode).toBe('create');
    expect(component.readOnly).toBeFalse();
    expect(component.title).toBe("Création d'un nouveau collaborateur");
    expect(component.form.disabled).toBeFalse();
  });

  it('enters create mode when id is absent (regression /new without data)', () => {
    const fixture = setup({
      data: {},
      id: null,
      url: '/firm/collaborateurs/new'
    });
    const component = fixture.componentInstance;

    expect(component.mode).toBe('create');
    expect(component.readOnly).toBeFalse();
    expect(component.title).toBe("Création d'un nouveau collaborateur");
  });

  it('enters edit mode on /collaborateurs/:id/edit', () => {
    const fixture = setup({
      id: 'user-1',
      url: '/firm/collaborateurs/user-1/edit'
    });
    const component = fixture.componentInstance;

    expect(component.mode).toBe('edit');
    expect(component.readOnly).toBeFalse();
    expect(component.title).toBe('Modification du collaborateur');
    expect(component.form.disabled).toBeFalse();
  });

  it('enters view mode on /collaborateurs/:id', () => {
    const fixture = setup({
      id: 'user-1',
      url: '/firm/collaborateurs/user-1'
    });
    const component = fixture.componentInstance;

    expect(component.mode).toBe('view');
    expect(component.readOnly).toBeTrue();
    expect(component.title).toBe('Consultation du collaborateur');
    expect(component.form.disabled).toBeTrue();
  });

  it('shows business duplicate-email message on create failure (not Http failure response)', () => {
    const businessMessage =
      "Le nom d'utilisateur et l'adresse email 'karim45@yahoo.com' sont déjà utilisés.";
    const httpError = new HttpErrorResponse({
      status: 400,
      statusText: 'Bad Request',
      url: 'https://localhost:7001/api/firm/users',
      error: {
        success: false,
        data: null,
        message: null,
        error: businessMessage
      }
    });

    const fixture = setup({
      data: { mode: 'create' },
      createResult: throwError(() => httpError)
    });
    const component = fixture.componentInstance;
    const toast = TestBed.inject(ToastService);

    component.form.patchValue({
      lastName: 'karim',
      firstName: 'Ferchiou',
      email: 'karim45@yahoo.com'
    });
    component.save();

    expect(toast.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        summary: 'Erreur',
        detail: businessMessage
      })
    );
    expect(toast.add).not.toHaveBeenCalledWith(
      jasmine.objectContaining({
        detail: jasmine.stringMatching(/Http failure response/i)
      })
    );
    expect(component.form.controls.email.hasError('server')).toBeTrue();
    expect(component.form.controls.email.getError('server')).toBe(businessMessage);
  });
});
