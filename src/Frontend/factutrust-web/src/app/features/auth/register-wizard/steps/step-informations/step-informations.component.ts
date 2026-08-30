import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { InputMaskModule } from 'primeng/inputmask';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ErrorMessageService } from '@core/services/error-message.service';
import {
  passwordCriteria as computePasswordCriteria,
  passwordMatches as checkPasswordMatches,
  passwordMismatch as checkPasswordMismatch,
  passwordStrengthLabel as getPasswordStrengthLabel,
  passwordStrengthLevel as computePasswordStrengthLevel,
  passwordStrengthMetCount
} from '../../../shared/auth-password.helpers';
import { TaxRegime } from '../../tax-regime.types';

@Component({
  selector: 'app-step-informations',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    InputTextModule,
    PasswordModule,
    SelectModule,
    InputMaskModule,
    InputSwitchModule
  ],
  animations: [
    trigger('fadeInOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('300ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, transform: 'translateY(-10px)' }))
      ])
    ])
  ],
  templateUrl: './step-informations.component.html',
  styleUrl: './step-informations.component.scss'
})
export class StepInformationsComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() taxRegimes: TaxRegime[] = [];
  /** Emitted so the container can apply the shared NIF cleanup helper. */
  @Output() nifBlur = new EventEmitter<void>();

  errorMessageService = inject(ErrorMessageService);

  showPartnerCode = false;

  readonly strengthSegments: readonly number[] = [1, 2, 3, 4, 5];

  togglePartnerCode(): void {
    this.showPartnerCode = !this.showPartnerCode;
  }

  isInvalid(field: string): boolean {
    const control = this.form.get(field);
    return !!(control?.invalid && control?.touched);
  }

  passwordMismatch(): boolean {
    return checkPasswordMismatch(this.form.get('password')?.value, this.form.get('confirmPassword')?.value);
  }

  passwordMatches(): boolean {
    return checkPasswordMatches(this.form.get('password')?.value, this.form.get('confirmPassword')?.value);
  }

  passwordCriteria() {
    return computePasswordCriteria(this.form.get('password')?.value);
  }

  passwordStrengthMetCount(): number {
    return passwordStrengthMetCount(this.passwordCriteria());
  }

  passwordStrengthLevel() {
    return computePasswordStrengthLevel(this.form.get('password')?.value, this.passwordCriteria());
  }

  passwordStrengthLabel(): string {
    return getPasswordStrengthLabel(this.passwordStrengthLevel());
  }

  passwordFieldAriaDescribedBy(): string {
    const ids: string[] = ['wiz-password-strength-hint'];
    if (this.isInvalid('password')) {
      ids.unshift('wiz-password-error');
    }
    return ids.join(' ');
  }

  confirmPasswordAriaDescribedBy(): string | null {
    if (this.isInvalid('confirmPassword') || this.passwordMismatch()) {
      return 'wiz-confirmPassword-error';
    }
    if (this.passwordMatches() && this.form.get('confirmPassword')?.value) {
      return 'wiz-confirmPassword-success';
    }
    return null;
  }

  onNifBlur(): void {
    this.nifBlur.emit();
  }
}
