import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { ToastService } from '@core/services/toast.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import {
  FIRM_LEAVE_MIRROR_STATE,
  FirmLeaveReconciliation,
  FirmLeaveReconciliationRow
} from './data-access/firm-leaves.models';

/**
 * Rapprochement congés cabinet ↔ paie interne.
 *
 * Le report vers la paie est volontairement fail-open : une base injoignable ne doit pas annuler
 * une approbation RH. Cet écran est la contrepartie de ce choix — l'endroit où l'écart se voit et
 * se rattrape.
 */
@Component({
  selector: 'app-firm-leaves-reconciliation',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, SelectModule, TagModule, EmptyStateComponent],
  template: `
    <div class="toolbar">
      <label>Année
        <p-select [options]="yearOptions" [(ngModel)]="year" (onChange)="reload()" />
      </label>
      <button
        type="button"
        pButton
        label="Actualiser"
        icon="pi pi-refresh"
        class="p-button-sm p-button-outlined"
        (click)="reload()"></button>
      @if (replayableCount() > 0) {
        <button
          type="button"
          pButton
          [label]="'Rejouer ' + replayableCount() + ' report(s)'"
          icon="pi pi-replay"
          class="p-button-sm"
          [loading]="replaying()"
          (click)="replayAll()"></button>
      }
    </div>

    @if (report(); as r) {
      <div class="fc-card summary">
        <span><strong>{{ r.mirroredCount }}</strong> congé(s) reporté(s) en paie</span>
        <span><strong>{{ r.noPayrollEffectCount }}</strong> sans effet paie (type non mappé)</span>
        <span [class.warn]="r.pending.length > 0">
          <strong>{{ r.pending.length }}</strong> écart(s) à traiter
        </span>
      </div>
    }

    <div class="fc-card">
      @if (!loading() && (report()?.pending?.length ?? 0) === 0) {
        <app-empty-state
          title="Aucun écart"
          description="Tous les congés approuvés ont produit leur effet en paie."
          icon="pi pi-check-circle" />
      } @else {
        <p-table [value]="report()?.pending ?? []" [loading]="loading()" responsiveLayout="scroll">
          <ng-template pTemplate="header">
            <tr>
              <th>Collaborateur</th>
              <th>Type</th>
              <th>Période</th>
              <th>Jours</th>
              <th>État du report</th>
              <th>Motif</th>
              <th style="width: 8rem"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr>
              <td>{{ row.collaboratorName }}</td>
              <td>{{ row.leaveTypeLabel }}</td>
              <td>{{ row.startDate | date:'dd/MM/yyyy' }} → {{ row.endDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ row.days | number:'1.0-2' }}</td>
              <td>
                <p-tag [value]="row.mirrorStateDisplay" [severity]="stateSeverity(row)" />
              </td>
              <td class="muted">{{ row.mirrorMessage || '—' }}</td>
              <td>
                @if (row.canReplay) {
                  <button
                    type="button"
                    pButton
                    label="Rejouer"
                    icon="pi pi-replay"
                    class="p-button-text p-button-sm"
                    [loading]="replaying()"
                    (click)="replayOne(row)"></button>
                } @else {
                  <span class="muted">Régularisation</span>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; margin-bottom: .75rem; }
    .toolbar label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 120px; }
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .summary { display: flex; flex-wrap: wrap; gap: .5rem 1.5rem; font-size: .875rem; }
    .summary .warn { color: #b45309; }
    .muted { color: var(--color-text-muted, #64748b); font-size: .8rem; }
  `]
})
export class FirmLeavesReconciliationComponent implements OnInit {
  private readonly api = inject(FirmLeavesService);
  private readonly toast = inject(ToastService);

  year = new Date().getFullYear();
  yearOptions = Array.from({ length: 6 }, (_, i) => new Date().getFullYear() - i);

  readonly loading = signal(false);
  readonly replaying = signal(false);
  readonly report = signal<FirmLeaveReconciliation | null>(null);

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading.set(true);
    this.api.getReconciliation(this.year).subscribe({
      next: r => { this.report.set(r.data ?? null); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Rapprochement',
          detail: err?.error?.message || 'Chargement impossible.'
        });
      }
    });
  }

  replayableCount(): number {
    return (this.report()?.pending ?? []).filter(r => r.canReplay).length;
  }

  stateSeverity(row: FirmLeaveReconciliationRow): 'warn' | 'danger' | 'secondary' {
    if (row.mirrorState === FIRM_LEAVE_MIRROR_STATE.blockedFrozenPayroll) return 'warn';
    if (row.mirrorState === FIRM_LEAVE_MIRROR_STATE.failed) return 'danger';
    return 'secondary';
  }

  replayAll(): void { this.runReplay(); }

  replayOne(row: FirmLeaveReconciliationRow): void { this.runReplay(row.leaveRequestId); }

  private runReplay(leaveRequestId?: string): void {
    this.replaying.set(true);
    this.api.replayPayrollMirror(this.year, leaveRequestId).subscribe({
      next: res => {
        this.replaying.set(false);
        const data = res.data;
        if (data) {
          const allDone = data.succeeded === data.replayed && data.replayed > 0;
          this.toast.add({
            severity: allDone ? 'success' : 'warn',
            summary: 'Rejeu du report',
            detail: `${data.succeeded}/${data.replayed} report(s) aboutis.`
              + (data.messages.length ? ` ${data.messages[0]}` : ''),
            life: allDone ? 5000 : 10000
          });
        }
        this.reload();
      },
      error: err => {
        this.replaying.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Rejeu du report',
          detail: err?.error?.message || 'Rejeu impossible.'
        });
      }
    });
  }
}
