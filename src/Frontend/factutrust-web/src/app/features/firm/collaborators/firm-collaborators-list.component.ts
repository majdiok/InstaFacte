import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { TableTotalsBarComponent, TotalMetric } from '@shared/components/table-totals-bar/table-totals-bar.component';
import { ToastService } from '@core/services/toast.service';
import { FirmCollaboratorsService, FirmUser } from '@core/services/firm-collaborators.service';
import { downloadBlob } from '@features/accounting/shared/accounting-download.util';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-firm-collaborators-list',
  standalone: true,
  providers: [ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TagModule,
    ConfirmDialogModule,
    PageHeaderComponent,
    TableTotalsBarComponent
  ],
  template: `
    <p-confirmDialog></p-confirmDialog>

    <app-page-header title="Collaborateurs" subtitle="Liste des collaborateurs du cabinet">
      <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" (onClick)="back()"></p-button>
      <p-button *ngIf="canManage()" label="Ajouter" icon="pi pi-plus" (onClick)="add()"></p-button>
      <p-button
        *ngIf="canManage()"
        label="Modifier"
        icon="pi pi-pencil"
        [outlined]="true"
        [disabled]="selected().length !== 1"
        (onClick)="editSelected()"></p-button>
      <p-button
        *ngIf="canManage()"
        label="Désactiver"
        icon="pi pi-ban"
        severity="danger"
        [outlined]="true"
        [disabled]="selected().length === 0"
        (onClick)="deactivateSelected()"></p-button>
      <p-button
        label="Analyse feuille de temps"
        icon="pi pi-chart-bar"
        [outlined]="true"
        [disabled]="selected().length !== 1"
        (onClick)="analyseTimesheet()"></p-button>
      <p-button
        label="Exporter vers Excel"
        icon="pi pi-file-excel"
        [outlined]="true"
        [loading]="exporting()"
        (onClick)="export()"></p-button>
    </app-page-header>

    <div class="filters">
      <input pInputText [(ngModel)]="filterName" placeholder="Rechercher par nom..." (keyup.enter)="reload()" />
      <input pInputText [(ngModel)]="filterQualification" placeholder="Rechercher par qualification..." (keyup.enter)="reload()" />
      <p-select
        [options]="statusOptions"
        [(ngModel)]="filterStatus"
        optionLabel="label"
        optionValue="value"
        placeholder="Statut"
        [showClear]="true"
        (onChange)="reload()"></p-select>
      <button pButton type="button" class="p-button-link" label="Réinitialiser la recherche" (click)="resetFilters()"></button>
      <button pButton type="button" icon="pi pi-search" label="Rechercher" (click)="reload()"></button>
    </div>

    <app-table-totals-bar [metrics]="summaryMetrics()" [loading]="loading()"></app-table-totals-bar>

    <div class="fc-card">
      <p-table
        [value]="users()"
        [loading]="loading()"
        [(selection)]="selection"
        dataKey="id"
        [paginator]="true"
        [rows]="15"
        responsiveLayout="scroll">
        <ng-template pTemplate="header">
          <tr>
            <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th>
            <th>Prénom Nom</th>
            <th>Email</th>
            <th>Téléphone</th>
            <th>Qualification</th>
            <th>Rôle</th>
            <th>Binôme(s)</th>
            <th>Statut</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-u>
          <tr>
            <td><p-tableCheckbox [value]="u"></p-tableCheckbox></td>
            <td>
              <a [routerLink]="['/firm/collaborateurs', u.id]" class="name-link">{{ u.firstName }} {{ u.lastName }}</a>
            </td>
            <td>{{ u.email }}</td>
            <td>{{ u.phoneNumber || '—' }}</td>
            <td>{{ u.qualification || '—' }}</td>
            <td>{{ u.roleDisplay }}</td>
            <td>{{ u.binomesDisplay || '—' }}</td>
            <td>
              <p-tag [severity]="u.isActive ? 'success' : 'danger'" [value]="u.isActive ? 'Actif' : 'Inactif'"></p-tag>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="8" class="empty">Aucun collaborateur</td></tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .filters {
      display: flex; flex-wrap: wrap; gap: .75rem; align-items: center;
      margin-bottom: 1rem;
    }
    .filters input { min-width: 200px; }
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .5rem;
    }
    .name-link { color: var(--color-primary-600, #2563eb); text-decoration: none; font-weight: 600; }
    .name-link:hover { text-decoration: underline; }
    .empty { text-align: center; color: var(--color-text-secondary, #64748b); padding: 1.5rem; }
  `]
})
export class FirmCollaboratorsListComponent implements OnInit {
  private readonly api = inject(FirmCollaboratorsService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly auth = inject(AuthService);

  readonly users = signal<FirmUser[]>([]);
  readonly loading = signal(false);
  readonly exporting = signal(false);
  selection: FirmUser[] = [];

  filterName = '';
  filterQualification = '';
  filterStatus: boolean | null = true;

  readonly statusOptions = [
    { label: 'Actifs', value: true },
    { label: 'Inactifs', value: false }
  ];

  readonly summaryMetrics = computed<TotalMetric[]>(() => [
    { label: 'Total', value: this.users().length, format: 'number' },
    { label: 'Actifs', value: this.users().filter(u => u.isActive).length, format: 'number' }
  ]);

  selected = () => this.selection;

  ngOnInit(): void {
    this.reload();
  }

  canManage(): boolean {
    return this.auth.hasPermission(PERMISSIONS.firm.usersManage);
  }

  reload(): void {
    this.loading.set(true);
    this.api.list({
      name: this.filterName.trim() || undefined,
      qualification: this.filterQualification.trim() || undefined,
      isActive: this.filterStatus
    }).subscribe({
      next: rows => {
        this.users.set(rows);
        this.selection = [];
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.message || 'Chargement impossible' });
      }
    });
  }

  resetFilters(): void {
    this.filterName = '';
    this.filterQualification = '';
    this.filterStatus = true;
    this.reload();
  }

  back(): void {
    this.location.back();
  }

  add(): void {
    void this.router.navigate(['/firm/collaborateurs/new']);
  }

  editSelected(): void {
    const row = this.selection[0];
    if (!row) return;
    void this.router.navigate(['/firm/collaborateurs', row.id, 'edit']);
  }

  deactivateSelected(): void {
    const ids = this.selection.filter(u => u.isActive).map(u => u.id);
    if (ids.length === 0) {
      this.toast.add({ severity: 'info', summary: 'Collaborateurs', detail: 'Aucun collaborateur actif sélectionné' });
      return;
    }
    this.confirm.confirm({
      message: `Désactiver ${ids.length} collaborateur(s) ?`,
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Désactiver',
      rejectLabel: 'Annuler',
      accept: () => {
        let remaining = ids.length;
        let failed = 0;
        for (const id of ids) {
          this.api.setActive(id, false).subscribe({
            next: () => {
              remaining--;
              if (remaining === 0) {
                if (failed) this.toast.add({ severity: 'error', summary: 'Erreur', detail: `${failed} désactivation(s) en échec` });
                else this.toast.add({ severity: 'success', summary: 'Collaborateurs', detail: 'Collaborateur(s) désactivé(s)' });
                this.reload();
              }
            },
            error: err => {
              failed++;
              remaining--;
              this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.message || 'Erreur' });
              if (remaining === 0) this.reload();
            }
          });
        }
      }
    });
  }

  analyseTimesheet(): void {
    const row = this.selection[0];
    if (!row) return;
    void this.router.navigate(['/firm/governance/time-sheets'], { queryParams: { userId: row.id } });
  }

  export(): void {
    this.exporting.set(true);
    this.api.exportExcel({
      name: this.filterName.trim() || undefined,
      qualification: this.filterQualification.trim() || undefined,
      isActive: this.filterStatus
    }).subscribe({
      next: blob => {
        downloadBlob(blob, `collaborateurs_${new Date().toISOString().slice(0, 10)}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.exporting.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Export impossible' });
      }
    });
  }
}
