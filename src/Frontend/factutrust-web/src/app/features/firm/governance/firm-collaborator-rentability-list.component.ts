import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import {
  FirmCollaboratorRentabilityListItem,
  FirmGovernanceService
} from '@core/services/firm-governance.service';
import { FirmCollaboratorsService, FirmUser } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-firm-collaborator-rentability-list',
  standalone: true,
  providers: [ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    DropdownModule,
    ConfirmDialogModule,
    PageHeaderComponent,
    EmptyStateComponent
  ],
  template: `
    <p-confirmDialog></p-confirmDialog>

    <app-page-header
      title="Marge sur coût direct"
      subtitle="Honoraires répartis au prorata des heures, moins le coût employeur et la structure">
      <button type="button" pButton label="Ajouter" icon="pi pi-plus" class="p-button-sm" (click)="goNew()"></button>
      <button
        type="button"
        pButton
        label="Recalculer"
        icon="pi pi-sync"
        class="p-button-sm p-button-outlined"
        title="Recalcule toutes les marges enregistrées. La valeur d'origine de chaque snapshot est conservée."
        [loading]="recalculating()"
        (click)="recalculate()"></button>
    </app-page-header>

    <div class="toolbar fc-card">
      <label>Année
        <p-dropdown
          [options]="yearOptions"
          [(ngModel)]="selectedYear"
          [showClear]="true"
          placeholder="Toutes"
          (onChange)="load()" />
      </label>
      <label>Collaborateur
        <p-dropdown
          [options]="collaboratorOptions"
          [(ngModel)]="selectedCollaboratorId"
          optionLabel="label"
          optionValue="value"
          [showClear]="true"
          [filter]="true"
          filterBy="label"
          placeholder="Tous"
          appendTo="body"
          (onChange)="load()" />
      </label>
      <button type="button" pButton label="Rechercher" icon="pi pi-search" class="p-button-sm" (click)="load()"></button>
    </div>

    @if (warnings().length > 0) {
      <div class="fc-card warnings">
        <strong>Cohérence des totaux</strong>
        <ul>
          @for (message of warnings(); track message) {
            <li>{{ message }}</li>
          }
        </ul>
      </div>
    }

    <div class="fc-card">
      @if (!loading() && items().length === 0) {
        <app-empty-state
          title="Aucune marge calculée"
          description="Ajoutez un snapshot pour un collaborateur et une année."
          icon="pi pi-chart-bar" />
      } @else {
        <p-table
          [value]="items()"
          [loading]="loading()"
          [(selection)]="selection"
          dataKey="id"
          [paginator]="true"
          [rows]="15"
          responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th>
              <th>Collaborateur</th>
              <th>Année</th>
              <th>Sociétés</th>
              <th>CA</th>
              <th>Masse salariale</th>
              <th>Admin</th>
              <th>Marge sur coût direct</th>
              <th style="width: 200px">Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td><p-tableCheckbox [value]="r"></p-tableCheckbox></td>
              <td>{{ r.collaboratorName }}</td>
              <td>{{ r.year }}</td>
              <td>{{ r.companiesCount }}</td>
              <td>{{ r.totalRevenue | number:'1.3-3' }}</td>
              <td>{{ r.payrollCost | number:'1.3-3' }}</td>
              <td>{{ r.adminPayrollCharge | number:'1.3-3' }}</td>
              <td [class.neg]="r.rentability < 0" [class.pos]="r.rentability > 0">
                {{ r.rentability | number:'1.3-3' }}
              </td>
              <td class="actions">
                <button type="button" pButton icon="pi pi-eye" class="p-button-text p-button-sm" title="Détails" (click)="goDetails(r)"></button>
                <button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" title="Modifier" (click)="goEdit(r)"></button>
                <button type="button" pButton icon="pi pi-copy" class="p-button-text p-button-sm" title="Dupliquer" (click)="duplicateOne(r)"></button>
                <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" title="Supprimer" (click)="confirmDelete(r)"></button>
              </td>
            </tr>
          </ng-template>
        </p-table>

        @if (totals(); as t) {
          <div class="totals-bar">
            <strong>Totaux</strong>
            <span>Sociétés : {{ t.companiesCount }}</span>
            <span>CA : {{ t.totalRevenue | number:'1.3-3' }}</span>
            <span>MS : {{ t.payrollCost | number:'1.3-3' }}</span>
            <span>Admin : {{ t.adminPayrollCharge | number:'1.3-3' }}</span>
            <span>Marge : <strong>{{ t.rentability | number:'1.3-3' }}</strong></span>
            <span>Encaissée : {{ t.collectedRentability | number:'1.3-3' }}</span>
            <span>Recouvrement : {{ t.recoveryRatePercent | number:'1.0-1' }} %</span>
          </div>
        }

        @if (selection.length > 0) {
          <div class="bulk-bar">
            <button
              type="button"
              pButton
              label="Dupliquer la sélection"
              icon="pi pi-copy"
              class="p-button-sm p-button-outlined"
              (click)="duplicateSelected()"></button>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .toolbar label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 140px; }
    .link-btn { font-size: .875rem; color: var(--color-primary, #0f766e); margin-left: .5rem; }
    .actions { display: flex; gap: .1rem; }
    .totals-bar {
      display: flex; flex-wrap: wrap; gap: .75rem 1.25rem;
      margin-top: .75rem; padding: .75rem;
      background: var(--color-surface-muted, #f8fafc);
      border-radius: 12px; font-size: .875rem;
    }
    .neg { color: #b91c1c; font-weight: 600; }
    .pos { color: #047857; font-weight: 600; }
    .bulk-bar { margin-top: .75rem; }
    .warnings { border-color: #f59e0b; background: #fffbeb; font-size: .875rem; }
    .warnings ul { margin: .5rem 0 0; padding-left: 1.25rem; }
  `]
})
export class FirmCollaboratorRentabilityListComponent implements OnInit {
  private readonly api = inject(FirmGovernanceService);
  private readonly collaboratorsApi = inject(FirmCollaboratorsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly router = inject(Router);

  items = signal<FirmCollaboratorRentabilityListItem[]>([]);
  totals = signal<FirmCollaboratorRentabilityListItem | null>(null);
  /** Alertes de cohérence sur les totaux, notamment le double comptage des charges réparties. */
  warnings = signal<string[]>([]);
  loading = signal(false);
  recalculating = signal(false);
  collaboratorOptions: { label: string; value: string }[] = [];
  selection: FirmCollaboratorRentabilityListItem[] = [];

  selectedYear: number | null = new Date().getFullYear();
  selectedCollaboratorId: string | null = null;

  readonly yearOptions = Array.from({ length: 8 }, (_, i) => {
    const y = new Date().getFullYear() - 4 + i;
    return { label: String(y), value: y };
  });

  ngOnInit(): void {
    this.collaboratorsApi.list({ isActive: true }).subscribe({
      next: (users: FirmUser[]) => {
        this.collaboratorOptions = users.map(u => ({
          label: `${u.firstName} ${u.lastName}`.trim() || u.email,
          value: u.id
        }));
      }
    });
    this.load();
  }

  /**
   * Recalcule toutes les marges enregistrées.
   *
   * Action explicite : elle modifie des chiffres déjà communiqués. Le serveur conserve la valeur
   * d'origine de chaque snapshot et laisse intacts les exercices sans feuille de temps.
   */
  recalculate(): void {
    this.recalculating.set(true);
    this.api.recalculateRentability().subscribe({
      next: res => {
        this.recalculating.set(false);
        this.load();
        const result = res.data;
        if (result && result.skipped.length > 0) {
          this.toast.add({
            severity: 'warn',
            summary: 'Recalcul partiel',
            detail: `${result.recalculated} marge(s) recalculée(s), `
              + `${result.skipped.length} laissée(s) intacte(s) : ${result.skipped[0].reason}`
          });
        } else {
          this.toast.add({
            severity: 'success',
            summary: 'Recalcul terminé',
            detail: `${result?.recalculated ?? 0} marge(s) recalculée(s).`
          });
        }
      },
      error: (err) => {
        this.recalculating.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Recalcul impossible.'
        });
      }
    });
  }

  load(): void {
    this.loading.set(true);
    this.selection = [];
    this.api.listRentability(
      this.selectedYear ?? undefined,
      this.selectedCollaboratorId ?? undefined
    ).subscribe({
      next: res => {
        this.items.set(res.data?.items ?? []);
        this.totals.set(res.data?.totals ?? null);
        this.warnings.set(res.data?.warnings ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Chargement impossible.' });
      }
    });
  }

  goNew(): void {
    void this.router.navigate(['/firm/governance/collaborator-rentability/new']);
  }

  goDetails(r: FirmCollaboratorRentabilityListItem): void {
    void this.router.navigate(['/firm/governance/collaborator-rentability', r.id]);
  }

  goEdit(r: FirmCollaboratorRentabilityListItem): void {
    void this.router.navigate(['/firm/governance/collaborator-rentability', r.id, 'edit']);
  }

  confirmDelete(r: FirmCollaboratorRentabilityListItem): void {
    this.confirm.confirm({
      message: `Supprimer la rentabilité de ${r.collaboratorName} (${r.year}) ?`,
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      accept: () => this.delete(r.id)
    });
  }

  duplicateOne(r: FirmCollaboratorRentabilityListItem): void {
    this.duplicate([r.id]);
  }

  duplicateSelected(): void {
    this.duplicate(this.selection.map(s => s.id));
  }

  private delete(id: string): void {
    this.api.deleteRentability(id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Supprimé', detail: 'Marge supprimée.' });
        this.load();
      },
      error: (err) => this.toast.add({
        severity: 'error',
        summary: 'Erreur',
        detail: err?.error?.message || 'Suppression impossible.'
      })
    });
  }

  private duplicate(ids: string[]): void {
    if (!ids.length) return;
    this.api.duplicateRentability(ids).subscribe({
      next: res => {
        const d = res.data;
        this.toast.add({
          severity: 'success',
          summary: 'Duplication',
          detail: `${d?.duplicated ?? 0} dupliqué(s), ${d?.skipped ?? 0} ignoré(s).`
        });
        this.load();
      },
      error: (err) => this.toast.add({
        severity: 'error',
        summary: 'Erreur',
        detail: err?.error?.message || 'Duplication impossible.'
      })
    });
  }
}
