import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { AuthService, RegisterAccountingFirmRequest } from '@core/services/auth.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { LogoComponent } from '@shared/components/logo/logo.component';
import { environment } from '@environments/environment';

@Component({
  selector: 'app-register-firm',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    DropdownModule,
    CheckboxModule,
    MessageModule,
    LogoComponent
  ],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <app-logo />
        <h1>Inscription cabinet comptable</h1>
        <p class="subtitle">Créez votre espace cabinet pour gérer les dossiers de vos clients sociétés.</p>

        @if (!environment.accountingFirmsEnabled) {
          <p-message severity="warn" text="Fonctionnalité non disponible."></p-message>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="grid">
              <input pInputText formControlName="firstName" placeholder="Prénom" />
              <input pInputText formControlName="lastName" placeholder="Nom" />
              <input pInputText formControlName="email" placeholder="Email connexion" class="full" />
              <input pInputText formControlName="firmName" placeholder="Raison sociale du cabinet" class="full" />
              <input pInputText formControlName="nif" placeholder="NIF" class="full" />
              <input pInputText formControlName="street" placeholder="Adresse" class="full" />
              <input pInputText formControlName="city" placeholder="Ville" />
              <p-dropdown formControlName="governorate" [options]="governorates" placeholder="Gouvernorat" />
              <input pInputText formControlName="firmEmail" placeholder="Email cabinet" class="full" />
              <input pInputText formControlName="phone" placeholder="Téléphone" class="full" />
              <p-password formControlName="password" placeholder="Mot de passe" [toggleMask]="true" class="full" />
              <p-password formControlName="confirmPassword" placeholder="Confirmer" [toggleMask]="true" class="full" />
            </div>
            <div class="checkbox-row">
              <p-checkbox formControlName="isPublicInDirectory" [binary]="true" inputId="pub" />
              <label for="pub">Visible dans l'annuaire des cabinets</label>
            </div>
            @if (error()) {
              <p-message severity="error" [text]="error()!" class="mt-2"></p-message>
            }
            <button pButton type="submit" label="Créer le cabinet" class="w-full mt-3" [loading]="loading()"></button>
          </form>
        }
        <p class="footer-link"><a routerLink="/auth/login">Déjà un compte ? Connexion</a></p>
        <p class="footer-link"><a routerLink="/auth/register">Inscription société</a></p>
      </div>
    </div>
  `,
  styles: [`
    .auth-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 2rem; background: var(--color-neutral-50); }
    .auth-card { width: 100%; max-width: 520px; background: white; padding: 2rem; border-radius: var(--radius-xl); box-shadow: var(--shadow-lg); }
    .subtitle { color: var(--color-neutral-600); margin-bottom: 1.5rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .full { grid-column: 1 / -1; }
    .checkbox-row { display: flex; align-items: center; gap: 0.5rem; margin-top: 1rem; }
    .footer-link { margin-top: 1rem; text-align: center; font-size: 0.875rem; }
    .w-full { width: 100%; }
    .mt-2 { margin-top: 0.5rem; display: block; }
    .mt-3 { margin-top: 1rem; }
  `]
})
export class RegisterFirmComponent {
  readonly environment = environment;
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly warehouseContext = inject(WarehouseContextService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly governorates = [
    'Tunis', 'Ariana', 'Ben Arous', 'Manouba', 'Nabeul', 'Zaghouan', 'Bizerte', 'Béja', 'Jendouba',
    'Kef', 'Siliana', 'Sousse', 'Monastir', 'Mahdia', 'Sfax', 'Kairouan', 'Kasserine', 'Sidi Bouzid',
    'Gabès', 'Medenine', 'Tataouine', 'Gafsa', 'Tozeur', 'Kebili'
  ].map(g => ({ label: g, value: g }));

  readonly form = this.fb.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    firmName: ['', Validators.required],
    nif: ['', Validators.required],
    street: ['', Validators.required],
    city: ['', Validators.required],
    governorate: ['', Validators.required],
    firmEmail: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    password: ['', Validators.required],
    confirmPassword: ['', Validators.required],
    isPublicInDirectory: [true]
  });

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    if (v.password !== v.confirmPassword) {
      this.error.set('Les mots de passe ne correspondent pas');
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    const payload: RegisterAccountingFirmRequest = {
      email: v.email!,
      password: v.password!,
      confirmPassword: v.confirmPassword!,
      firstName: v.firstName!,
      lastName: v.lastName!,
      firmName: v.firmName!,
      nif: v.nif!,
      street: v.street!,
      city: v.city!,
      governorate: v.governorate as string,
      firmEmail: v.firmEmail!,
      phone: v.phone!,
      isPublicInDirectory: v.isPublicInDirectory ?? true
    };
    this.auth.registerFirm(payload).subscribe({
      next: r => {
        this.loading.set(false);
        if (r.success) {
          this.warehouseContext.navigateAfterSuccessfulAuth('/firm/dashboard');
        } else {
          this.error.set(r.message ?? 'Erreur inscription');
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Erreur inscription');
      }
    });
  }
}
