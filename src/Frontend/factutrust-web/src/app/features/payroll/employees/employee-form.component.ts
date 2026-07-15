import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ToastService } from '@core/services/toast.service';
import { ErrorMessageService } from '@core/services/error-message.service';
import { EmployeeService, CreateEmployeeRequest, EmployeeDetail, UpdateEmployeeRequest } from '@core/services/employee.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { tunisianPayrollValidators, formatValidationError } from '@core/validators/tunisian-payroll.validators';
import { MARITAL_STATUS_OPTIONS, TUNISIAN_GOVERNORATE_OPTIONS } from '../payroll-options';

function toIsoDate(value: Date | null | undefined): string | undefined {
  if (!value) return undefined;
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function parseIsoDate(value?: string): Date | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

@Component({
  selector: 'app-employee-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    InputTextModule,
    InputNumberModule,
    DropdownModule,
    CalendarModule,
    InputSwitchModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    FormSectionComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems()" />

    <app-page-header
      [title]="isEditMode() ? 'Modifier le salarié' : 'Nouveau salarié'"
      [subtitle]="isEditMode() ? 'Mettez à jour le dossier salarié.' : 'Créez un dossier salarié et ses informations de base.'">
      <app-button variant="outline" icon="pi-times" iconPos="left" routerLink="/payroll/employees">Annuler</app-button>
    </app-page-header>

    @if (loading()) {
      <p>Chargement…</p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <app-form-section title="Identité" icon="pi-id-card" [number]="1">
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="employeeNumber">Matricule <span class="required">*</span></label>
              <input pInputText id="employeeNumber" formControlName="employeeNumber" class="w-full" [class.ng-invalid]="isInvalid('employeeNumber')" />
              @if (isInvalid('employeeNumber')) {
                <div class="field-error">{{ fieldError('employeeNumber') }}</div>
              }
            </div>
            <div class="payroll-form-group">
              <label for="cin">CIN</label>
              <input pInputText id="cin" formControlName="cin" class="w-full" [class.ng-invalid]="isInvalid('cin')" />
              <span class="field-hint">8 chiffres (ex. 12345678)</span>
              @if (isInvalid('cin')) {
                <div class="field-error">{{ fieldError('cin') }}</div>
              }
            </div>
            <div class="payroll-form-group">
              <label for="cnssNumber">N° CNSS</label>
              <input pInputText id="cnssNumber" formControlName="cnssNumber" class="w-full" [class.ng-invalid]="isInvalid('cnssNumber')" />
              <span class="field-hint">10 chiffres sans espaces</span>
              @if (isInvalid('cnssNumber')) {
                <div class="field-error">{{ fieldError('cnssNumber') }}</div>
              }
            </div>
          </div>
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="firstName">Prénom <span class="required">*</span></label>
              <input pInputText id="firstName" formControlName="firstName" class="w-full" [class.ng-invalid]="isInvalid('firstName')" />
              @if (isInvalid('firstName')) {
                <div class="field-error">{{ fieldError('firstName') }}</div>
              }
            </div>
            <div class="payroll-form-group">
              <label for="lastName">Nom <span class="required">*</span></label>
              <input pInputText id="lastName" formControlName="lastName" class="w-full" [class.ng-invalid]="isInvalid('lastName')" />
              @if (isInvalid('lastName')) {
                <div class="field-error">{{ fieldError('lastName') }}</div>
              }
            </div>
          </div>
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="dateOfBirth">Date de naissance</label>
              <p-calendar id="dateOfBirth" formControlName="dateOfBirth" dateFormat="dd/mm/yy" [showIcon]="true" styleClass="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="hireDate">Date d'embauche <span class="required">*</span></label>
              <p-calendar id="hireDate" formControlName="hireDate" dateFormat="dd/mm/yy" [showIcon]="true" styleClass="w-full" />
              @if (isInvalid('hireDate')) {
                <div class="field-error">{{ fieldError('hireDate') }}</div>
              }
            </div>
          </div>
        </app-form-section>

        <app-form-section title="Situation familiale & contact" icon="pi-users" [number]="2">
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="maritalStatus">Situation familiale</label>
              <p-dropdown id="maritalStatus" [options]="maritalStatusOptions" formControlName="maritalStatus" optionLabel="label" optionValue="value" styleClass="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="dependentChildren">Enfants à charge</label>
              <p-inputNumber id="dependentChildren" formControlName="dependentChildren" [min]="0" styleClass="w-full" />
            </div>
            <div class="payroll-form-group switch-group">
              <label for="isHeadOfFamily">Chef de famille</label>
              <p-inputSwitch id="isHeadOfFamily" formControlName="isHeadOfFamily" />
            </div>
          </div>
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="email">Email</label>
              <input pInputText id="email" formControlName="email" class="w-full" [class.ng-invalid]="isInvalid('email')" />
              @if (isInvalid('email')) {
                <div class="field-error">{{ fieldError('email') }}</div>
              }
            </div>
            <div class="payroll-form-group">
              <label for="phone">Téléphone</label>
              <input pInputText id="phone" formControlName="phone" class="w-full" [class.ng-invalid]="isInvalid('phone')" />
              @if (isInvalid('phone')) {
                <div class="field-error">{{ fieldError('phone') }}</div>
              }
            </div>
            <div class="payroll-form-group">
              <label for="rib">RIB</label>
              <input pInputText id="rib" formControlName="rib" class="w-full" [class.ng-invalid]="isInvalid('rib')" />
              <span class="field-hint">20 chiffres (compte bancaire tunisien)</span>
              @if (isInvalid('rib')) {
                <div class="field-error">{{ fieldError('rib') }}</div>
              }
            </div>
          </div>
        </app-form-section>

        <app-form-section title="Adresse" icon="pi-map-marker" [number]="3">
          <div class="payroll-form-group">
            <label for="street">Rue</label>
            <input pInputText id="street" formControlName="street" class="w-full" />
          </div>
          <div class="payroll-form-group">
            <label for="streetLine2">Complément d'adresse</label>
            <input pInputText id="streetLine2" formControlName="streetLine2" class="w-full" />
          </div>
          <div class="payroll-form-row">
            <div class="payroll-form-group">
              <label for="city">Ville</label>
              <input pInputText id="city" formControlName="city" class="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="postalCode">Code postal</label>
              <input pInputText id="postalCode" formControlName="postalCode" class="w-full" />
            </div>
            <div class="payroll-form-group">
              <label for="governorate">Gouvernorat</label>
              <p-dropdown id="governorate" [options]="governorateOptions" formControlName="governorate" optionLabel="label" optionValue="value" [showClear]="true" styleClass="w-full" />
              @if (isInvalid('governorate')) {
                <div class="field-error">{{ fieldError('governorate') }}</div>
              }
            </div>
          </div>
        </app-form-section>

        <div class="form-actions">
          <app-button type="submit" variant="primary" icon="pi-check" iconPos="left" [disabled]="form.invalid || saving()">
            {{ isEditMode() ? 'Enregistrer' : 'Créer le salarié' }}
          </app-button>
        </div>
      </form>
    }
  `,
  styles: [`
    .required { color: var(--color-error-600); }
    .form-actions { margin-top: var(--spacing-6); display: flex; gap: var(--spacing-3); }
    .switch-group { justify-content: flex-end; }
    .w-full { width: 100%; }
  `]
})
export class EmployeeFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly employees = inject(EmployeeService);
  private readonly toast = inject(ToastService);
  readonly errorMessageService = inject(ErrorMessageService);

  readonly maritalStatusOptions = MARITAL_STATUS_OPTIONS;
  readonly governorateOptions = TUNISIAN_GOVERNORATE_OPTIONS;
  loading = signal(false);
  saving = signal(false);
  employeeId = signal<string | null>(null);
  isEditMode = computed(() => !!this.employeeId());

  form: FormGroup = this.fb.group({
    employeeNumber: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    cin: ['', tunisianPayrollValidators.cin],
    cnssNumber: ['', tunisianPayrollValidators.cnss],
    dateOfBirth: [null as Date | null],
    hireDate: [null as Date | null, Validators.required],
    maritalStatus: ['Single'],
    isHeadOfFamily: [false],
    dependentChildren: [0, Validators.min(0)],
    email: ['', Validators.email],
    phone: ['', tunisianPayrollValidators.phone],
    rib: ['', tunisianPayrollValidators.rib],
    street: [''],
    streetLine2: [''],
    city: [''],
    postalCode: [''],
    governorate: ['', tunisianPayrollValidators.governorate]
  });

  breadcrumbItems = computed((): BreadcrumbItem[] => [
    { label: 'Salariés', route: '/payroll/employees' },
    { label: this.isEditMode() ? 'Modifier' : 'Nouveau' }
  ]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.employeeId.set(id);
      this.form.get('employeeNumber')?.disable();
      this.form.get('hireDate')?.disable();
      this.loadEmployee(id);
    } else {
      this.form.patchValue({ hireDate: new Date() });
    }
  }

  isInvalid(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && c?.touched);
  }

  fieldError(field: string): string {
    const c = this.form.get(field);
    return formatValidationError(c?.errors ?? null)
      ?? this.errorMessageService.getErrorMessage(c);
  }

  private loadEmployee(id: string): void {
    this.loading.set(true);
    this.employees.getById(id).subscribe({
      next: res => {
        if (res.data) this.populateForm(res.data);
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Salarié', detail: 'Salarié introuvable.' });
        this.router.navigate(['/payroll/employees']);
        this.loading.set(false);
      }
    });
  }

  private populateForm(e: EmployeeDetail): void {
    this.form.patchValue({
      employeeNumber: e.employeeNumber,
      firstName: e.firstName,
      lastName: e.lastName,
      cin: e.cin ?? '',
      cnssNumber: e.cnssNumber ?? '',
      dateOfBirth: parseIsoDate(e.dateOfBirth),
      hireDate: parseIsoDate(e.hireDate),
      maritalStatus: e.maritalStatus,
      isHeadOfFamily: e.isHeadOfFamily,
      dependentChildren: e.dependentChildren,
      email: e.email ?? '',
      phone: e.phone ?? '',
      rib: e.rib ?? '',
      street: e.address?.street ?? '',
      streetLine2: e.address?.streetLine2 ?? '',
      city: e.address?.city ?? '',
      postalCode: e.address?.postalCode ?? '',
      governorate: e.address?.governorate ?? ''
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const raw = this.form.getRawValue();

    if (this.isEditMode()) {
      const body: UpdateEmployeeRequest = {
        firstName: raw.firstName,
        lastName: raw.lastName,
        cin: raw.cin || undefined,
        cnssNumber: raw.cnssNumber || undefined,
        dateOfBirth: toIsoDate(raw.dateOfBirth),
        maritalStatus: raw.maritalStatus,
        isHeadOfFamily: raw.isHeadOfFamily,
        dependentChildren: raw.dependentChildren ?? 0,
        street: raw.street || undefined,
        streetLine2: raw.streetLine2 || undefined,
        city: raw.city || undefined,
        postalCode: raw.postalCode || undefined,
        governorate: raw.governorate || undefined,
        email: raw.email || undefined,
        phone: raw.phone || undefined,
        rib: raw.rib || undefined
      };
      this.employees.update(this.employeeId()!, body).subscribe({
        next: () => {
          this.toast.add({ severity: 'success', summary: 'Salarié', detail: 'Dossier mis à jour.' });
          this.router.navigate(['/payroll/employees', this.employeeId()]);
          this.saving.set(false);
        },
        error: err => {
          this.toast.add({ severity: 'error', summary: 'Salarié', detail: err.error?.message ?? 'Enregistrement impossible.' });
          this.saving.set(false);
        }
      });
    } else {
      const body: CreateEmployeeRequest = {
        employeeNumber: raw.employeeNumber,
        firstName: raw.firstName,
        lastName: raw.lastName,
        cin: raw.cin || undefined,
        cnssNumber: raw.cnssNumber || undefined,
        dateOfBirth: toIsoDate(raw.dateOfBirth),
        hireDate: toIsoDate(raw.hireDate)!,
        maritalStatus: raw.maritalStatus,
        isHeadOfFamily: raw.isHeadOfFamily,
        dependentChildren: raw.dependentChildren ?? 0,
        street: raw.street || undefined,
        streetLine2: raw.streetLine2 || undefined,
        city: raw.city || undefined,
        postalCode: raw.postalCode || undefined,
        governorate: raw.governorate || undefined,
        email: raw.email || undefined,
        phone: raw.phone || undefined,
        rib: raw.rib || undefined
      };
      this.employees.create(body).subscribe({
        next: res => {
          if (res.success && res.data) {
            this.toast.add({ severity: 'success', summary: 'Salarié', detail: 'Salarié créé.' });
            this.router.navigate(['/payroll/employees', res.data]);
          }
          this.saving.set(false);
        },
        error: err => {
          this.toast.add({ severity: 'error', summary: 'Salarié', detail: err.error?.message ?? 'Création impossible.' });
          this.saving.set(false);
        }
      });
    }
  }
}
