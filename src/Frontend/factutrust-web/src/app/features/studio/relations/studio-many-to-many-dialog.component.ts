import { ChangeDetectionStrategy, Component, computed, inject, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CustomEntity, ManyToManyRelationDto } from '../studio.models';
import { StudioService } from '../studio.service';
import { STUDIO_RUNTIME_LABELS } from '../shared/studio-runtime-labels';
import { slugifyKey } from '../shared/studio-text.util';

/**
 * Dialog de création d'une relation plusieurs-à-plusieurs (2.5f, maquette M4) : cible parmi les
 * autres tables non-jonction, libellé facultatif, clé de jonction facultative (placeholder
 * `{source}_{cible}` — clé par défaut serveur). « Attribut de liaison » = bloc désactivé (Bientôt).
 * 409 ⇒ `relations.duplicateKey` ; 400 ⇒ message serveur en ligne (`Validation.target/junctionKey`).
 */
@Component({
  selector: 'app-studio-many-to-many-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DialogModule, ButtonModule, InputTextModule, SelectModule],
  template: `
    <p-dialog [header]="labels.relations.addManyToMany" [(visible)]="visible" [modal]="true" [style]="{ width: '30rem' }"
      (onHide)="reset()" data-testid="m2m-dialog">
      <div class="ft-form">
        <label for="m2m-target">Table cible *</label>
        <p-select inputId="m2m-target" class="studio-w-full" [options]="targetOptions()" [ngModel]="targetEntityId()"
          (ngModelChange)="targetEntityId.set($event)" optionLabel="label" optionValue="value" appendTo="body"
          panelStyleClass="studio-theme" data-testid="m2m-target" />

        <label for="m2m-label">Libellé affiché</label>
        <input pInputText id="m2m-label" class="studio-w-full" [ngModel]="label()" (ngModelChange)="label.set($event)"
          [placeholder]="selectedTargetLabel() || 'Tels que nommés'" maxlength="120" data-testid="m2m-label" />

        <label for="m2m-jkey">Clé de la table de liaison</label>
        <input pInputText id="m2m-jkey" class="studio-w-full" [ngModel]="junctionKey()" (ngModelChange)="junctionKey.set($event)"
          [placeholder]="defaultJunctionKey()" maxlength="64" data-testid="m2m-junction-key" />
        @if (junctionKey() && !junctionKeyValid()) {
          <small class="studio-hint" data-testid="m2m-key-invalid">Clé invalide : minuscule initiale, lettres, chiffres ou « _ » (2 à 64 caractères).</small>
        }

        <label>Attribut de liaison</label>
        <div class="studio-row studio-row-section m2m-soon">
          <i class="pi pi-info-circle studio-mr"></i>
          <span class="studio-grow">{{ labels.relations.junctionAttributeSoon }}</span>
        </div>

        @if (error(); as message) {
          <div class="studio-row studio-row-section" role="alert" data-testid="m2m-error">{{ message }}</div>
        }
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" (onClick)="visible.set(false)" />
        <p-button label="Créer" icon="pi pi-check" [loading]="saving()" [disabled]="!canSubmit()" (onClick)="submit()" data-testid="m2m-submit" />
      </ng-template>
    </p-dialog>
  `,
  // studio-layout.scss fournit .studio-row / .studio-row-section / .studio-grow utilisés par le
  // bloc « Bientôt » et les alertes d'erreur (encapsulation émulée : styleUrl requis ici).
  styleUrl: '../shared/studio-layout.scss',
  styles: [`.m2m-soon { opacity: .75; }`]
})
export class StudioManyToManyDialogComponent {
  readonly sourceEntity = input.required<CustomEntity>();
  readonly entities = input.required<CustomEntity[]>();
  readonly visible = model(false);
  readonly created = output<ManyToManyRelationDto>();

  private readonly studio = inject(StudioService);

  readonly labels = STUDIO_RUNTIME_LABELS;
  readonly targetEntityId = signal<string | null>(null);
  readonly label = signal('');
  readonly junctionKey = signal('');
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  /** Cibles : toutes les tables sauf la source et les jonctions. */
  readonly targetOptions = computed(() => this.entities()
    .filter(e => e.id !== this.sourceEntity()?.id && e.kind !== 'Junction')
    .map(e => ({ label: e.displayName, value: e.id })));
  readonly selectedTargetLabel = computed(() =>
    this.targetOptions().find(o => o.value === this.targetEntityId())?.label ?? null);
  /** Clé de jonction par défaut (même règle que le serveur : `{source}_{cible}` slugifiée). */
  readonly defaultJunctionKey = computed(() => {
    const target = this.entities().find(e => e.id === this.targetEntityId());
    return target ? slugifyKey(`${this.sourceEntity()?.key ?? ''}_${target.key}`, 'v_') : '';   // 4.5h : plus d'import du concepteur de vues
  });
  readonly junctionKeyValid = computed(() => {
    const key = this.junctionKey().trim();
    return !key || /^[a-z][a-z0-9_]{1,63}$/.test(key);
  });
  readonly canSubmit = computed(() => !!this.targetEntityId() && this.junctionKeyValid() && !this.saving());

  submit(): void {
    const target = this.targetEntityId();
    if (!target || !this.canSubmit()) return;
    this.saving.set(true);
    this.error.set(null);
    const label = this.label().trim() || null;
    const junctionKey = this.junctionKey().trim() || null;
    this.studio.createManyToMany(this.sourceEntity().id, {
      targetEntityId: target,
      label,
      junctionKey,
      junctionDisplayName: label
    }).subscribe({
      next: res => {
        this.saving.set(false);
        if (!res.success) { this.error.set(res.message || 'Création impossible.'); return; }
        this.created.emit(res.data);
        this.visible.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        if (err.status === 409) { this.error.set(this.labels.relations.duplicateKey); return; }
        this.error.set(typeof err.error?.message === 'string' ? err.error.message : 'Création impossible.');
      }
    });
  }

  reset(): void {
    this.targetEntityId.set(null);
    this.label.set('');
    this.junctionKey.set('');
    this.error.set(null);
  }
}
