import { Component, Input, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { MessageModule } from 'primeng/message';
import { finalize } from 'rxjs';
import { PayrollService, PayrollProrataPreview } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  PayrollSectionComponent,
  PayrollStatGridComponent,
  PayrollEmptyStateComponent,
  PayrollAmountPipe,
  formatPayrollAmount,
  type PayrollStatItem
} from '../shared';

@Component({
  selector: 'app-payroll-prorata-preview-panel',
  standalone: true,
  imports: [
    CommonModule,
    TableModule,
    TooltipModule,
    MessageModule,
    ButtonComponent,
    PayrollSectionComponent,
    PayrollStatGridComponent,
    PayrollEmptyStateComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-payroll-section title="Prorata du mois" icon="pi-calendar-minus">
      <div class="payroll-toolbar mb-3">
        <app-button variant="primary" icon="pi-eye" iconPos="left" (click)="load()" [disabled]="loading()">
          Prévisualiser le prorata
        </app-button>
      </div>

      @if (!preview() && !loading() && !loadedOnce()) {
        <p class="payroll-info-text">Cliquez sur « Prévisualiser le prorata » pour estimer les retenues avant calcul.</p>
      }

      @if (preview() && !preview()!.isEnabled) {
        <p-message
          severity="warn"
          text="Le prorata automatique est désactivé sur l'exercice. Activez-le dans Paramètres de paie → Conformité."
          styleClass="mb-3 w-full" />
      }

      @if (loading()) {
        <app-payroll-empty-state [loading]="true" [skeletonColumns]="6" />
      } @else if (preview()) {
        <app-payroll-stat-grid [items]="summaryStats()" class="mb-4" />

        @if (preview()!.lines.length === 0) {
          <app-payroll-empty-state
            icon="pi-check-circle"
            title="Aucune retenue prorata"
            description="Aucun salarié concerné par une absence, suspension ou sortie sur ce mois." />
        } @else {
          <p-table [value]="preview()!.lines" styleClass="p-datatable-sm">
            <ng-template pTemplate="header">
              <tr>
                <th>Salarié</th>
                <th class="text-right">Jours travaillés</th>
                <th class="text-right">Jours non travaillés</th>
                <th class="text-right">Retenue</th>
                <th>Motif</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-l>
              <tr>
                <td>
                  {{ l.employeeName }}
                  <span class="text-secondary">({{ l.employeeNumber }})</span>
                  @if (l.warnings?.length) {
                    <i class="pi pi-exclamation-triangle text-warning ml-2"
                       [pTooltip]="l.warnings.join(' — ')"
                       tooltipPosition="top"></i>
                  }
                </td>
                <td class="text-right">{{ l.workedDays }}</td>
                <td class="text-right">{{ l.nonWorkedDays }}</td>
                <td class="text-right">{{ l.deductionAmount | payrollAmount }}</td>
                <td>{{ l.reason }}</td>
              </tr>
            </ng-template>
          </p-table>
        }
      }
    </app-payroll-section>
  `,
  styles: [`
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mb-4 { margin-bottom: var(--spacing-6); display: block; }
    .ml-2 { margin-left: var(--spacing-2); }
    .text-secondary { color: var(--color-text-secondary); font-size: var(--font-size-sm); }
    .text-warning { color: var(--color-warning-600); }
  `]
})
export class PayrollProrataPreviewPanelComponent {
  @Input({ required: true }) runId!: string;

  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);

  preview = signal<PayrollProrataPreview | null>(null);
  loading = signal(false);
  loadedOnce = signal(false);

  summaryStats = computed((): PayrollStatItem[] => {
    const p = this.preview();
    if (!p) return [];
    return [
      { label: 'Salariés', value: p.employeeCount, icon: 'pi-users', variant: 'primary' },
      { label: 'Total retenues', value: formatPayrollAmount(p.totalDeduction, false), icon: 'pi-minus-circle', variant: 'warning', featured: true }
    ];
  });

  load(): void {
    if (!this.runId) return;
    this.loading.set(true);
    this.payroll.getProrataPreview(this.runId).pipe(
      finalize(() => {
        this.loading.set(false);
        this.loadedOnce.set(true);
      })
    ).subscribe({
      next: res => this.preview.set(res.data ?? null),
      error: () => {
        this.preview.set(null);
        this.toast.add({ severity: 'error', summary: 'Prorata', detail: 'Prévisualisation impossible.' });
      }
    });
  }
}
