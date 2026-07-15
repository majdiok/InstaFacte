import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DynamicFormComponent } from '@shared/studio-runtime/dynamic-form.component';
import { StudioService } from './studio.service';
import { CustomEntity, CustomField, FormLayout } from './studio.models';
import { StudioPageShellComponent } from './shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from './shared/studio-breadcrumb.util';
import { BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { SkeletonTableComponent } from '@shared/components/skeleton/skeleton-table.component';

@Component({
  selector: 'app-studio-record-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ToastModule, DynamicFormComponent, StudioPageShellComponent, SkeletonTableComponent],
  template: `
    <p-toast></p-toast>
    @if (entity(); as e) {
      <app-studio-page-shell
        [title]="(recordId ? 'Modifier' : 'Nouveau') + ' — ' + e.displayName"
        [subtitle]="recordId ? 'Modifiez les champs ci-dessous.' : 'Remplissez le formulaire pour créer un enregistrement.'"
        [breadcrumbs]="breadcrumbs()">
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

  entityKey = '';
  recordId: string | null = null;

  ngOnInit(): void {
    this.entityKey = this.route.snapshot.paramMap.get('key') ?? '';
    this.recordId = this.route.snapshot.paramMap.get('id');
    this.studio.getSchema(this.entityKey).subscribe({
      next: res => {
        if (res.success) {
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
