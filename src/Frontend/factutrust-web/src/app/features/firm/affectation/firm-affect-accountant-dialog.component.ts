import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import {
  FirmAssignableAccountant,
  FirmGovernanceService
} from '@core/services/firm-governance.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-firm-affect-accountant-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, DropdownModule, ButtonModule],
  template: `
    <p-dialog
      header="Affecter à un gestionnaire comptable"
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [style]="{ width: '480px' }"
      [closable]="!saving()"
      [draggable]="false">
      <p class="hint">
        {{ dossierCount }} dossier{{ dossierCount > 1 ? 's' : '' }} sélectionné{{ dossierCount > 1 ? 's' : '' }}.
        Choisissez un gestionnaire comptable du cabinet, ou laissez vide pour désaffecter.
      </p>

      <label class="field">
        Gestionnaire comptable
        <p-dropdown
          [options]="accountants()"
          [(ngModel)]="selectedId"
          optionLabel="fullName"
          optionValue="id"
          placeholder="— Aucun (désaffectation) —"
          [showClear]="true"
          [filter]="true"
          filterBy="fullName,email"
          appendTo="body"
          styleClass="w-full"
          [loading]="loading()" />
      </label>

      <ng-template pTemplate="footer">
        <button
          type="button"
          pButton
          label="Annuler"
          class="p-button-text"
          [disabled]="saving()"
          (click)="close()"></button>
        <button
          type="button"
          pButton
          label="Affecter"
          icon="pi pi-check"
          [loading]="saving()"
          (click)="confirm()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .hint {
      margin: 0 0 1rem;
      color: var(--color-text-muted, #64748b);
      font-size: 0.9rem;
      line-height: 1.4;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
      font-size: 0.875rem;
      font-weight: 500;
    }
    :host ::ng-deep .w-full { width: 100%; }
  `]
})
export class FirmAffectAccountantDialogComponent implements OnChanges {
  private readonly governance = inject(FirmGovernanceService);
  private readonly toast = inject(ToastService);

  @Input() visible = false;
  @Input() assignmentIds: string[] = [];
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() assigned = new EventEmitter<void>();

  readonly accountants = signal<FirmAssignableAccountant[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  selectedId: string | null = null;

  get dossierCount(): number {
    return this.assignmentIds.length;
  }

  ngOnChanges(): void {
    if (this.visible) {
      this.selectedId = null;
      this.loadAccountants();
    }
  }

  onVisibleChange(value: boolean): void {
    this.visibleChange.emit(value);
  }

  close(): void {
    this.visibleChange.emit(false);
  }

  private loadAccountants(): void {
    this.loading.set(true);
    this.governance.listAssignableAccountants().subscribe({
      next: res => {
        this.accountants.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger les gestionnaires comptables.'
        });
      }
    });
  }

  confirm(): void {
    if (!this.assignmentIds.length) return;
    this.saving.set(true);
    const ids = [...this.assignmentIds];
    const accountantUserId = this.selectedId;

    const request =
      ids.length === 1
        ? this.governance.assignManager(ids[0], accountantUserId)
        : this.governance.assignManagerBulk(ids, accountantUserId);

    request.subscribe({
      next: res => {
        this.saving.set(false);
        if (!res.success) {
          this.toast.add({
            severity: 'error',
            summary: 'Affectation impossible',
            detail: res.message ?? 'Une erreur est survenue.'
          });
          return;
        }

        const bulk = res.data as { succeeded?: number; failed?: { error: string }[] } | null;
        if (bulk && typeof bulk.succeeded === 'number' && Array.isArray(bulk.failed)) {
          if (bulk.failed.length === 0) {
            this.toast.add({
              severity: 'success',
              summary: 'Affectation réussie',
              detail: `${bulk.succeeded} dossier(s) mis à jour.`
            });
          } else {
            this.toast.add({
              severity: 'warn',
              summary: 'Affectation partielle',
              detail: `${bulk.succeeded} OK, ${bulk.failed.length} échec(s).`
            });
          }
        } else {
          this.toast.add({
            severity: 'success',
            summary: 'Affectation réussie',
            detail: accountantUserId
              ? 'Gestionnaire comptable affecté.'
              : 'Gestionnaire désaffecté.'
          });
        }

        this.assigned.emit();
        this.close();
      },
      error: () => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Affectation impossible',
          detail: 'Une erreur est survenue.'
        });
      }
    });
  }
}
