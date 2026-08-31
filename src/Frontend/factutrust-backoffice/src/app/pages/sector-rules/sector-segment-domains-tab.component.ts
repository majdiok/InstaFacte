import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type { SectorDomainDto, SectorSegmentDto, SegmentDomainLinkDto } from '@core/models/sector-rules.models';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

/** Code du domaine « Autre » — toujours disponible, non modifiable (cf. mockup associations). */
const OTHER_DOMAIN_CODE = 'autre';

/** Phase 2 (WP-F7) — Onglet « Associations » : matrice domaines × segments. */
@Component({
  selector: 'app-sector-segment-domains-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, CheckboxModule, TooltipModule],
  template: `
    <p class="hint">{{ t('assoc.hint') }}</p>
    <div class="matrix-wrap">
      <table class="matrix" role="table" aria-label="Associations segment / domaine">
        <thead>
          <tr>
            <th scope="col" class="col-domain">{{ t('assoc.col.domain') }}</th>
            @for (seg of segments; track seg.code) {
              <th scope="col" class="col-segment">
                <span>{{ seg.label }}</span>
                <span class="order-hint">Ordre {{ seg.sortOrder }}</span>
              </th>
            }
          </tr>
        </thead>
        <tbody>
          @for (dom of domains; track dom.code) {
            <tr>
              <th scope="row" class="row-domain">
                {{ dom.label }}
                <code class="cell-mono">{{ dom.code }}</code>
              </th>
              @for (seg of segments; track seg.code) {
                <td class="cell-checkbox">
                  @if (dom.code === otherDomainCode) {
                    <p-checkbox
                      [binary]="true"
                      [ngModel]="true"
                      [disabled]="true"
                      [pTooltip]="t('assoc.locked.title')"
                      tooltipPosition="top"
                    />
                  } @else {
                    <p-checkbox
                      [binary]="true"
                      [ngModel]="isChecked(seg.code, dom.code)"
                      (ngModelChange)="toggle(seg.code, dom.code, $event)"
                      [disabled]="!canManage()"
                    />
                  }
                </td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>
    <div class="save-row">
      <p-button
        [label]="t('assoc.actions.save')"
        icon="pi pi-check"
        severity="primary"
        [loading]="busy()"
        [disabled]="!canManage() || !dirty()"
        (onClick)="save()"
      />
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .hint { color: var(--ft-text-muted, #8b949e); font-size: 0.88rem; margin: 0 0 0.9rem; }
      .matrix-wrap { overflow-x: auto; border: 1px solid var(--ft-border, #30363d); border-radius: 8px; }
      .matrix { border-collapse: collapse; width: 100%; font-size: 0.85rem; }
      .matrix th, .matrix td { padding: 0.55rem 0.75rem; border-bottom: 1px solid var(--ft-border, #30363d); }
      .col-domain { text-align: left; min-width: 14rem; }
      .col-segment { text-align: center; min-width: 8rem; }
      .col-segment span { display: block; }
      .order-hint { font-size: 0.7rem; color: var(--ft-text-muted, #8b949e); font-weight: 400; }
      .row-domain { text-align: left; font-weight: 500; color: var(--ft-text); white-space: nowrap; }
      .cell-mono { display: block; font-size: 0.72rem; color: var(--ft-text-muted, #8b949e); }
      .cell-checkbox { text-align: center; }
      .save-row { display: flex; justify-content: flex-end; margin-top: 1rem; }
    `
  ]
})
export class SectorSegmentDomainsTabComponent implements OnChanges {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) segments: SectorSegmentDto[] = [];
  @Input({ required: true }) domains: SectorDomainDto[] = [];
  @Input({ required: true }) segmentDomains: SegmentDomainLinkDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly otherDomainCode = OTHER_DOMAIN_CODE;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));
  readonly busy = signal(false);

  /** État local (modifiable) : segmentCode -> Set<domainCode>. */
  private state = new Map<string, Set<string>>();
  /** Snapshot initial pour calculer `dirty()` et le diff à sauvegarder. */
  private baseline = new Map<string, Set<string>>();
  readonly version = signal(0);

  ngOnChanges(): void {
    this.state = new Map(this.segmentDomains.map(l => [l.segmentCode, new Set(l.domainCodes)]));
    this.baseline = new Map(this.segmentDomains.map(l => [l.segmentCode, new Set(l.domainCodes)]));
    this.version.update(v => v + 1);
  }

  isChecked(segmentCode: string, domainCode: string): boolean {
    this.version();
    return this.state.get(segmentCode)?.has(domainCode) ?? false;
  }

  toggle(segmentCode: string, domainCode: string, checked: boolean): void {
    const set = this.state.get(segmentCode) ?? new Set<string>();
    if (checked) {
      set.add(domainCode);
    } else {
      set.delete(domainCode);
    }
    this.state.set(segmentCode, set);
    this.version.update(v => v + 1);
  }

  readonly dirty = computed(() => {
    this.version();
    for (const seg of this.segments) {
      const current = [...(this.state.get(seg.code) ?? new Set())].sort().join(',');
      const original = [...(this.baseline.get(seg.code) ?? new Set())].sort().join(',');
      if (current !== original) return true;
    }
    return false;
  });

  save(): void {
    const requests = this.segments
      .filter(seg => {
        const current = [...(this.state.get(seg.code) ?? new Set())].sort().join(',');
        const original = [...(this.baseline.get(seg.code) ?? new Set())].sort().join(',');
        return current !== original;
      })
      .map(seg => {
        const codes = new Set(this.state.get(seg.code) ?? new Set<string>());
        codes.add(OTHER_DOMAIN_CODE);
        return this.api.setSegmentDomains(seg.code, [...codes]);
      });

    if (requests.length === 0) return;

    this.busy.set(true);
    forkJoin(requests).subscribe({
      next: results => {
        this.busy.set(false);
        if (results.every(r => r.success)) {
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['assoc.toast.save.success'] });
          this.changed.emit();
        } else {
          const failed = results.find(r => !r.success);
          this.toastError(failed?.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.toastError((err as { error?: { message?: string } })?.error?.message);
      }
    });
  }

  private toastError(message?: string | null): void {
    this.toast.add({ severity: 'error', summary: SECTOR_RULES_FR['toast.error.title'], detail: message ?? SECTOR_RULES_FR['toast.error.generic'] });
  }
}
