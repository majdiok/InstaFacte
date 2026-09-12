import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { StudioAiBuildService } from '../../studio-ai-build.service';
import { StudioPageShellComponent } from '../../shared/studio-page-shell.component';
import { studioBreadcrumb } from '../../shared/studio-breadcrumb.util';
import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';
import { StudioAiPlanKind, StudioAiPlanListItemDto, StudioAiPlanStatus, StudioPagedResult } from '../studio-ai.models';
import { planKindLabel, planStatusLabel, planStatusSeverity } from '../rail/studio-ai-rail.util';

export const STUDIO_AI_PROJECTS_PAGE_SIZE = 20;

interface FilterOption<T extends string> { label: string; value: T | null; }

const STATUS_ORDER: StudioAiPlanStatus[] = ['Pending', 'Executing', 'Completed', 'Failed', 'Cancelled', 'Expired'];
const KIND_ORDER: StudioAiPlanKind[] = ['CreateSystem', 'CreateApp', 'Amendment', 'View', 'RecordView', 'Report', 'Workflow'];

/**
 * « Mes projets » (`/studio/ai/projects`) : toutes les générations Studio IA du cabinet, paginées
 * côté serveur (`GET api/ai/studio/plans`, 20 par page), filtrables par statut et genre.
 * « Reprendre » rouvre un plan encore à valider dans l'atelier (`/studio/ai?plan=<id>`) ;
 * « Ouvrir le système » mène au hub du système créé.
 */
@Component({
  selector: 'app-studio-ai-projects-page',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, ButtonModule, PaginatorModule, SelectModule, TableModule, TagModule, TooltipModule, StudioPageShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-studio-page-shell [title]="labels.title" [subtitle]="labels.subtitle" [breadcrumbs]="breadcrumbs">
      <a
        studioActions
        pButton
        [label]="labels.backToStudio"
        icon="fa-solid fa-wand-magic-sparkles"
        severity="secondary"
        [outlined]="true"
        size="small"
        routerLink="/studio/ai"></a>

      <div class="sap-projects">
        <div class="sap-projects__filters">
          <p-select
            inputId="sap-status"
            [options]="statusOptions"
            [ngModel]="status()"
            (ngModelChange)="setStatus($event)"
            optionLabel="label"
            optionValue="value"
            [placeholder]="labels.filters.status"
            [ariaLabel]="labels.filters.status"
            appendTo="body"
            panelStyleClass="studio-theme"
            styleClass="sap-projects__select" />
          <p-select
            inputId="sap-kind"
            [options]="kindOptions"
            [ngModel]="kind()"
            (ngModelChange)="setKind($event)"
            optionLabel="label"
            optionValue="value"
            [placeholder]="labels.filters.kind"
            [ariaLabel]="labels.filters.kind"
            appendTo="body"
            panelStyleClass="studio-theme"
            styleClass="sap-projects__select" />
          <span class="sap-projects__count" data-testid="projects-count">{{ countText() }}</span>
        </div>

        @if (error(); as message) {
          <div class="sap-projects__error" role="alert">
            <i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i>
            <span>{{ message }}</span>
            <p-button [label]="retryLabel" [link]="true" size="small" (onClick)="load()" />
          </div>
        }

        <p-table
          [value]="items()"
          [loading]="loading()"
          dataKey="id"
          styleClass="p-datatable-sm sap-projects__table"
          [tableStyle]="{ 'min-width': '48rem' }">
          <ng-template #header>
            <tr>
              <th>{{ labels.columns.title }}</th>
              <th>{{ labels.columns.kind }}</th>
              <th>{{ labels.columns.status }}</th>
              <th>{{ labels.columns.createdAt }}</th>
              <th>{{ labels.columns.expiresAt }}</th>
              <th>{{ labels.columns.system }}</th>
              <th class="sap-projects__actions-col">{{ labels.columns.actions }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-item>
            <tr [attr.data-plan-id]="item.id">
              <td class="sap-projects__title">{{ item.title || planKindLabel(item.kind) }}</td>
              <td>{{ planKindLabel(item.kind) }}</td>
              <td><p-tag [value]="planStatusLabel(item.status)" [severity]="planStatusSeverity(item.status)" /></td>
              <td>{{ item.createdAt | date: 'dd/MM/yyyy HH:mm' }}</td>
              <td>{{ item.status === 'Pending' && item.expiresAt ? (item.expiresAt | date: 'dd/MM/yyyy HH:mm') : '—' }}</td>
              <td class="sap-projects__system">{{ item.systemKey || '—' }}</td>
              <td class="sap-projects__actions">
                @if (item.status === 'Pending') {
                  <a
                    pButton
                    [label]="labels.resume"
                    icon="fa-solid fa-play"
                    size="small"
                    [routerLink]="['/studio/ai']"
                    [queryParams]="{ plan: item.id }"
                    data-action="resume"></a>
                } @else if (item.systemKey) {
                  <a
                    pButton
                    [label]="labels.openSystem"
                    icon="fa-solid fa-arrow-up-right-from-square"
                    size="small"
                    severity="secondary"
                    [outlined]="true"
                    [routerLink]="['/studio/systems', item.systemKey]"
                    data-action="open-system"></a>
                }
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="7" class="sap-projects__empty" data-testid="projects-empty">
                @if (!loading() && !error()) { {{ labels.empty }} }
              </td>
            </tr>
          </ng-template>
        </p-table>

        @if (totalCount() > pageSize) {
          <p-paginator
            [first]="(page() - 1) * pageSize"
            [rows]="pageSize"
            [totalRecords]="totalCount()"
            [showCurrentPageReport]="false"
            (onPageChange)="onPage($event)" />
        }
      </div>
    </app-studio-page-shell>
  `,
  styles: [`
    .sap-projects {
      display: flex; flex-direction: column; gap: 1rem; padding: 1rem;
      background: var(--color-surface, #fff); border: 1px solid var(--color-neutral-200, #e5e7eb); border-radius: var(--radius-lg, 12px);
    }
    .sap-projects__filters { display: flex; flex-wrap: wrap; align-items: center; gap: 0.75rem; }
    .sap-projects__count { margin-left: auto; font-size: 0.8125rem; color: var(--color-neutral-500, #6b7280); }
    .sap-projects__title { font-weight: 500; }
    .sap-projects__system { font-family: ui-monospace, monospace; font-size: 0.8125rem; color: var(--color-neutral-600, #4b5563); }
    .sap-projects__actions { white-space: nowrap; text-align: right; }
    .sap-projects__actions a { text-decoration: none; }
    .sap-projects__actions-col { text-align: right; }
    .sap-projects__empty { text-align: center; padding: 2rem 1rem; color: var(--color-neutral-500, #6b7280); }
    .sap-projects__error {
      display: flex; align-items: center; gap: 0.5rem; padding: 0.75rem 1rem; border-radius: 8px;
      background: var(--color-danger-50, #fef2f2); color: var(--color-danger-700, #b91c1c); font-size: 0.875rem;
    }
    .sap-projects__error span { flex: 1 1 auto; }
  `]
})
export class StudioAiProjectsPageComponent {
  private readonly builds = inject(StudioAiBuildService);

  readonly labels = STUDIO_AI_LABELS.history;
  readonly retryLabel = STUDIO_AI_LABELS.conversation.retry;
  readonly breadcrumbs = studioBreadcrumb({ label: 'Assistant IA', route: '/studio/ai' }, { label: STUDIO_AI_LABELS.history.title });
  readonly pageSize = STUDIO_AI_PROJECTS_PAGE_SIZE;

  readonly statusOptions: FilterOption<StudioAiPlanStatus>[] = [
    { label: STUDIO_AI_LABELS.history.filters.allStatuses, value: null },
    ...STATUS_ORDER.map(value => ({ label: planStatusLabel(value), value }))
  ];
  readonly kindOptions: FilterOption<StudioAiPlanKind>[] = [
    { label: STUDIO_AI_LABELS.history.filters.allKinds, value: null },
    ...KIND_ORDER.map(value => ({ label: planKindLabel(value), value }))
  ];

  readonly status = signal<StudioAiPlanStatus | null>(null);
  readonly kind = signal<StudioAiPlanKind | null>(null);
  readonly page = signal(1);
  readonly items = signal<StudioAiPlanListItemDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly countText = computed(() => formatLabel(this.labels.count, { count: this.totalCount() }));

  readonly planKindLabel = planKindLabel;
  readonly planStatusLabel = planStatusLabel;
  readonly planStatusSeverity = planStatusSeverity;

  private requestSeq = 0;

  constructor() {
    this.load();
  }

  setStatus(value: StudioAiPlanStatus | null): void {
    this.status.set(value);
    this.page.set(1);
    this.load();
  }

  setKind(value: StudioAiPlanKind | null): void {
    this.kind.set(value);
    this.page.set(1);
    this.load();
  }

  onPage(event: PaginatorState): void {
    const next = (event.page ?? 0) + 1;
    if (next === this.page()) return;
    this.page.set(next);
    this.load();
  }

  load(): void {
    const seq = ++this.requestSeq;
    this.loading.set(true);
    this.error.set(null);
    this.builds
      .listPlans({ status: this.status(), kind: this.kind(), page: this.page(), pageSize: this.pageSize })
      .subscribe({
        next: res => {
          if (seq !== this.requestSeq) return;
          const data: StudioPagedResult<StudioAiPlanListItemDto> | null = res?.success ? res.data : null;
          this.items.set(data?.items ?? []);
          this.totalCount.set(data?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          if (seq !== this.requestSeq) return;
          this.items.set([]);
          this.totalCount.set(0);
          this.loading.set(false);
          this.error.set(this.labels.loadFailed);
        }
      });
  }
}
