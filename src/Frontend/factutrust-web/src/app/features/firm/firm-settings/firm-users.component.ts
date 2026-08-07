import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { PasswordModule } from 'primeng/password';
import { environment } from '@environments/environment';
import { ApiResponse } from '@core/services/auth.service';

interface FirmUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: number;
  roleDisplay: string;
  isActive: boolean;
}

@Component({
  selector: 'app-firm-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TableModule, ButtonModule, InputTextModule, SelectModule, PasswordModule],
  template: `
    <div class="page">
      <h1>Utilisateurs du cabinet</h1>
      <p-table [value]="users()" class="mb-4">
        <ng-template pTemplate="header">
          <tr><th>Nom</th><th>Email</th><th>Rôle</th></tr>
        </ng-template>
        <ng-template pTemplate="body" let-u>
          <tr><td>{{ u.firstName }} {{ u.lastName }}</td><td>{{ u.email }}</td><td>{{ u.roleDisplay }}</td></tr>
        </ng-template>
      </p-table>
      <h2>Ajouter un utilisateur</h2>
      <form [formGroup]="form" (ngSubmit)="create()" class="form-grid">
        <input pInputText formControlName="firstName" placeholder="Prénom" />
        <input pInputText formControlName="lastName" placeholder="Nom" />
        <input pInputText formControlName="email" placeholder="Email" class="full" />
        <p-select formControlName="role" [options]="roles" optionLabel="label" optionValue="value" class="full" />
        <p-password formControlName="password" placeholder="Mot de passe" [toggleMask]="true" class="full" />
        <button pButton type="submit" label="Créer" [disabled]="form.invalid"></button>
      </form>
    </div>
  `,
  styles: [`
    .page { padding: 1.5rem; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; max-width: 480px; }
    .full { grid-column: 1 / -1; }
    .mb-4 { margin-bottom: 1.5rem; }
  `]
})
export class FirmUsersComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly users = signal<FirmUser[]>([]);
  readonly roles = [
    { label: 'Comptable cabinet', value: 12 },
    { label: 'Responsable cabinet', value: 11 }
  ];
  readonly form = this.fb.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    role: [12, Validators.required],
    password: ['', Validators.required]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.http.get<ApiResponse<FirmUser[]>>(`${environment.apiUrl}/firm/users`).subscribe(r => {
      if (r.success) this.users.set(r.data);
    });
  }

  create(): void {
    if (this.form.invalid) return;
    this.http.post(`${environment.apiUrl}/firm/users`, this.form.getRawValue()).subscribe(() => {
      this.form.reset({ role: 12 });
      this.load();
    });
  }
}
