import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { trigger, transition, style, animate } from '@angular/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputMaskModule } from 'primeng/inputmask';
import { StepsModule } from 'primeng/steps';
import { MessageModule } from 'primeng/message';
import { DividerModule } from 'primeng/divider';
import { CheckboxModule } from 'primeng/checkbox';
import { InputSwitchModule } from 'primeng/inputswitch';
import { MenuItem } from '@shared/models/menu-item.model';
import { AuthService, RegisterRequest } from '@core/services/auth.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ErrorMessageService } from '@core/services/error-message.service';
import { LogoComponent } from '@shared/components/logo/logo.component';

interface TaxRegime {
  label: string;
  value: number;
}

interface Governorate {
  label: string;
  value: string;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    DropdownModule,
    InputMaskModule,
    StepsModule,
    MessageModule,
    DividerModule,
    CheckboxModule,
    InputSwitchModule,
    LogoComponent
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
    ]),
    trigger('stepSlide', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateX(16px)' }),
        animate('320ms cubic-bezier(0.16, 1, 0.3, 1)', style({ opacity: 1, transform: 'translateX(0)' }))
      ]),
      transition(':leave', [
        animate('220ms ease-in', style({ opacity: 0, transform: 'translateX(-12px)' }))
      ])
    ])
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private warehouseContext = inject(WarehouseContextService);
  private router = inject(Router);
  private errorHandler = inject(ErrorHandlerService);
  errorMessageService = inject(ErrorMessageService);

  loading = signal(false);
  error = signal<string | null>(null);
  currentStep = signal(0);

  /** Libellé ARIA pour le formulaire multi-étapes (progression + contexte). */
  registrationFormAriaLabel = computed(() => {
    const stepNames = ['Compte', 'Entreprise', 'Adresse'];
    const cur = this.currentStep();
    const prev = cur > 0 ? `Étapes précédentes complétées. ` : '';
    return `${prev}Étape ${cur + 1} sur 3 : ${stepNames[cur]}.`;
  });

  steps: MenuItem[] = [
    { label: 'Compte' },
    { label: 'Entreprise' },
    { label: 'Adresse' }
  ];

  /** Segments de la barre de force (5 critères). */
  readonly strengthSegments: readonly number[] = [1, 2, 3, 4, 5];

  taxRegimes: TaxRegime[] = [
    { label: 'Régime réel', value: 0 },
    { label: 'Régime forfaitaire', value: 1 },
    { label: 'Régime simplifié', value: 2 }
  ];

  governorates: Governorate[] = [
    { label: 'Ariana', value: 'Ariana' },
    { label: 'Béja', value: 'Béja' },
    { label: 'Ben Arous', value: 'Ben Arous' },
    { label: 'Bizerte', value: 'Bizerte' },
    { label: 'Gabès', value: 'Gabès' },
    { label: 'Gafsa', value: 'Gafsa' },
    { label: 'Jendouba', value: 'Jendouba' },
    { label: 'Kairouan', value: 'Kairouan' },
    { label: 'Kasserine', value: 'Kasserine' },
    { label: 'Kébili', value: 'Kébili' },
    { label: 'Kef', value: 'Kef' },
    { label: 'Mahdia', value: 'Mahdia' },
    { label: 'Manouba', value: 'Manouba' },
    { label: 'Médenine', value: 'Médenine' },
    { label: 'Monastir', value: 'Monastir' },
    { label: 'Nabeul', value: 'Nabeul' },
    { label: 'Sfax', value: 'Sfax' },
    { label: 'Sidi Bouzid', value: 'Sidi Bouzid' },
    { label: 'Siliana', value: 'Siliana' },
    { label: 'Sousse', value: 'Sousse' },
    { label: 'Tataouine', value: 'Tataouine' },
    { label: 'Tozeur', value: 'Tozeur' },
    { label: 'Tunis', value: 'Tunis' },
    { label: 'Zaghouan', value: 'Zaghouan' }
  ];

  showPartnerCode = signal(false);

  form: FormGroup = this.fb.group({
    // Step 1: Account
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(12),
      // Backend requires: lowercase, uppercase, digit, and special character
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/)
    ]],
    confirmPassword: ['', Validators.required],
    // Optional; not sent to API until backend supports it (TODO: add partnerCode to RegisterRequest when API is ready)
    partnerCode: [''],
    acceptTerms: [false, Validators.requiredTrue],
    // Step 2: Company
    companyName: ['', [Validators.required, Validators.minLength(2)]],
    nif: ['', Validators.required],
    taxRegime: [null, Validators.required],
    companyEmail: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    website: [''],
    warehouseName: [''],  // Default to empty, let placeholder show default
    // Step 3: Address
    street: ['', Validators.required],
    streetLine2: [''],
    city: ['', Validators.required],
    postalCode: [''],
    governorate: ['', Validators.required]
  });

  /** Vérifie si les mots de passe ne correspondent pas. Méthode (pas computed) pour lire les valeurs à jour à chaque appel. */
  passwordMismatch(): boolean {
    const password = this.form.get('password')?.value;
    const confirm = this.form.get('confirmPassword')?.value;
    return !!(confirm && password !== confirm);
  }

  /** True si les deux champs mot de passe sont remplis et identiques. */
  passwordMatches(): boolean {
    const password = this.form.get('password')?.value;
    const confirm = this.form.get('confirmPassword')?.value;
    return !!(password && confirm && password === confirm);
  }

  /** Critères du mot de passe pour affichage progressif (informativement, la validation reste sur le FormControl). */
  passwordCriteria(): { minLength: boolean; hasUpper: boolean; hasLower: boolean; hasDigit: boolean; hasSpecial: boolean } {
    const p = (this.form.get('password')?.value ?? '') as string;
    return {
      minLength: p.length >= 12,
      hasUpper: /[A-Z]/.test(p),
      hasLower: /[a-z]/.test(p),
      hasDigit: /\d/.test(p),
      hasSpecial: /[^a-zA-Z0-9]/.test(p)
    };
  }

  /** Nombre de critères remplis (0–5), pour la barre de force. */
  passwordStrengthMetCount(): number {
    const c = this.passwordCriteria();
    return [c.minLength, c.hasUpper, c.hasLower, c.hasDigit, c.hasSpecial].filter(Boolean).length;
  }

  /**
   * Niveau de force pour styles (barre + libellé accessibilité).
   * 4 segments affichés : mapping 0–5 critères sur weak / fair / good / strong.
   */
  passwordStrengthLevel(): 'empty' | 'weak' | 'fair' | 'good' | 'strong' {
    const n = this.passwordStrengthMetCount();
    const p = (this.form.get('password')?.value ?? '') as string;
    if (!p) {
      return 'empty';
    }
    if (n <= 2) {
      return 'weak';
    }
    if (n === 3) {
      return 'fair';
    }
    if (n === 4) {
      return 'good';
    }
    return 'strong';
  }

  /** Libellé court pour aria-live sur la force du mot de passe. */
  passwordStrengthLabel(): string {
    switch (this.passwordStrengthLevel()) {
      case 'empty':
        return '';
      case 'weak':
        return 'Force du mot de passe : faible';
      case 'fair':
        return 'Force du mot de passe : moyenne';
      case 'good':
        return 'Force du mot de passe : bonne';
      case 'strong':
        return 'Force du mot de passe : forte';
      default:
        return '';
    }
  }

  /** Indice d’étape affiché (1–3) pour le libellé « Étape X sur 3 ». */
  stepHumanIndex(): number {
    return this.currentStep() + 1;
  }

  /** IDs pour aria-describedby du champ mot de passe (erreur + aide). */
  passwordFieldAriaDescribedBy(): string {
    const ids: string[] = ['password-strength-hint'];
    if (this.isInvalid('password')) {
      ids.unshift('password-error');
    }
    return ids.join(' ');
  }

  /** IDs pour aria-describedby du champ confirmation. */
  confirmPasswordAriaDescribedBy(): string | null {
    if (this.isInvalid('confirmPassword') || this.passwordMismatch()) {
      return 'confirmPassword-error';
    }
    if (this.passwordMatches() && this.form.get('confirmPassword')?.value) {
      return 'confirmPassword-success';
    }
    return null;
  }

  ngOnInit(): void {
    // Watch for password changes to update validation
    this.form.get('password')?.valueChanges.subscribe(() => {
      this.form.get('confirmPassword')?.updateValueAndValidity();
    });
  }

  isInvalid(field: string): boolean {
    const control = this.form.get(field);
    return !!(control?.invalid && control?.touched);
  }

  isCurrentStepValid(): boolean {
    const step = this.currentStep();

    if (step === 0) {
      const fields = ['firstName', 'lastName', 'email', 'password', 'confirmPassword', 'acceptTerms'];
      return fields.every(f => this.form.get(f)?.valid) && !this.passwordMismatch();
    }

    if (step === 1) {
      const fields = ['companyName', 'nif', 'taxRegime', 'companyEmail', 'phone'];
      return fields.every(f => this.form.get(f)?.valid);
    }

    if (step === 2) {
      const fields = ['street', 'city', 'governorate'];
      return fields.every(f => this.form.get(f)?.valid);
    }

    return false;
  }

  nextStep(): void {
    if (this.isCurrentStepValid() && this.currentStep() < 2) {
      this.currentStep.update(s => s + 1);
    }
  }

  previousStep(): void {
    if (this.currentStep() > 0) {
      this.currentStep.update(s => s - 1);
    }
  }

  onSubmit(): void {
    // Prevent submission if form is invalid
    if (this.form.invalid) {
      // Mark all fields as touched to show validation errors
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      // Scroll to first invalid field
      this.scrollToFirstInvalidField();
      return;
    }

    // Check acceptTerms (required for step 1, not sent to backend)
    if (!this.form.get('acceptTerms')?.value) {
      this.form.get('acceptTerms')?.markAsTouched();
      this.scrollToFirstInvalidField();
      return;
    }

    // Check password mismatch
    if (this.passwordMismatch()) {
      this.error.set('Les mots de passe ne correspondent pas');
      this.form.get('confirmPassword')?.markAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    // Nettoyer le NIF une fois et réutiliser partout (évite que l'InputMask écrase notre valeur)
    const nifControl = this.form.get('nif');
    const rawNif = nifControl?.value ?? '';
    const cleanedNif = this.cleanNifValue(rawNif);
    if (nifControl && cleanedNif !== rawNif) {
      nifControl.setValue(cleanedNif, { emitEvent: false });
      nifControl.updateValueAndValidity({ emitEvent: false });
    }


    // Use getRawValue() to ensure we capture all fields, even if disabled
    const formValue = this.form.getRawValue();

    const getValue = (value: any, defaultValue: any = '') => {
      if (value === null || value === undefined) return defaultValue;
      if (typeof value === 'object' && 'value' in value) return value.value;
      return value;
    };

    const validationErrors = this.validateFormData(formValue, cleanedNif);
    if (validationErrors.length > 0) {
      this.error.set(validationErrors.join(', '));
      this.loading.set(false);
      return;
    }

    // Transform data to match backend expectations
    const request: RegisterRequest = {
      email: (formValue.email || '').trim(),
      password: formValue.password || '',
      confirmPassword: formValue.confirmPassword || '',
      firstName: (formValue.firstName || '').trim(),
      lastName: (formValue.lastName || '').trim(),
      companyName: (formValue.companyName || '').trim(),
      // Remove spaces from phone number (backend expects 8 digits without spaces)
      phone: (formValue.phone?.replace(/\s/g, '') || '').trim(),
      // Clean NIF: remove mask placeholders, spaces, and convert to uppercase
      nif: cleanedNif,
      // Ensure taxRegime is a number (dropdown with optionValue returns the value directly)
      taxRegime: typeof formValue.taxRegime === 'number'
        ? formValue.taxRegime
        : (getValue(formValue.taxRegime, null) ?? 0),
      companyEmail: (formValue.companyEmail || '').trim(),
      website: formValue.website?.trim() || undefined,
      street: (formValue.street || '').trim(),
      streetLine2: formValue.streetLine2?.trim() || undefined,
      city: (formValue.city || '').trim(),
      postalCode: formValue.postalCode?.trim() || undefined,
      // Ensure governorate is a string (dropdown with optionValue returns the value directly)
      governorate: typeof formValue.governorate === 'string'
        ? formValue.governorate.trim()
        : (getValue(formValue.governorate, '') || '').trim(),
      // Optional: warehouse name for stock configuration
      warehouseName: formValue.warehouseName?.trim() || undefined
    };

    // Log request for debugging (remove sensitive data in production)
    console.log('[RegisterComponent] Submitting registration request:', {
      ...request,
      password: '***',
      confirmPassword: '***'
    });

    this.authService.register(request).subscribe({
      next: (response) => {
        if (response.success) {
          console.log('[RegisterComponent] Registration successful');
          this.warehouseContext.navigateAfterSuccessfulAuth('/dashboard');
        } else {
          // Backend returned success: false in response body
          const errorMessage = response.errors?.length > 0
            ? response.errors.join(', ')
            : response.message || 'Une erreur est survenue lors de l\'inscription';
          this.error.set(errorMessage);
          this.errorHandler.logError('Registration failed (success: false)', { response });
        }
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse | any) => {
        // Use centralized error handler for consistent error extraction
        let errorMessage = this.errorHandler.extractErrorMessage(err);

        // Fallback if error message is still undefined or empty
        if (!errorMessage || errorMessage === 'undefined' || errorMessage.trim() === '' || errorMessage.includes('undefined')) {
          // Check for network errors
          if (!err || !err.status || err.status === 0 || err.status === undefined) {
            errorMessage = 'Impossible de se connecter au serveur. Vérifiez que le backend est démarré sur https://localhost:7001.';
          } else {
            errorMessage = `Une erreur est survenue lors de l'inscription (${err.status || 'erreur inconnue'}). Veuillez réessayer.`;
          }
        }

        // S'assurer qu'on ne définit jamais "undefined" comme message
        if (!errorMessage || errorMessage === 'undefined') {
          errorMessage = 'Une erreur est survenue lors de l\'inscription. Veuillez réessayer.';
        }

        this.error.set(errorMessage);

        // Log detailed error for debugging
        this.errorHandler.logError('Registration HTTP error', err);

        this.loading.set(false);
      }
    });
  }

  /**
   * Nettoie et normalise la valeur NIF du masque PrimeNG.
   * - Enlève placeholders (_), espaces, convertit en majuscules.
   * - Normalise le format sans slashes (1234567ABC000) vers 1234567/A/B/C/000.
   * - Normalise 1234567A/B/C/000 (slash manquant après les 7 chiffres) vers 1234567/A/B/C/000.
   * - Normalise les slashes Unicode vers ASCII.
   */
  private cleanNifValue(value: string | null | undefined): string {
    if (!value) return '';

    let cleaned = value
      .replace(/_/g, '')
      .replace(/\s/g, '')
      .replace(/[\u2044\u2215\/]/g, '/')  // Unicode slash, division slash, ASCII slash → /
      .toUpperCase()
      .trim();

    if (!cleaned || cleaned === '/////' || cleaned === '///') {
      return '';
    }

    // Format sans slashes (ex. InputMask unmask) : 7 chiffres + 3 lettres + 3 chiffres
    const noSlashMatch = cleaned.match(/^(\d{7})([A-Z])([A-Z])([A-Z])(\d{3})$/);
    if (noSlashMatch) {
      return `${noSlashMatch[1]}/${noSlashMatch[2]}/${noSlashMatch[3]}/${noSlashMatch[4]}/${noSlashMatch[5]}`;
    }

    // Format avec slashes mais sans slash après les 7 chiffres (ex. 1234567A/B/C/000)
    const missingFirstSlash = cleaned.match(/^(\d{7})([A-Z])\/([A-Z])\/([A-Z])\/(\d{3})$/);
    if (missingFirstSlash) {
      return `${missingFirstSlash[1]}/${missingFirstSlash[2]}/${missingFirstSlash[3]}/${missingFirstSlash[4]}/${missingFirstSlash[5]}`;
    }

    return cleaned;
  }

  /**
   * Validates form data before submission.
   * @param formValue - Valeurs du formulaire
   * @param nifOverride - NIF déjà nettoyé (prioritaire) pour éviter désync avec l'InputMask
   */
  private validateFormData(formValue: any, nifOverride?: string): string[] {
    const errors: string[] = [];

    if (!formValue.taxRegime && formValue.taxRegime !== 0) {
      errors.push('Le régime fiscal est requis');
    }

    if (!formValue.governorate) {
      errors.push('Le gouvernorat est requis');
    }

    const phone = formValue.phone?.replace(/\s/g, '') || '';
    if (phone && !/^[2-57-9]\d{7}$/.test(phone)) {
      errors.push('Le numéro de téléphone doit contenir 8 chiffres et commencer par 2, 3, 4, 5, 7 ou 9');
    }

    const nifToValidate = nifOverride !== undefined
      ? nifOverride
      : this.cleanNifValue(formValue?.nif ?? this.form.get('nif')?.value ?? '');

    if (nifToValidate && nifToValidate.length > 0) {
      const nifRegex = /^\d{7}\/[A-Z]\/[A-Z]\/[A-Z]\/\d{3}$/;
      if (!nifRegex.test(nifToValidate)) {
        errors.push('Le format du NIF est invalide (format attendu: 1234567/A/B/C/000)');
      }
    } else {
      const nifControl = this.form.get('nif');
      if (nifControl?.touched) {
        errors.push('Le matricule fiscal est requis (format: 1234567/A/B/C/000)');
      }
    }

    return errors;
  }

  /**
   * Handles NIF field blur event to clean the value.
   */
  onNifBlur(): void {
    const nifControl = this.form.get('nif');
    if (nifControl) {
      const currentValue = nifControl.value || '';
      const cleaned = this.cleanNifValue(currentValue);

      // Toujours mettre à jour pour s'assurer que la valeur est propre
      if (cleaned !== currentValue) {
        nifControl.setValue(cleaned, { emitEvent: false });
        nifControl.updateValueAndValidity({ emitEvent: false });
        nifControl.markAsTouched();
      } else if (cleaned) {
        // Même si la valeur est déjà propre, forcer la validation
        nifControl.updateValueAndValidity({ emitEvent: false });
      }
    }
  }

  /**
   * Scrolls to the first invalid field in the form.
   */
  private scrollToFirstInvalidField(): void {
    const firstInvalidField = document.querySelector('.ng-invalid');
    if (firstInvalidField) {
      firstInvalidField.scrollIntoView({ behavior: 'smooth', block: 'center' });
      // Focus the field if it's an input
      const input = firstInvalidField.querySelector('input, select, textarea') as HTMLElement;
      if (input) {
        setTimeout(() => input.focus(), 300);
      }
    }
  }
}
