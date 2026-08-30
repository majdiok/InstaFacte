import { Component, OnDestroy, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { trigger, transition, style, animate } from '@angular/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { timeout, TimeoutError, catchError, throwError } from 'rxjs';
import { AuthService, RegisterRequest } from '@core/services/auth.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AppModule } from '@core/models/app-module';
import { environment } from '@environments/environment';
import { LogoComponent } from '@shared/components/logo/logo.component';
import { GOVERNORATE_OPTIONS } from '../shared/auth-governorate.options';
import {
  AUTH_PASSWORD_VALIDATORS_PATTERN,
  passwordMismatch as checkPasswordMismatch
} from '../shared/auth-password.helpers';
import {
  applyNifBlurCleanup,
  buildCompanyRegisterRequest,
  cleanNifValue,
  markAllFormControlsTouched,
  scrollAuthWizardStepIntoView,
  scrollToFirstInvalidField,
  validateCompanyRegisterFormData
} from '../shared/auth-registration.helpers';
import { RegistrationCatalogService } from './registration-catalog';
import { TaxRegime } from './tax-regime.types';
import { StepCompanyTypeComponent } from './steps/step-company-type/step-company-type.component';
import { StepInformationsComponent } from './steps/step-informations/step-informations.component';
import { StepConfigurationComponent } from './steps/step-configuration/step-configuration.component';
import { StepFinalisationComponent } from './steps/step-finalisation/step-finalisation.component';

interface WizardStepMeta {
  name: string;
  desc: string;
  icon: string;
  why: string[];
}

@Component({
  selector: 'app-register-wizard',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LogoComponent,
    StepCompanyTypeComponent,
    StepInformationsComponent,
    StepConfigurationComponent,
    StepFinalisationComponent
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
  templateUrl: './register-wizard.component.html',
  styleUrl: './register-wizard.component.scss'
})
export class RegisterWizardComponent implements OnInit, OnDestroy {
  readonly environment = environment;
  readonly catalog = inject(RegistrationCatalogService);

  private static readonly REGISTRATION_TIMEOUT_MS = 120_000;

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private warehouseContext = inject(WarehouseContextService);
  private errorHandler = inject(ErrorHandlerService);

  loading = signal(false);
  loadingMessage = signal('Création de votre espace...');
  error = signal<string | null>(null);
  currentStep = signal(0);
  /** Once the user manually toggles a module, recommendation auto-recompute stops overwriting their choices. */
  modulesTouched = signal(false);

  private loadingMessageTimer: ReturnType<typeof setInterval> | null = null;
  private loadingStartedAt = 0;

  readonly stepsMeta: WizardStepMeta[] = [
    {
      name: 'Type de société',
      desc: 'Segment et domaine',
      icon: 'pi-sitemap',
      why: [
        'Configuration adaptée à votre activité',
        'Plan comptable tunisien personnalisé',
        'Modules et workflows sur mesure',
        'Gain de temps dès le démarrage'
      ]
    },
    {
      name: 'Informations générales',
      desc: 'Compte et société',
      icon: 'pi-id-card',
      why: [
        'Vos informations restent confidentielles',
        'Un seul compte administrateur créé',
        'Coordonnées utilisées pour les factures',
        'Modifiable à tout moment plus tard'
      ]
    },
    {
      name: 'Configuration',
      desc: 'Modules recommandés',
      icon: 'pi-cog',
      why: [
        'Modules déjà présélectionnés pour vous',
        'Rien n’est figé : tout reste modifiable',
        'Activez uniquement ce qui vous sert',
        'Paramètres tunisiens préconfigurés'
      ]
    },
    {
      name: 'Finalisation',
      desc: 'Adresse et récapitulatif',
      icon: 'pi-check-circle',
      why: [
        'Vérifiez votre espace avant de le créer',
        'Modifiez toute étape en un clic',
        'Adresse utilisée pour vos documents',
        'Votre espace est prêt en quelques secondes'
      ]
    }
  ];

  registrationFormAriaLabel = computed(() => {
    const cur = this.currentStep();
    const prev = cur > 0 ? `Étapes précédentes complétées. ` : '';
    return `${prev}Étape ${cur + 1} sur ${this.stepsMeta.length} : ${this.stepsMeta[cur].name}.`;
  });

  taxRegimes: TaxRegime[] = [
    { label: 'Régime réel', value: 0 },
    { label: 'Régime forfaitaire', value: 1 },
    { label: 'Exonéré', value: 2 }
  ];

  readonly governorates = GOVERNORATE_OPTIONS;

  form: FormGroup = this.fb.group({
    // Step 1: Type de société
    companySegment: ['', Validators.required],
    businessDomain: ['', Validators.required],
    // Step 2 §Votre compte
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(12),
      Validators.pattern(AUTH_PASSWORD_VALIDATORS_PATTERN)
    ]],
    confirmPassword: ['', Validators.required],
    // Optional; not sent to API (existing TODO preserved, see register.component.ts)
    partnerCode: [''],
    // Step 2 §Votre société
    companyName: ['', [Validators.required, Validators.minLength(2)]],
    nif: ['', Validators.required],
    taxRegime: [null, Validators.required],
    companyEmail: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    website: [''],
    warehouseName: [''],
    // Step 3: Configuration
    enabledModules: [[...this.catalog.recommendedModules(null, null)]],
    // Step 4: Finalisation
    street: ['', Validators.required],
    streetLine2: [''],
    city: ['', Validators.required],
    postalCode: [''],
    governorate: ['', Validators.required],
    acceptTerms: [false, Validators.requiredTrue]
  });

  passwordMismatch(): boolean {
    return checkPasswordMismatch(this.form.get('password')?.value, this.form.get('confirmPassword')?.value);
  }

  stepHumanIndex(): number {
    return this.currentStep() + 1;
  }

  stepCount(): number {
    return this.stepsMeta.length;
  }

  selectedSegment(): string {
    return this.form.get('companySegment')?.value ?? '';
  }

  selectedDomain(): string {
    return this.form.get('businessDomain')?.value ?? '';
  }

  ngOnInit(): void {
    this.form.get('password')?.valueChanges.subscribe(() => {
      this.form.get('confirmPassword')?.updateValueAndValidity();
    });

    this.form.get('companySegment')?.valueChanges.subscribe(() => this.onProfileChanged());
    this.form.get('businessDomain')?.valueChanges.subscribe(() => this.onProfileChanged());
  }

  ngOnDestroy(): void {
    this.clearLoadingMessageTimer();
  }

  /** Recomputes recommended modules on segment/domain change, unless the user already customized their selection. */
  private onProfileChanged(): void {
    if (this.modulesTouched()) return;
    const recommended = this.catalog.recommendedModules(this.selectedSegment(), this.selectedDomain());
    this.form.get('enabledModules')?.setValue(recommended);
  }

  onModuleToggled(moduleId: AppModule): void {
    this.modulesTouched.set(true);
    const control = this.form.get('enabledModules');
    const current: AppModule[] = control?.value ?? [];
    const next = current.includes(moduleId) ? current.filter(m => m !== moduleId) : [...current, moduleId];
    control?.setValue(next);
  }

  resetModulesToRecommendations(): void {
    this.modulesTouched.set(false);
    const recommended = this.catalog.recommendedModules(this.selectedSegment(), this.selectedDomain());
    this.form.get('enabledModules')?.setValue(recommended);
  }

  isCurrentStepValid(): boolean {
    const step = this.currentStep();

    if (step === 0) {
      return !!this.form.get('companySegment')?.value && !!this.form.get('businessDomain')?.value;
    }

    if (step === 1) {
      const fields = [
        'firstName', 'lastName', 'email', 'password', 'confirmPassword',
        'companyName', 'nif', 'taxRegime', 'companyEmail', 'phone'
      ];
      return fields.every(f => this.form.get(f)?.valid) && !this.passwordMismatch();
    }

    if (step === 2) {
      return true;
    }

    if (step === 3) {
      const fields = ['street', 'city', 'governorate', 'acceptTerms'];
      return fields.every(f => this.form.get(f)?.valid);
    }

    return false;
  }

  goToStep(index: number): void {
    if (index < 0 || index >= this.stepsMeta.length || index === this.currentStep()) return;
    // Allow jumping back freely (recap "Modifier" links); only gate forward navigation.
    if (index > this.currentStep() && !this.isCurrentStepValid()) return;
    this.currentStep.set(index);
    scrollAuthWizardStepIntoView();
  }

  nextStep(): void {
    if (this.isCurrentStepValid() && this.currentStep() < this.stepsMeta.length - 1) {
      this.currentStep.update(s => s + 1);
      scrollAuthWizardStepIntoView();
    }
  }

  previousStep(): void {
    if (this.currentStep() > 0) {
      this.currentStep.update(s => s - 1);
      scrollAuthWizardStepIntoView();
    }
  }

  onNifBlur(): void {
    applyNifBlurCleanup(this.form);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      markAllFormControlsTouched(this.form);
      scrollToFirstInvalidField();
      return;
    }

    if (!this.form.get('acceptTerms')?.value) {
      this.form.get('acceptTerms')?.markAsTouched();
      scrollToFirstInvalidField();
      return;
    }

    if (this.passwordMismatch()) {
      this.error.set('Les mots de passe ne correspondent pas');
      this.form.get('confirmPassword')?.markAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.loadingMessage.set('Création de votre espace...');
    this.loadingStartedAt = Date.now();
    this.startLoadingMessageTimer();

    const nifControl = this.form.get('nif');
    const rawNif = nifControl?.value ?? '';
    const cleanedNif = cleanNifValue(rawNif);
    if (nifControl && cleanedNif !== rawNif) {
      nifControl.setValue(cleanedNif, { emitEvent: false });
      nifControl.updateValueAndValidity({ emitEvent: false });
    }

    const formValue = this.form.getRawValue();

    const validationErrors = validateCompanyRegisterFormData(
      formValue,
      cleanedNif,
      !!this.form.get('nif')?.touched
    );
    if (validationErrors.length > 0) {
      this.error.set(validationErrors.join(', '));
      this.stopLoading();
      return;
    }

    const request: RegisterRequest = {
      ...buildCompanyRegisterRequest(formValue, cleanedNif),
      companySegment: formValue.companySegment || undefined,
      businessDomain: formValue.businessDomain || undefined,
      enabledModules: Array.isArray(formValue.enabledModules) ? formValue.enabledModules : undefined
    };

    this.authService.register(request).pipe(
      timeout(RegisterWizardComponent.REGISTRATION_TIMEOUT_MS),
      catchError(err => {
        if (err instanceof TimeoutError) {
          return throwError(() => ({
            status: 0,
            message: 'La création du compte prend plus de temps que prévu. Veuillez patienter ou réessayer dans quelques instants.'
          }));
        }
        return throwError(() => err);
      })
    ).subscribe({
      next: (response) => {
        if (response.success) {
          console.log('[RegisterWizardComponent] Registration successful');
          this.warehouseContext.navigateAfterSuccessfulAuth('/dashboard');
        } else {
          const errorMessage = response.errors?.length > 0
            ? response.errors.join(', ')
            : response.message || 'Une erreur est survenue lors de l\'inscription';
          this.error.set(errorMessage);
          this.errorHandler.logError('Registration failed (success: false)', { response });
        }
        this.stopLoading();
      },
      error: (err: HttpErrorResponse | { status?: number; message?: string }) => {
        let errorMessage = err instanceof HttpErrorResponse
          ? this.errorHandler.extractErrorMessage(err)
          : ('message' in err && err.message ? err.message : this.errorHandler.extractErrorMessage(err as HttpErrorResponse));

        if (!errorMessage || errorMessage === 'undefined' || errorMessage.trim() === '' || errorMessage.includes('undefined')) {
          const status = 'status' in err ? err.status : (err as HttpErrorResponse)?.status;
          if (!status) {
            errorMessage = 'Impossible de se connecter au serveur. Vérifiez que le backend est démarré sur https://localhost:7001.';
          } else {
            errorMessage = `Une erreur est survenue lors de l'inscription (${status || 'erreur inconnue'}). Veuillez réessayer.`;
          }
        }

        if (!errorMessage || errorMessage === 'undefined') {
          errorMessage = 'Une erreur est survenue lors de l\'inscription. Veuillez réessayer.';
        }

        this.error.set(errorMessage);
        this.errorHandler.logError('Registration HTTP error', err);
        this.stopLoading();
      }
    });
  }

  private startLoadingMessageTimer(): void {
    this.clearLoadingMessageTimer();
    this.loadingMessageTimer = setInterval(() => {
      const elapsed = Date.now() - this.loadingStartedAt;
      if (elapsed < 5_000) {
        this.loadingMessage.set('Création de votre espace...');
      } else if (elapsed < 30_000) {
        this.loadingMessage.set('Configuration de la base de données, cela peut prendre une minute...');
      } else {
        this.loadingMessage.set('Finalisation en cours, merci de patienter...');
      }
    }, 1_000);
  }

  private clearLoadingMessageTimer(): void {
    if (this.loadingMessageTimer !== null) {
      clearInterval(this.loadingMessageTimer);
      this.loadingMessageTimer = null;
    }
  }

  private stopLoading(): void {
    this.clearLoadingMessageTimer();
    this.loading.set(false);
  }
}
