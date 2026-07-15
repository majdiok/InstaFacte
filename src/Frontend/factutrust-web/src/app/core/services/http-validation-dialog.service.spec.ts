import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { HttpValidationDialogService } from './http-validation-dialog.service';
import { ConfirmationService } from './confirmation.service';
import { ErrorHandlerService } from './error-handler.service';

describe('HttpValidationDialogService', () => {
  let service: HttpValidationDialogService;
  let confirmation: jasmine.SpyObj<ConfirmationService>;

  beforeEach(() => {
    confirmation = jasmine.createSpyObj('ConfirmationService', ['alert']);

    TestBed.configureTestingModule({
      providers: [
        HttpValidationDialogService,
        ErrorHandlerService,
        { provide: ConfirmationService, useValue: confirmation }
      ]
    });

    service = TestBed.inject(HttpValidationDialogService);
  });

  it('should show alert with formatted message and md scrollable modal', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        code: 'VALIDATION_FAILED',
        fieldErrors: { identificationNumber: [{ message: 'CIN invalide' }] }
      }
    });

    service.showValidationFailed(error, 'POST|/api/x|msg');

    expect(confirmation.alert).toHaveBeenCalledWith(
      jasmine.objectContaining({
        header: 'Erreur de validation',
        message: jasmine.stringContaining('CIN invalide'),
        size: 'md',
        scrollable: true,
        icon: 'pi pi-exclamation-circle'
      })
    );
  });

  it('should dedupe identical calls within window', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { code: 'VALIDATION_FAILED', fieldErrors: { a: [{ message: 'e' }] } }
    });

    const key = 'POST|/api/same|same';
    service.showValidationFailed(error, key);
    service.showValidationFailed(error, key);

    expect(confirmation.alert).toHaveBeenCalledTimes(1);
  });
});
