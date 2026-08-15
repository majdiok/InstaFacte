import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { AuthService } from '@core/services/auth.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ErrorMessageService } from '@core/services/error-message.service';
import { ToastService } from '@core/services/toast.service';
import { LogoComponent } from '@shared/components/logo/logo.component';
import { AuthShellComponent } from '../auth-shell/auth-shell.component';
import { LOGIN_AUTH_SHELL_CONFIG } from '../auth-shell/auth-shell.config';
import { environment } from '@environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    CheckboxModule,
    MessageModule,
    LogoComponent,
    AuthShellComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  readonly environment = environment;
  readonly shellConfig = LOGIN_AUTH_SHELL_CONFIG;
  readonly rememberMeHint =
    'Sans cette option, chaque fenêtre garde sa propre session. Avec cette option, la session est partagée entre tous les onglets de ce navigateur.';

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private warehouseContext = inject(WarehouseContextService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toastService = inject(ToastService);
  errorMessageService = inject(ErrorMessageService);

  loading = signal(false);
  error = signal<string | null>(null);

  form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    rememberMe: [false]
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParams['reset'] === 'success') {
      this.toastService.add({
        severity: 'success',
        summary: 'Mot de passe mis à jour',
        detail: 'Vous pouvez maintenant vous connecter avec votre nouveau mot de passe.',
        life: 6000,
      });
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { reset: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }
  }

  isEmailInvalid(): boolean {
    const c = this.form.get('email');
    return !!(c?.invalid && c.touched);
  }

  isPasswordInvalid(): boolean {
    const c = this.form.get('password');
    return !!(c?.invalid && c.touched);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.authService.login(this.form.value).subscribe({
      next: (response) => {
        if (response.success) {
          const defaultUrl = this.authService.isAccountingFirm() ? '/firm/dashboard' : '/dashboard';
          const returnUrl = this.route.snapshot.queryParams['returnUrl'] || defaultUrl;
          this.warehouseContext.navigateAfterSuccessfulAuth(returnUrl);
        } else {
          this.error.set(response.errors[0] || 'Une erreur est survenue');
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
}
