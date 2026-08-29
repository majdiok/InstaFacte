import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AccountingService, NctLiasseExportOptions } from '../services/accounting.service';
import { FinancialStatementsExportDialogComponent } from './financial-statements-export-dialog.component';

describe('FinancialStatementsExportDialogComponent — buildOptions (BUG #005)', () => {
  let fixture: ComponentFixture<FinancialStatementsExportDialogComponent>;
  let accountingMock: { exportNctStatementsPdfWithOptions: jasmine.Spy };

  beforeEach(async () => {
    accountingMock = {
      exportNctStatementsPdfWithOptions: jasmine
        .createSpy('exportNctStatementsPdfWithOptions')
        .and.returnValue(of(new Blob()))
    };

    await TestBed.configureTestingModule({
      imports: [FinancialStatementsExportDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: AccountingService, useValue: accountingMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FinancialStatementsExportDialogComponent);
    fixture.componentInstance.fiscalYear = 2025;
    fixture.componentInstance.visible = true;
    fixture.componentInstance.ngOnChanges();
    fixture.detectChanges();
  });

  it("replie sur le 31/12 de l'exercice quand asOfDate est une Date invalide, sans jamais produire NaN", () => {
    fixture.componentInstance.asOfDate = new Date('not a date');
    fixture.componentInstance.preview();

    expect(accountingMock.exportNctStatementsPdfWithOptions).toHaveBeenCalled();
    const options: NctLiasseExportOptions = accountingMock.exportNctStatementsPdfWithOptions.calls.mostRecent().args[0];
    expect(options.asOfDate).toBe('2025-12-31');
    expect(options.asOfDate).not.toContain('NaN');
    expect(fixture.componentInstance.localError()).toBeTruthy();
  });

  it("replie sur le 31/12 de l'exercice quand asOfDate est hors bornes [2000;2100]", () => {
    fixture.componentInstance.asOfDate = new Date(2500, 0, 1);
    fixture.componentInstance.preview();

    const options: NctLiasseExportOptions = accountingMock.exportNctStatementsPdfWithOptions.calls.mostRecent().args[0];
    expect(options.asOfDate).toBe('2025-12-31');
    expect(fixture.componentInstance.localError()).toBeTruthy();
  });

  it('utilise la date saisie quand elle est valide, sans message de repli', () => {
    fixture.componentInstance.asOfDate = new Date(2025, 5, 15);
    fixture.componentInstance.preview();

    const options: NctLiasseExportOptions = accountingMock.exportNctStatementsPdfWithOptions.calls.mostRecent().args[0];
    expect(options.asOfDate).toBe('2025-06-15');
    expect(fixture.componentInstance.localError()).toBeNull();
  });
});
