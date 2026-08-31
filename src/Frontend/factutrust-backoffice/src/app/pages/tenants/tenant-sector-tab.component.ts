import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformTenantSectorService } from '@core/services/platform-tenant-sector.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type {
  SectorDomainDto,
  SectorReconfigurationPreviewDto,
  SectorSegmentDto,
  SegmentDomainLinkDto
} from '@core/models/sector-rules.models';
import { moduleLabel } from '@core/models/module-catalog';

import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';

import { TENANTS_FR } from './tenants.i18n.fr';

/**
 * Phase 2 (WP-F8) — Tab « Configuration sectorielle » du détail tenant.
 *
 * Lecture du secteur/domaine courants (résolus en libellés FR via le catalogue chargé une
 * fois), édition avec prévisualisation obligatoire avant application (`preview()` est sans
 * effet de bord ; `apply()` mute réellement classification + accès modules + modèles).
 */
@Component({
  selector: 'app-tenant-sector-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    SelectModule,
    CheckboxModule,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    FtConfirmActionComponent
  ],
  template: `
    @if (catalogLoading()) {
      <div class="loading-block" aria-busy="true">
        <ft-skeleton shape="rect" width="100%" height="6rem" />
      </div>
    } @else {
      @if (!companySegment && !businessDomain) {
        <ft-empty-state
          variant="table-empty"
          [title]="t('sector.empty.title')"
          [description]="t('sector.empty.desc')"
        >
          @if (canApply()) {
            <p-button [label]="t('sector.action.modify')" icon="pi pi-pencil" severity="primary" (onClick)="openPicker()" />
          }
        </ft-empty-state>
      } @else {
        <div class="card current-card">
          <h3>{{ t('sector.card.title') }}</h3>
          <p class="muted">{{ t('sector.card.subtitle') }}</p>
          <div class="current-row">
            <span class="current-label">{{ t('sector.segment') }}</span>
            <ft-badge tone="accent" size="md">
              {{ segmentLabel(companySegment) }}
              <code class="badge-code">{{ companySegment || '—' }}</code>
            </ft-badge>
          </div>
          <div class="current-row">
            <span class="current-label">{{ t('sector.domain') }}</span>
            <ft-badge tone="info" size="md">
              {{ domainLabel(businessDomain) }}
              <code class="badge-code">{{ businessDomain || '—' }}</code>
            </ft-badge>
          </div>
          @if (canApply()) {
            <p-button [label]="t('sector.action.modify')" icon="pi pi-pencil" [text]="true" (onClick)="openPicker()" />
          } @else {
            <p class="muted small">{{ t('sector.readonly.hint') }}</p>
          }
        </div>
      }

      @if (pickerOpen()) {
        <div class="card picker-card">
          <div class="picker-row">
            <div class="picker-field">
              <label class="field-label" for="newSegment">{{ t('sector.picker.newSegment') }}</label>
              <p-select
                inputId="newSegment"
                [options]="segmentOptions()"
                [(ngModel)]="pickedSegment"
                (ngModelChange)="onSegmentPicked()"
                optionLabel="label"
                optionValue="value"
                styleClass="w-full"
              />
            </div>
            <div class="picker-field">
              <label class="field-label" for="newDomain">{{ t('sector.picker.newDomain') }}</label>
              <p-select
                inputId="newDomain"
                [options]="domainOptionsForSegment()"
                [(ngModel)]="pickedDomain"
                optionLabel="label"
                optionValue="value"
                styleClass="w-full"
              />
            </div>
          </div>
          <div class="picker-actions">
            <p-button [label]="t('sector.action.cancel')" [text]="true" severity="secondary" (onClick)="closePicker()" />
            <p-button
              [label]="t('sector.action.preview')"
              icon="pi pi-eye"
              severity="primary"
              [loading]="previewLoading()"
              [disabled]="!pickedSegment"
              (onClick)="runPreview()"
            />
          </div>
        </div>
      }

      @if (preview(); as p) {
        <div class="card diff-card">
          <h4>{{ previewTitle(p) }}</h4>
          <p class="muted small">{{ previewMeta() }}</p>

          <div class="diff-group">
            <h5 class="t-success">{{ t('sector.diff.modulesEnabled') }}</h5>
            <div class="badge-row">
              @for (id of enabledModuleIds(p); track id) {
                <ft-badge tone="success" size="sm">{{ moduleLabel(id) }}</ft-badge>
              } @empty {
                <span class="muted small">{{ t('sector.diff.none') }}</span>
              }
            </div>
          </div>

          <div class="diff-group">
            <h5 class="t-danger">{{ t('sector.diff.modulesDisabled') }}</h5>
            <div class="badge-row">
              @for (id of disabledModuleIds(p); track id) {
                <ft-badge tone="danger" size="sm">{{ moduleLabel(id) }}</ft-badge>
              } @empty {
                <span class="muted small">{{ t('sector.diff.none') }}</span>
              }
            </div>
            @if (disabledModuleIds(p).length > 0) {
              <p class="warning-note">{{ t('sector.diff.modulesDisabled.warning') }}</p>
            }
          </div>

          @if (p.templates.length > 0) {
            <div class="diff-group">
              <h5 class="t-info">{{ t('sector.diff.templates') }}</h5>
              <ul class="model-list">
                @for (tpl of p.templates; track tpl.code) {
                  <li>{{ tpl.code }} — v{{ tpl.version }} @if (tpl.alreadyApplied) { <em>(déjà appliqué)</em> }</li>
                }
              </ul>
            </div>
          }

          @if (p.settings.length > 0) {
            <div class="diff-group">
              <h5>{{ t('sector.diff.settings') }}</h5>
              <ul class="model-list">
                @for (s of p.settings; track s.key) {
                  <li>{{ s.key }} → « {{ s.value }} »</li>
                }
              </ul>
            </div>
          }

          @if (p.warnings.length > 0) {
            <div class="diff-group">
              <h5 class="t-warning">{{ t('sector.diff.warnings') }}</h5>
              <ul class="model-list">
                @for (w of p.warnings; track w) {
                  <li>{{ w }}</li>
                }
              </ul>
            </div>
          }

          <p class="audit-note">{{ t('sector.audit.note') }}</p>

          <div class="apply-row">
            <label class="confirm-checkbox">
              <p-checkbox [binary]="true" [(ngModel)]="confirmed" />
              {{ t('sector.confirm.checkbox') }}
            </label>
            <p-button
              [label]="t('sector.action.apply')"
              icon="pi pi-check"
              severity="danger"
              [outlined]="true"
              [disabled]="!confirmed"
              (onClick)="openApplyConfirm()"
            />
          </div>
        </div>
      }
    }

    <ft-confirm-action
      [(visible)]="applyConfirmVisible"
      title="Appliquer la reconfiguration sectorielle"
      [description]="applyDescription()"
      variant="destructive"
      confirmKeyword="APPLIQUER"
      [confirmLabel]="t('sector.action.apply')"
      confirmIcon="pi pi-check"
      [busy]="applyBusy()"
      (confirmed)="confirmApply()"
    >
      <ul>
        <li>{{ t('sector.diff.modulesDisabled.warning') }}</li>
        <li>{{ t('sector.audit.note') }}</li>
      </ul>
    </ft-confirm-action>
  `,
  styles: [
    `
      :host { display: block; }
      .loading-block { margin-top: 0.5rem; }
      .card {
        border: 1px solid var(--ft-border, #30363d);
        border-radius: 8px;
        padding: 1rem;
        margin-bottom: 1rem;
        background: var(--ft-surface-2, #0d1117);
      }
      .card h3 { margin: 0 0 0.2rem; font-size: 1rem; color: var(--ft-text); }
      .card h4 { margin: 0 0 0.2rem; font-size: 0.95rem; color: var(--ft-text); }
      .muted { color: var(--ft-text-muted, #8b949e); margin: 0 0 0.85rem; font-size: 0.85rem; }
      .muted.small { font-size: 0.78rem; }
      .current-row { display: flex; align-items: center; gap: 0.6rem; margin-bottom: 0.5rem; }
      .current-label { min-width: 5rem; font-size: 0.85rem; color: var(--ft-text-muted, #8b949e); }
      .badge-code { margin-left: 0.4rem; font-size: 0.7rem; opacity: 0.85; }
      .picker-card { border-style: dashed; }
      .picker-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.85rem; margin-bottom: 0.9rem; }
      .picker-field { display: flex; flex-direction: column; gap: 0.35rem; }
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .picker-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
      .diff-group { margin-bottom: 0.9rem; }
      .diff-group h5 { margin: 0 0 0.35rem; font-size: 0.82rem; text-transform: uppercase; letter-spacing: 0.03em; }
      .t-success { color: var(--ft-success-text, #3fb950); }
      .t-danger { color: var(--ft-danger-text, #f85149); }
      .t-info { color: var(--ft-info-text, #79c0ff); }
      .t-warning { color: var(--ft-warning-text, #d29922); }
      .badge-row { display: flex; flex-wrap: wrap; gap: 0.3rem; }
      .warning-note { font-size: 0.78rem; color: var(--ft-warning-text, #d29922); margin: 0.4rem 0 0; }
      .model-list { margin: 0; padding-left: 1.1rem; font-size: 0.85rem; color: var(--ft-text); }
      .audit-note { font-size: 0.78rem; color: var(--ft-text-muted, #8b949e); margin: 0.8rem 0; }
      .apply-row { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; padding-top: 0.6rem; border-top: 1px dashed var(--ft-border, #30363d); }
      .confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.82rem; color: var(--ft-text-muted, #8b949e); max-width: 32rem; }
      .w-full { width: 100%; }
      :host ::ng-deep .p-select { width: 100%; }
    `
  ]
})
export class TenantSectorTabComponent implements OnInit, OnChanges {
  private readonly rulesApi = inject(PlatformSectorRulesService);
  private readonly sectorApi = inject(PlatformTenantSectorService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) tenantId: string | null = null;
  @Input() companyName: string | null = null;
  @Input() companySegment: string | null = null;
  @Input() businessDomain: string | null = null;
  @Output() changed = new EventEmitter<void>();

  protected readonly moduleLabel = moduleLabel;

  protected t(key: keyof typeof TENANTS_FR): string {
    return TENANTS_FR[key];
  }

  readonly canApply = computed(() => this.permissions.has(PlatformPermission.SectorRulesApply));

  readonly catalogLoading = signal(true);
  private segments: SectorSegmentDto[] = [];
  private domains: SectorDomainDto[] = [];
  private segmentDomains: SegmentDomainLinkDto[] = [];

  readonly pickerOpen = signal(false);
  pickedSegment: string | null = null;
  pickedDomain: string | null = null;

  readonly previewLoading = signal(false);
  readonly preview = signal<SectorReconfigurationPreviewDto | null>(null);
  confirmed = false;

  applyConfirmVisible = false;
  readonly applyBusy = signal(false);

  ngOnInit(): void {
    this.loadCatalog();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['tenantId']) {
      this.pickerOpen.set(false);
      this.preview.set(null);
      this.confirmed = false;
    }
  }

  private loadCatalog(): void {
    this.catalogLoading.set(true);
    this.rulesApi.getAll().subscribe({
      next: res => {
        this.catalogLoading.set(false);
        if (res.success && res.data) {
          this.segments = res.data.segments;
          this.domains = res.data.domains;
          this.segmentDomains = res.data.segmentDomains;
        }
      },
      error: () => this.catalogLoading.set(false)
    });
  }

  segmentLabel(code: string | null): string {
    if (!code) return '—';
    return this.segments.find(s => s.code === code)?.label ?? code;
  }

  domainLabel(code: string | null): string {
    if (!code) return '—';
    return this.domains.find(d => d.code === code)?.label ?? code;
  }

  segmentOptions() {
    return this.segments.filter(s => s.isActive).map(s => ({ label: s.label, value: s.code }));
  }

  domainOptionsForSegment() {
    if (!this.pickedSegment) return [];
    const allowed = new Set(this.segmentDomains.find(l => l.segmentCode === this.pickedSegment)?.domainCodes ?? []);
    return this.domains.filter(d => d.isActive && allowed.has(d.code)).map(d => ({ label: d.label, value: d.code }));
  }

  onSegmentPicked(): void {
    this.pickedDomain = null;
  }

  openPicker(): void {
    this.pickedSegment = this.companySegment;
    this.pickedDomain = this.businessDomain;
    this.preview.set(null);
    this.confirmed = false;
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
    this.preview.set(null);
  }

  runPreview(): void {
    if (!this.tenantId || !this.pickedSegment) return;
    this.previewLoading.set(true);
    this.confirmed = false;
    this.sectorApi
      .preview(this.tenantId, {
        companySegment: this.pickedSegment,
        businessDomain: this.pickedDomain,
        recomputeModuleGrants: true,
        applyDataTemplates: true
      })
      .subscribe({
        next: res => {
          this.previewLoading.set(false);
          if (res.success && res.data) {
            this.preview.set(res.data);
          } else {
            this.toastError(res.message ?? TENANTS_FR['sector.toast.preview.error']);
          }
        },
        error: err => {
          this.previewLoading.set(false);
          this.toastError((err as { error?: { message?: string } })?.error?.message ?? TENANTS_FR['sector.toast.preview.error']);
        }
      });
  }

  previewTitle(p: SectorReconfigurationPreviewDto): string {
    const from = `${p.currentSegment ?? '—'} / ${p.currentDomain ?? '—'}`;
    const to = `${p.targetSegment ?? '—'} / ${p.targetDomain ?? '—'}`;
    return TENANTS_FR['sector.preview.title'].replace('{from}', from).replace('{to}', to);
  }

  previewMeta(): string {
    const now = new Date().toLocaleString('fr-FR');
    return TENANTS_FR['sector.preview.computedAt'].replace('{date}', now).replace('{version}', '—');
  }

  enabledModuleIds(p: SectorReconfigurationPreviewDto): number[] {
    return [...new Set(p.users.flatMap(u => u.modulesToEnable))];
  }

  disabledModuleIds(p: SectorReconfigurationPreviewDto): number[] {
    return [...new Set(p.users.flatMap(u => u.modulesToDisable))];
  }

  applyDescription(): string {
    return TENANTS_FR['sector.confirm.tenantWarning'].replace('{tenant}', this.companyName ?? '');
  }

  openApplyConfirm(): void {
    if (!this.confirmed) return;
    this.applyConfirmVisible = true;
  }

  confirmApply(): void {
    if (!this.tenantId || !this.pickedSegment) return;
    this.applyBusy.set(true);
    this.sectorApi
      .apply(this.tenantId, {
        companySegment: this.pickedSegment,
        businessDomain: this.pickedDomain,
        recomputeModuleGrants: true,
        applyDataTemplates: true
      })
      .subscribe({
        next: res => {
          this.applyBusy.set(false);
          this.applyConfirmVisible = false;
          if (res.success) {
            this.toast.add({ severity: 'success', summary: TENANTS_FR['sector.toast.apply.success'], detail: res.message ?? '' });
            this.companySegment = this.pickedSegment;
            this.businessDomain = this.pickedDomain;
            this.pickerOpen.set(false);
            this.preview.set(null);
            this.confirmed = false;
            this.changed.emit();
          } else {
            this.toastError(res.message);
          }
        },
        error: err => {
          this.applyBusy.set(false);
          this.applyConfirmVisible = false;
          this.toastError((err as { error?: { message?: string } })?.error?.message);
        }
      });
  }

  private toastError(message?: string | null): void {
    this.toast.add({ severity: 'error', summary: 'Erreur', detail: message ?? 'Une erreur est survenue.' });
  }
}
