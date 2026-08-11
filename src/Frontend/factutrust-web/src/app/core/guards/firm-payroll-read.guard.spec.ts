import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { firmPayrollReadGuard } from './firm-payroll-read.guard';
import { AuthService } from '@core/services/auth.service';

describe('firmPayrollReadGuard', () => {
  function setup(role: 'FirmManager' | 'FirmAccountant' | 'Admin') {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isFirmManager: signal(role === 'FirmManager'),
            isFirmAccountant: signal(role === 'FirmAccountant')
          }
        }
      ]
    });
  }

  function run(): boolean | UrlTree {
    return TestBed.runInInjectionContext(() =>
      firmPayrollReadGuard({} as never, { url: '/firm/payroll' } as never)
    ) as boolean | UrlTree;
  }

  it('laisse passer le responsable cabinet', () => {
    setup('FirmManager');
    expect(run()).toBeTrue();
  });

  it('laisse passer le comptable cabinet, qui possède payroll:read', () => {
    // C'est le défaut corrigé : la permission existait côté serveur, mais le garde responsable
    // posé sur la racine renvoyait le comptable vers /access-denied.
    setup('FirmAccountant');
    expect(run()).toBeTrue();
  });

  it('refuse les autres rôles en les renvoyant vers /access-denied', () => {
    setup('Admin');
    const result = run();
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toContain('/access-denied');
  });
});
