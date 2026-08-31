import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type {
  ModuleDependencyDto,
  SectorModuleRuleDto,
  SectorDomainDto,
  SectorSegmentDto
} from '@core/models/sector-rules.models';
import { CORE_MODULE_IDS, MODULE_CATALOG, SECTOR_ELIGIBLE_MODULES, moduleLabel } from '@core/models/module-catalog';

import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

type RuleTarget = { kind: 'segment'; id: string; code: string; label: string } | { kind: 'domain'; id: string; code: string; label: string };

/**
 * Phase 2 (WP-F7) — Onglet « Règles de modules ».
 *
 * Sélecteur segment/domaine + tableau des règles actuelles + dialogue d'édition avec chips
 * togglables et aperçu en direct (mockup `rules-admin-module-rule-dialog.html`). Les règles sont
 * individually des lignes CRUD backend (`ruleKind` chaîne + `segmentId`/`domainId` GUID) : la
 * sauvegarde calcule un diff (créations POST + désactivations DELETE) plutôt qu'un envoi groupé.
 */
@Component({
  selector: 'app-sector-module-rules-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, DialogModule, FtBadgeComponent],
  template: `
    <section class="rules-section">
      <div class="section-head">
        <div>
          <h3>{{ t('rules.segment.title') }}</h3>
          <p class="muted">{{ t('rules.segment.desc') }}</p>
        </div>
      </div>
      <div class="chips-grid">
        @for (seg of segments; track seg.id) {
          <div class="target-card">
            <div class="target-head">
              <strong>{{ seg.labelFr }}</strong>
              <code class="cell-mono">{{ seg.code }}</code>
            </div>
            <div class="target-modules">
              @for (id of segmentModuleIds(seg.id); track id) {
                <ft-badge tone="accent" size="sm">{{ moduleLabel(id) }}</ft-badge>
              } @empty {
                <span class="muted">{{ t('common.none') }}</span>
              }
            </div>
            <p-button
              [label]="t('rules.edit')"
              icon="pi pi-pencil"
              [text]="true"
              size="small"
              [disabled]="!canManage()"
              (onClick)="openSegmentDialog(seg)"
            />
          </div>
        }
      </div>
    </section>

    <section class="rules-section">
      <div class="section-head">
        <div>
          <h3>{{ t('rules.domain.title') }}</h3>
          <p class="muted">{{ t('rules.domain.desc') }}</p>
        </div>
      </div>
      <div class="chips-grid">
        @for (dom of domains; track dom.id) {
          <div class="target-card">
            <div class="target-head">
              <strong>{{ dom.labelFr }}</strong>
              <code class="cell-mono">{{ dom.code }}</code>
            </div>
            <div class="target-modules">
              @for (id of domainModuleIds(dom.id); track id) {
                <ft-badge tone="info" size="sm">{{ moduleLabel(id) }}</ft-badge>
              } @empty {
                <span class="muted">{{ t('common.none') }}</span>
              }
            </div>
            <p-button
              [label]="t('rules.edit')"
              icon="pi pi-pencil"
              [text]="true"
              size="small"
              [disabled]="!canManage()"
              (onClick)="openDomainDialog(dom)"
            />
          </div>
        }
      </div>
    </section>

    <p-dialog
      [header]="dialogTitle()"
      [(visible)]="dialogVisible"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(46rem, 96vw)' }"
    >
      @if (target(); as tgt) {
        <div class="dialog-target">
          <code class="cell-mono">{{ tgt.code }}</code>
        </div>

        <div class="dialog-block">
          <h4>{{ t('rules.dialog.core.title') }}</h4>
          <p class="muted small">{{ t('rules.dialog.core.hint') }}</p>
          <div class="chip-row" role="group" aria-label="Modules de base">
            @for (m of coreModules; track m.value) {
              <button type="button" class="chip chip--locked" disabled aria-disabled="true" [attr.aria-pressed]="true">
                {{ m.label }}
              </button>
            }
            @for (m of eligibleModules; track m.value) {
              <button
                type="button"
                class="chip"
                [class.chip--selected]="isSelected(m.value)"
                [attr.aria-pressed]="isSelected(m.value)"
                [disabled]="!canManage()"
                (click)="toggleModule(m.value)"
              >
                {{ m.label }}
              </button>
            }
          </div>
        </div>

        <div class="dialog-block preview-card" aria-live="polite">
          <div class="preview-head">
            <h4>{{ t('rules.dialog.preview.title') }}</h4>
            <span class="preview-live"><span class="live-dot" aria-hidden="true"></span>{{ t('rules.dialog.preview.live') }}</span>
          </div>
          <div class="preview-row">
            <span class="preview-label">{{ t('rules.dialog.preview.core') }}</span>
            <div class="preview-badges">
              @for (m of coreModules; track m.value) {
                <ft-badge tone="success" size="sm">{{ m.label }}</ft-badge>
              }
            </div>
          </div>
          <div class="preview-row">
            <span class="preview-label">{{ t('rules.dialog.preview.recommended') }}</span>
            <div class="preview-badges">
              @for (id of selectedModuleIds(); track id) {
                <ft-badge tone="accent" size="sm">{{ moduleLabel(id) }}</ft-badge>
              } @empty {
                <span class="muted small">{{ t('common.none') }}</span>
              }
            </div>
          </div>
          <div class="preview-row">
            <span class="preview-label">{{ t('rules.dialog.preview.optional') }}</span>
            <div class="preview-badges">
              @for (m of unselectedModules(); track m.value) {
                <ft-badge tone="neutral" size="sm">{{ m.label }}</ft-badge>
              }
            </div>
          </div>
          @if (dependencyNote(); as note) {
            <p class="dep-note">
              <i class="pi pi-info-circle" aria-hidden="true"></i>
              {{ note }}
            </p>
          }
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button [label]="t('common.cancel')" [text]="true" severity="secondary" (onClick)="dialogVisible = false" [disabled]="busy()" />
        <p-button [label]="t('rules.confirm.save')" icon="pi pi-check" [loading]="busy()" (onClick)="save()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .rules-section { margin-bottom: 1.75rem; }
      .section-head h3 { margin: 0 0 0.2rem; font-size: 1rem; color: var(--ft-text); }
      .muted { color: var(--ft-text-muted, #8b949e); margin: 0 0 0.9rem; font-size: 0.88rem; }
      .muted.small { font-size: 0.8rem; }
      .chips-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: 0.75rem; }
      .target-card { border: 1px solid var(--ft-border, #30363d); border-radius: 8px; padding: 0.85rem; display: flex; flex-direction: column; gap: 0.5rem; }
      .target-head { display: flex; justify-content: space-between; align-items: baseline; gap: 0.5rem; }
      .target-head strong { color: var(--ft-text); }
      .cell-mono { font-size: 0.72rem; color: var(--ft-text-muted, #8b949e); }
      .target-modules { display: flex; flex-wrap: wrap; gap: 0.3rem; min-height: 1.5rem; }
      .dialog-target { margin-bottom: 0.75rem; }
      .dialog-block { margin-bottom: 1.1rem; }
      .dialog-block h4 { margin: 0 0 0.3rem; font-size: 0.9rem; color: var(--ft-text); }
      .chip-row { display: flex; flex-wrap: wrap; gap: 0.4rem; }
      .chip {
        border: 1px solid var(--ft-border, #30363d);
        background: var(--ft-surface-2, #0d1117);
        color: var(--ft-text-muted, #8b949e);
        border-radius: 999px;
        padding: 0.3rem 0.75rem;
        font-size: 0.8rem;
        cursor: pointer;
      }
      .chip--locked { opacity: 0.6; cursor: not-allowed; }
      .chip--selected { background: var(--ft-accent-surface, rgba(88,166,255,0.16)); color: var(--ft-accent, #58a6ff); border-color: var(--ft-accent, #58a6ff); }
      .chip:disabled { cursor: not-allowed; }
      .preview-card { border: 1px solid var(--ft-border, #30363d); border-radius: 8px; padding: 0.9rem; background: var(--ft-surface-2, #0d1117); }
      .preview-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.6rem; }
      .preview-live { display: inline-flex; align-items: center; gap: 0.35rem; font-size: 0.75rem; color: var(--ft-success-text, #3fb950); }
      .live-dot { width: 0.4rem; height: 0.4rem; border-radius: 50%; background: var(--ft-success-text, #3fb950); }
      .preview-row { display: flex; gap: 0.75rem; align-items: flex-start; margin-bottom: 0.5rem; }
      .preview-label { min-width: 6.5rem; font-size: 0.8rem; color: var(--ft-text-muted, #8b949e); }
      .preview-badges { display: flex; flex-wrap: wrap; gap: 0.3rem; }
      .dep-note { display: flex; align-items: flex-start; gap: 0.4rem; font-size: 0.78rem; color: var(--ft-text-muted, #8b949e); margin: 0.5rem 0 0; }
    `
  ]
})
export class SectorModuleRulesTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) segments: SectorSegmentDto[] = [];
  @Input({ required: true }) domains: SectorDomainDto[] = [];
  @Input({ required: true }) moduleRules: SectorModuleRuleDto[] = [];
  @Input({ required: true }) dependencies: ModuleDependencyDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly moduleLabel = moduleLabel;
  protected readonly coreModules = MODULE_CATALOG.filter(m => CORE_MODULE_IDS.includes(m.value));
  protected readonly eligibleModules = SECTOR_ELIGIBLE_MODULES;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));
  readonly busy = signal(false);

  readonly target = signal<RuleTarget | null>(null);
  readonly selection = signal<Set<number>>(new Set());
  dialogVisible = false;

  segmentModuleIds(segmentId: string): number[] {
    return this.moduleRules.filter(r => r.ruleKind === 'SegmentBase' && r.segmentId === segmentId && r.isActive).map(r => r.moduleId);
  }

  domainModuleIds(domainId: string): number[] {
    return this.moduleRules.filter(r => r.ruleKind === 'DomainOverlay' && r.domainId === domainId && r.isActive).map(r => r.moduleId);
  }

  dialogTitle(): string {
    const tgt = this.target();
    if (!tgt) return '';
    return SECTOR_RULES_FR['rules.dialog.title'].replace('{name}', tgt.label);
  }

  openSegmentDialog(seg: SectorSegmentDto): void {
    this.target.set({ kind: 'segment', id: seg.id, code: seg.code, label: seg.labelFr });
    this.selection.set(new Set(this.segmentModuleIds(seg.id)));
    this.dialogVisible = true;
  }

  openDomainDialog(dom: SectorDomainDto): void {
    this.target.set({ kind: 'domain', id: dom.id, code: dom.code, label: dom.labelFr });
    this.selection.set(new Set(this.domainModuleIds(dom.id)));
    this.dialogVisible = true;
  }

  isSelected(moduleId: number): boolean {
    return this.selection().has(moduleId);
  }

  selectedModuleIds(): number[] {
    return [...this.selection()];
  }

  unselectedModules() {
    const sel = this.selection();
    return this.eligibleModules.filter(m => !sel.has(m.value));
  }

  toggleModule(moduleId: number): void {
    const next = new Set(this.selection());
    if (next.has(moduleId)) {
      next.delete(moduleId);
      // Retire en cascade les modules qui dépendent de celui qu'on vient de décocher.
      for (const dep of this.dependencies) {
        if (dep.requiredModuleId === moduleId && next.has(dep.moduleId)) {
          next.delete(dep.moduleId);
        }
      }
    } else {
      next.add(moduleId);
      // Ajoute automatiquement les prérequis de ce module.
      for (const dep of this.dependencies) {
        if (dep.moduleId === moduleId) {
          next.add(dep.requiredModuleId);
        }
      }
    }
    this.selection.set(next);
  }

  dependencyNote(): string | null {
    const sel = this.selection();
    const relevant = this.dependencies.find(d => sel.has(d.moduleId) || sel.has(d.requiredModuleId));
    if (!relevant) return null;
    return SECTOR_RULES_FR['rules.dialog.dependency.note']
      .replace('{module}', moduleLabel(relevant.moduleId))
      .replace('{requires}', moduleLabel(relevant.requiredModuleId));
  }

  save(): void {
    const tgt = this.target();
    if (!tgt) return;
    this.busy.set(true);

    const isSegment = tgt.kind === 'segment';
    const currentRules = this.moduleRules.filter(
      r => r.isActive && (isSegment ? r.ruleKind === 'SegmentBase' && r.segmentId === tgt.id : r.ruleKind === 'DomainOverlay' && r.domainId === tgt.id)
    );
    const currentIds = new Set(currentRules.map(r => r.moduleId));
    const selected = this.selection();
    const orderedSelected = [...selected];

    const toAdd = orderedSelected.filter(id => !currentIds.has(id));
    const toRemove = currentRules.filter(r => !selected.has(r.moduleId));

    const adds = toAdd.map(id =>
      this.api.createModuleRule({
        ruleKind: isSegment ? 'SegmentBase' : 'DomainOverlay',
        segmentId: isSegment ? tgt.id : null,
        domainId: isSegment ? null : tgt.id,
        moduleId: id,
        sortOrder: orderedSelected.indexOf(id)
      })
    );
    const removes = toRemove.map(r => this.api.deactivateModuleRule(r.id));

    if (adds.length === 0 && removes.length === 0) {
      this.busy.set(false);
      this.dialogVisible = false;
      return;
    }

    forkJoin([...adds, ...removes]).subscribe({
      next: results => {
        this.busy.set(false);
        if (results.every(r => r.success)) {
          this.dialogVisible = false;
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['rules.toast.save.success'], detail: tgt.label });
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
