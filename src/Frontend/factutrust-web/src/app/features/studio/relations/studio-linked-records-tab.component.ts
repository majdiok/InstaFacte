import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { SkeletonTableComponent } from '@shared/components/skeleton/skeleton-table.component';
import { STUDIO_RUNTIME_LABELS } from '../shared/studio-runtime-labels';
import { EntityRelationDto } from './studio-relations.models';
import { CustomRecord } from '../studio.models';
import { StudioLinkedRecordsService, LinkedRecordRow, primaryLabel } from './studio-linked-records.service';

interface TargetOption { id: string; label: string; }

/**
 * Onglet « Liés » de la fiche d'un enregistrement (2.5e2) : liste les liens N-N via la jonction,
 * ajout/retrait conditionnés à `canWrite` (le service compose les endpoints CRUD de la jonction ;
 * une paire existante ⇒ 409 `record.duplicate_link` rendu en ligne). Le libellé cible est résolu
 * en une passe par `searchTargets` (les records de jonction ne portent que les ids).
 */
/** Projection jonction → ligne affichée (libellé résolu ou repli sur l'id tronqué). */
function toRow(p: { junction: CustomRecord; targetId: string }, targetLabel: string): LinkedRecordRow {
  return {
    junctionRecordId: p.junction.id,
    targetId: p.targetId,
    targetLabel,
    rowVersion: p.junction.rowVersion,
    createdAt: p.junction.createdAt
  };
}

@Component({
  selector: 'app-studio-linked-records-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe, ButtonModule, SelectModule, SkeletonTableComponent],
  template: `
    <section class="slinked" [attr.aria-label]="labels.linked.tabLabel">
      @if (error(); as message) {
        <div class="studio-row studio-row-section" role="alert" data-testid="linked-error">{{ message }}</div>
      }
      @if (truncated()) {
        <div class="studio-row" role="status" data-testid="linked-truncated">{{ labels.linked.truncated }}</div>
      }

      @if (canWrite()) {
        <div class="slinked__addbar">
          <p-select class="slinked__search" [options]="options()" [ngModel]="selectedTarget()"
            (ngModelChange)="selectedTarget.set($event)" optionLabel="label" optionValue="id" [filter]="true"
            filterBy="label" (onFilter)="onFilter($event)" [placeholder]="relation().targetLabel" appendTo="body"
            panelStyleClass="studio-theme" data-testid="linked-search" />
          <p-button [label]="labels.linked.add" icon="pi pi-link" size="small" [disabled]="!selectedTarget() || saving()"
            (onClick)="add()" data-testid="linked-add" />
        </div>
      }

      @if (loading()) {
        <app-skeleton-table [columns]="[{width:'60%'},{width:'30%'},{width:'10%'}]" [rows]="3" />
      } @else {
        @for (row of rows(); track row.junctionRecordId) {
          <div class="studio-row" [attr.data-testid]="'linked-row-' + row.targetId">
            <span class="studio-grow">{{ row.targetLabel }}</span>
            <span class="studio-muted">{{ row.createdAt | date:'dd/MM/yyyy' }}</span>
            @if (canWrite()) {
              <p-button icon="pi pi-times" [text]="true" size="small" severity="danger" [disabled]="saving()"
                [attr.aria-label]="labels.linked.remove" (onClick)="remove(row)" data-testid="linked-remove" />
            }
          </div>
        } @empty {
          <p class="studio-muted" data-testid="linked-empty">Aucune fiche liée.</p>
        }
      }
    </section>
  `,
  // studio-layout.scss fournit .studio-row / .studio-row-section / .studio-grow / .studio-muted
  // (encapsulation émulée : sans styleUrl ici, les lignes « Liés » n'étaient pas stylées).
  styleUrl: '../shared/studio-layout.scss',
  styles: [`
    .slinked__addbar { display: flex; gap: var(--spacing-2); margin-bottom: var(--spacing-3); }
    .slinked__search { flex: 1; max-width: 24rem; }
  `]
})
export class StudioLinkedRecordsTabComponent implements OnInit {
  readonly relation = input.required<EntityRelationDto>();
  readonly recordId = input.required<string>();
  readonly canWrite = input(false);

  private readonly linked = inject(StudioLinkedRecordsService);
  private readonly toast = inject(MessageService);

  readonly labels = STUDIO_RUNTIME_LABELS;
  readonly pageSize = 50;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<LinkedRecordRow[]>([]);
  readonly options = signal<TargetOption[]>([]);
  readonly selectedTarget = signal<string | null>(null);
  readonly truncated = signal(false);

  ngOnInit(): void {
    this.refresh();
    this.searchTargets(null);
  }

  refresh(): void {
    this.loading.set(true);
    this.linked.listLinks(this.relation(), this.recordId(), 1, this.pageSize).subscribe({
      next: res => {
        this.loading.set(false);
        if (!res.success) { this.error.set(res.message || this.labels.linked.error); return; }
        const junctions = res.data.items ?? [];
        this.truncated.set((res.data.totalCount ?? 0) > this.pageSize);
        this.resolveLabels(junctions);
      },
      error: () => { this.loading.set(false); this.error.set(this.labels.linked.error); }
    });
  }

  /** Résout les libellés cibles en une passe (`searchTargets` filtre côté serveur si besoin). */
  private resolveLabels(junctions: CustomRecord[]): void {
    const targetField = this.relation().junctionTargetFieldKey;
    if (!targetField) { this.rows.set([]); return; }
    const targetIds = junctions
      .map(j => ({ junction: j, targetId: String(j.data?.[targetField] ?? '') }))
      .filter(p => p.targetId.length > 0);
    this.linked.searchTargets(this.relation(), null, 200).subscribe({
      next: res => {
        const labelsById = new Map<string, string>();
        if (res.success) for (const r of res.data.items ?? []) labelsById.set(r.id, primaryLabel(r, []));
        this.rows.set(targetIds.map(p => toRow(p, labelsById.get(p.targetId) ?? p.targetId.slice(0, 8))));
      },
      error: () => this.rows.set(targetIds.map(p => toRow(p, p.targetId.slice(0, 8))))
    });
  }

  searchTargets(search: string | null): void {
    this.linked.searchTargets(this.relation(), search).subscribe({
      next: res => {
        if (!res.success) return;
        this.options.set((res.data.items ?? []).map(r => ({ id: r.id, label: primaryLabel(r, []) })));
      },
      error: () => { /* liste de choix indisponible : le tableau reste fonctionnel */ }
    });
  }

  onFilter(event: { filter?: string }): void {
    this.searchTargets(event.filter ?? null);
  }

  add(): void {
    const targetId = this.selectedTarget();
    if (!targetId || !this.canWrite()) return;
    this.saving.set(true);
    this.error.set(null);
    this.linked.link(this.relation(), this.recordId(), targetId).subscribe({
      next: res => {
        this.saving.set(false);
        if (!res.success) { this.error.set(res.message || this.labels.linked.error); return; }
        this.selectedTarget.set(null);
        this.refresh();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(err.status === 409 && err.error?.code === 'record.duplicate_link'
          ? this.labels.linked.duplicate
          : (typeof err.error?.message === 'string' ? err.error.message : this.labels.linked.error));
      }
    });
  }

  remove(row: LinkedRecordRow): void {
    if (!this.canWrite()) return;
    this.saving.set(true);
    this.error.set(null);
    this.linked.unlink(this.relation(), row.junctionRecordId).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: this.labels.linked.tabLabel, detail: this.labels.linked.remove });
        this.rows.update(rows => rows.filter(r => r.junctionRecordId !== row.junctionRecordId));
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(typeof err.error?.message === 'string' ? err.error.message : this.labels.linked.error);
      }
    });
  }
}
