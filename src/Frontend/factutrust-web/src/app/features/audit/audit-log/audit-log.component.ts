import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableLazyLoadEvent } from 'primeng/table';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import {
  AuditService,
  AuditLogEntryDto,
  AuditLogDetailDto
} from '../services/audit.service';
import { ToastService } from '@core/services/toast.service';
import {
  auditActionLabel,
  auditActionSeverity,
  auditEntityTypeLabel,
  auditEntityTypeSelectOptions,
  auditActionSelectOptions,
  AuditSelectOption
} from '../audit-action-labels';

export interface VerifyIntegrityResult {
  isValid: boolean;
  entryCount: number;
  firstBrokenEntryId?: string | null;
  firstFailureReason?: string | null;
  duplicatePreviousHashGroupCount?: number;
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    MessageModule,
    TagModule,
    TooltipModule,
    CalendarModule,
    DropdownModule,
    DialogModule,
    InputTextModule,
    PageHeaderComponent
  ],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  private readonly api = inject(AuditService);
  private readonly toast = inject(ToastService);

  readonly rows = signal<AuditLogEntryDto[]>([]);
  readonly totalRecords = signal(0);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly verifyResult = signal<VerifyIntegrityResult | null>(null);
  readonly verifyLoading = signal(false);
  readonly verifyError = signal<string | null>(null);

  readonly detailOpen = signal(false);
  readonly detailLoading = signal(false);
  readonly detailEntry = signal<AuditLogDetailDto | null>(null);

  readonly entityOptions: AuditSelectOption[] = auditEntityTypeSelectOptions();
  readonly actionOptions: AuditSelectOption[] = auditActionSelectOptions();

  /** Filtres (dates par défaut : 30 derniers jours) */
  fromDate: Date | null = null;
  toDate: Date | null = null;
  selectedEntityType: string | null = null;
  selectedAction: string | null = null;
  userIdFilter = '';

  /** Synchronisé avec la table lazy (pagination serveur) */
  first = 0;
  /** Taille de page courante (alignée sur le sélecteur PrimeNG). */
  tableRows = 25;

  ngOnInit(): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 30);
    from.setHours(0, 0, 0, 0);
    to.setHours(23, 59, 59, 999);
    this.fromDate = from;
    this.toDate = to;
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const rows = event.rows ?? this.tableRows;
    this.tableRows = rows;
    const page = Math.floor((event.first ?? 0) / rows) + 1;
    this.loadPage(page, rows);
  }

  applyFilters(): void {
    this.first = 0;
    this.loadPage(1, this.tableRows);
  }

  resetFilters(): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 30);
    from.setHours(0, 0, 0, 0);
    to.setHours(23, 59, 59, 999);
    this.fromDate = from;
    this.toDate = to;
    this.selectedEntityType = null;
    this.selectedAction = null;
    this.userIdFilter = '';
    this.first = 0;
    this.loadPage(1, this.tableRows);
  }

  onDetailVisibleChange(visible: boolean): void {
    this.detailOpen.set(visible);
    if (!visible) this.detailEntry.set(null);
  }

  private loadPage(page: number, pageSize: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getLogs({
        from: this.fromDate ?? undefined,
        to: this.toDate ?? undefined,
        action: this.selectedAction ?? undefined,
        entityType: this.selectedEntityType ?? undefined,
        userId: this.userIdFilter.trim() || undefined,
        page,
        pageSize
      })
      .subscribe({
        next: r => {
          this.loading.set(false);
          if (r.success && r.data) {
            this.rows.set(r.data.items);
            this.totalRecords.set(r.data.totalCount);
            this.first = (r.data.page - 1) * r.data.pageSize;
          } else {
            this.error.set(r.error ?? r.message ?? 'Impossible de charger le journal d’audit.');
          }
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Erreur réseau. Réessayez plus tard.');
        }
      });
  }

  verify(): void {
    this.verifyLoading.set(true);
    this.verifyError.set(null);
    this.api.verifyIntegrity().subscribe({
      next: r => {
        this.verifyLoading.set(false);
        if (r.success && r.data) {
          this.verifyResult.set({
            isValid: r.data.isValid,
            entryCount: r.data.entryCount,
            firstBrokenEntryId: r.data.firstBrokenEntryId,
            firstFailureReason: r.data.firstFailureReason,
            duplicatePreviousHashGroupCount: r.data.duplicatePreviousHashGroupCount
          });
        } else {
          this.verifyError.set(r.error ?? r.message ?? 'Vérification impossible.');
        }
      },
      error: () => {
        this.verifyLoading.set(false);
        this.verifyError.set('Erreur réseau lors de la vérification.');
      }
    });
  }

  integrityValidMessage(count: number): string {
    return `Chaîne d'intégrité valide — ${count} entrée(s) vérifiée(s).`;
  }

  integrityBrokenMessage(
    count: number,
    firstBrokenId: string | null | undefined,
    reason?: string | null,
    dupGroups?: number
  ): string {
    const idPart = firstBrokenId ? ` — première anomalie détectée sur l'entrée ${firstBrokenId}.` : '';
    const reasonPart =
      reason === 'IntegrityMismatch'
        ? ' Motif : empreinte incohérente avec le contenu enregistré.'
        : reason === 'ChainMismatch'
          ? ' Motif : lien avec l’entrée précédente incorrect.'
          : '';
    const dupPart =
      dupGroups != null && dupGroups > 0
        ? ` ${dupGroups} valeur(s) de chaîne antérieure dupliquée(s) — souvent lié à des écritures concurrentes avant correction.`
        : '';
    return `Chaîne d'intégrité rompue — ${count} entrée(s) analysée(s).${idPart}${reasonPart}${dupPart}`;
  }

  isBrokenHighlight(rowId: string): boolean {
    const vr = this.verifyResult();
    if (!vr || vr.isValid || !vr.firstBrokenEntryId) return false;
    return rowId.toLowerCase() === vr.firstBrokenEntryId.toLowerCase();
  }

  actionLabel(action: string): string {
    return auditActionLabel(action);
  }

  actionSeverity(action: string): ReturnType<typeof auditActionSeverity> {
    return auditActionSeverity(action);
  }

  entityLabel(entityType: string): string {
    return auditEntityTypeLabel(entityType);
  }

  shortId(id: string): string {
    const s = id.replace(/-/g, '');
    return s.length <= 8 ? id : `${s.slice(0, 8)}…`;
  }

  async copyId(id: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(id);
      this.toast.add({
        severity: 'success',
        summary: 'Copié',
        detail: 'Identifiant copié dans le presse-papiers.',
        life: 2500
      });
    } catch {
      this.toast.add({
        severity: 'error',
        summary: 'Copie impossible',
        detail: 'Autorisez l’accès au presse-papiers ou copiez manuellement.',
        life: 4000
      });
    }
  }

  openDetail(row: AuditLogEntryDto): void {
    this.detailOpen.set(true);
    this.detailLoading.set(true);
    this.detailEntry.set(null);
    this.api.getById(row.id).subscribe({
      next: r => {
        this.detailLoading.set(false);
        if (r.success && r.data) this.detailEntry.set(r.data);
        else {
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: r.error ?? r.message ?? 'Chargement impossible.',
            life: 4000
          });
          this.detailOpen.set(false);
        }
      },
      error: () => {
        this.detailLoading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Réseau',
          detail: 'Impossible de charger le détail.',
          life: 4000
        });
        this.detailOpen.set(false);
      }
    });
  }

  formatJsonBlob(s: string | null | undefined): string {
    if (s == null || s === '') return '—';
    try {
      return JSON.stringify(JSON.parse(s), null, 2);
    } catch {
      return s;
    }
  }
}
