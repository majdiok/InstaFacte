import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DynamicFormComponent } from '@shared/studio-runtime/dynamic-form.component';
import { StudioService } from './studio.service';
import { CustomEntity, CustomEntitySchema, CustomField, FormLayout } from './studio.models';
import { StudioPageShellComponent } from './shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from './shared/studio-breadcrumb.util';
import { BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { SkeletonTableComponent } from '@shared/components/skeleton/skeleton-table.component';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { StudioRecordTabsComponent, StudioRecordTab } from './shared/studio-record-tabs.component';
import { StudioLinkedRecordsTabComponent } from './relations/studio-linked-records-tab.component';
import { STUDIO_RUNTIME_LABELS } from './shared/studio-runtime-labels';

@Component({
  selector: 'app-studio-record-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ToastModule, DynamicFormComponent, StudioPageShellComponent, SkeletonTableComponent, StudioRecordTabsComponent, StudioLinkedRecordsTabComponent],
  template: `
    <p-toast></p-toast>
    @if (entity(); as e) {
      <app-studio-page-shell
        [title]="(recordId ? 'Modifier' : 'Nouveau') + ' — ' + e.displayName"
        [subtitle]="recordId ? 'Modifiez les champs ci-dessous.' : 'Remplissez le formulaire pour créer un enregistrement.'"
        [breadcrumbs]="breadcrumbs()">
        @if (showTabs()) {
          <app-studio-record-tabs [tabs]="tabs()" [(active)]="activeTab" />
        }
        @switch (activeTab()) {
          @case ('form') {
            @if (loading()) {
              <app-skeleton-table [columns]="[{width:'100%'}]" [rows]="6" />
            } @else if (fields().length > 0) {
              <div class="studio-form-card">
                <app-dynamic-form
                  [fields]="fields()"
                  [layout]="layout()"
                  [model]="model()"
                  [saving]="saving()"
                  [entityKey]="entityKey"
                  (save)="submit($event)"
                  (formCancel)="cancel()" />
              </div>
            } @else {
              <p class="studio-hint">
                Cette table n'a pas encore de champ.
                <a [routerLink]="['/studio', e.id]">Ajoutez des champs</a> avant de saisir des données.
              </p>
            }
          }
          @default {
            @if (activeRelation(); as rel) {
              <app-studio-linked-records-tab [relation]="rel" [recordId]="recordId!" [canWrite]="canWrite()" />
            }
          }
        }
      </app-studio-page-shell>
    }
  `,
  styleUrl: './shared/studio-layout.scss',
})
export class StudioRecordFormComponent implements OnInit {
  private readonly studio = inject(StudioService);
  private readonly toast = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly entity = signal<CustomEntity | null>(null);
  readonly fields = signal<CustomField[]>([]);
  readonly layout = signal<FormLayout | null>(null);
  readonly model = signal<Record<string, unknown> | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly breadcrumbs = signal<BreadcrumbItem[]>([]);
  /** Schéma complet (relations incluses) — alimente l'onglet « Liés » (2.5e, N8 : Fiche / Liés). */
  readonly schema = signal<CustomEntitySchema | null>(null);
  readonly activeTab = signal('form');
  private readonly auth = inject(AuthService);
  readonly runtimeLabels = STUDIO_RUNTIME_LABELS;

  readonly canWrite = computed(() => this.auth.hasPermission(PERMISSIONS.customData.recordsWrite));
  readonly manyToMany = computed(() => (this.schema()?.relations ?? []).filter(r => r.kind === 'many_to_many'));
  readonly showTabs = computed(() => !!this.recordId && this.manyToMany().length > 0);
  readonly tabs = computed<StudioRecordTab[]>(() => [
    { key: 'form', label: 'Fiche' },
    ...this.manyToMany().map(r => ({
      key: `linked:${r.junctionEntityKey ?? r.targetEntityKey}`,
      label: `${this.runtimeLabels.linked.tabLabel} — ${r.targetLabel}`
    }))
  ]);
  readonly activeRelation = computed(() => {
    const key = this.activeTab();
    if (!key.startsWith('linked:')) return null;
    return this.manyToMany().find(r => `linked:${r.junctionEntityKey ?? r.targetEntityKey}` === key) ?? null;
  });

  entityKey = '';
  recordId: string | null = null;

  ngOnInit(): void {
    this.entityKey = this.route.snapshot.paramMap.get('key') ?? '';
    this.recordId = this.route.snapshot.paramMap.get('id');
    this.studio.getSchema(this.entityKey).subscribe({
      next: res => {
        if (res.success) {
          this.schema.set(res.data);
          this.entity.set(res.data.entity);
          this.fields.set(res.data.fields.filter(f => f.isActive));
          this.layout.set(res.data.form);
          this.breadcrumbs.set(STUDIO_BREADCRUMBS.recordForm(
            res.data.entity.displayName, this.entityKey, !!this.recordId));
        }
        if (this.recordId) {
          this.studio.getRecord(this.entityKey, this.recordId).subscribe({
            next: r => { this.loading.set(false); if (r.success) this.model.set(r.data.data ?? {}); },
            error: () => { this.loading.set(false); this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Enregistrement introuvable.' }); }
          });
        } else {
          this.loading.set(false);
          this.model.set({});
        }
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Schéma introuvable.' });
      }
    });
  }

  submit(data: Record<string, unknown>): void {
    this.saving.set(true);
    const obs = this.recordId
      ? this.studio.updateRecord(this.entityKey, this.recordId, data, null)
      : this.studio.createRecord(this.entityKey, data);
    obs.subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: 'Enregistré' });
          this.router.navigate(['/studio/d', this.entityKey]);
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.errors?.[0] ?? res.message ?? 'Échec.' });
        }
      },
      error: err => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: err?.error?.message ?? 'Enregistrement impossible.' });
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/studio/d', this.entityKey]);
  }
}
