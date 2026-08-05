import { ComponentFixture, TestBed } from '@angular/core/testing';

import { provideHttpClient } from '@angular/common/http';

import { provideHttpClientTesting } from '@angular/common/http/testing';

import { PayrollMealVoucherGridComponent } from './payroll-meal-voucher-grid.component';

import { PayrollService } from '@core/services/payroll.service';

import { EmployeeService } from '@core/services/employee.service';

import { ToastService } from '@core/services/toast.service';

import { ConfirmationService } from '@core/services/confirmation.service';

import { of } from 'rxjs';



describe('PayrollMealVoucherGridComponent', () => {

  let fixture: ComponentFixture<PayrollMealVoucherGridComponent>;



  beforeEach(() => {

    TestBed.configureTestingModule({

      imports: [PayrollMealVoucherGridComponent],

      providers: [

        provideHttpClient(),

        provideHttpClientTesting(),

        {

          provide: PayrollService,

          useValue: {

            listMealVouchers: () => of({ success: true, data: [] })

          }

        },

        {

          provide: EmployeeService,

          useValue: { list: () => of({ success: true, data: { items: [] } }) }

        },

        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },

        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }

      ]

    });

    fixture = TestBed.createComponent(PayrollMealVoucherGridComponent);

    fixture.componentInstance.year = 2026;

    fixture.componentInstance.month = 8;

    fixture.componentInstance.readOnly = true;

    fixture.detectChanges();

  });



  it('hides add button in readOnly mode', () => {

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter une ligne');

  });



  it('shows section title for meal vouchers', () => {

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Tickets restaurant');

  });

});


