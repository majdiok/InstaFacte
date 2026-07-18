import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import {
  FirmAssignmentService,
  AccountingFirmDirectoryItem,
  FirmClientAssignment
} from '@core/services/firm-assignment.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

@Component({
  selector: 'app-accounting-firm-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AutoCompleteModule,
    ButtonModule,
    CardModule,
    MessageModule,
    TagModule,
    PageHeaderComponent
  ],
  template: `
    <app-page-header
      title="Cabinet comptable"
      subtitle="Confiez la gestion comptable de votre société à un cabinet partenaire">
    </app-page-header>

    @if (current()) {
      <p-card class="mb-4">
        <div class="current-row">
          <div>
            <strong>{{ current()!.firmDisplayName }}</strong>
            <p-tag [value]="current()!.statusDisplay" [severity]="statusSeverity(current()!)"></p-tag>
          </div>
          @if (current()!.status === 1) {
            <button pButton label="Révoquer l'affectation" class="p-button-danger p-button-outlined" (click)="revoke()"></button>
          }
        </div>
      </p-card>
    } @else {
      <p-card header="Rechercher un cabinet">
        <form [formGroup]="form" (ngSubmit)="submit()">
          <p-autoComplete
            formControlName="firm"
            [suggestions]="suggestions()"
            (completeMethod)="search($event)"
            field="displayName"
            placeholder="Nom ou ville du cabinet"
            [dropdown]="true"
            styleClass="w-full">
          </p-autoComplete>
          @if (message()) {
            <p-message [severity]="message()!.severity" [text]="message()!.text" styleClass="mt-3"></p-message>
          }
          <button pButton type="submit" label="Envoyer la demande" class="mt-3" [disabled]="form.invalid || submitting()"></button>
        </form>
      </p-card>
    }
  `,
  styles: [`
    .current-row { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .mb-4 { margin-bottom: 1rem; }
    .mt-3 { margin-top: 1rem; }
    :host ::ng-deep .w-full { width: 100%; }
  `]
})
export class AccountingFirmSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly assignments = inject(FirmAssignmentService);

  readonly form = this.fb.group({
    firm: [null as AccountingFirmDirectoryItem | null, Validators.required]
  });
  readonly suggestions = signal<AccountingFirmDirectoryItem[]>([]);
  readonly current = signal<FirmClientAssignment | null>(null);
  readonly submitting = signal(false);
  readonly message = signal<{ severity: 'success' | 'error'; text: string } | null>(null);

  ngOnInit(): void {
    this.assignments.getCompanyCurrent().subscribe(r => {
      if (r.success) this.current.set(r.data);
    });
  }

  search(event: { query: string }): void {
    this.assignments.searchDirectory(event.query).subscribe(r => {
      if (r.success) this.suggestions.set(r.data);
    });
  }

  submit(): void {
    const firm = this.form.value.firm;
    if (!firm) return;
    this.submitting.set(true);
    this.assignments.requestAssignment(firm.firmTenantId).subscribe({
      next: r => {
        this.submitting.set(false);
        if (r.success) {
          this.message.set({ severity: 'success', text: 'Demande envoyée au cabinet.' });
          this.current.set(r.data);
        } else {
          this.message.set({ severity: 'error', text: r.message ?? 'Erreur' });
        }
      },
      error: () => {
        this.submitting.set(false);
        this.message.set({ severity: 'error', text: 'Erreur lors de l\'envoi' });
      }
    });
  }

  revoke(): void {
    this.assignments.revokeByCompany().subscribe(r => {
      if (r.success) {
        this.current.set(null);
        this.message.set({ severity: 'success', text: 'Affectation révoquée.' });
      }
    });
  }

  statusSeverity(a: FirmClientAssignment): 'success' | 'warn' | 'danger' | 'info' {
    if (a.status === 1) return 'success';
    if (a.status === 0) return 'warn';
    return 'info';
  }
}
