import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { CustomRecordViewDto } from './studio-record-views.models';

interface ViewSwitcherOption {
  id: string | null;
  label: string;
  isDefault: boolean;
}

/**
 * Sélecteur « Liste / vues enregistrées » de la page Données (2.5a) : onglets tant qu'il y a ≤ 6
 * vues (maquette `studio-runtime-vues-kanban.html`), bascule sur `p-select panelStyleClass="studio-theme"`
 * au-delà pour rester lisible. La vue par défaut porte une étoile.
 */
@Component({
  selector: 'app-studio-view-switcher',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, SelectModule],
  template: `
    @if (options().length > 6) {
      <p-select
        [options]="options()"
        optionLabel="label"
        optionValue="id"
        [ngModel]="activeId()"
        (ngModelChange)="select($event)"
        appendTo="body"
        panelStyleClass="studio-theme"
        styleClass="view-switcher-select"
        [ariaLabel]="'Vue active'">
        <ng-template let-opt pTemplate="selectedItem">
          <span class="view-switcher__mode">{{ opt.label }}</span>
          @if (opt.isDefault) {
            <span class="view-switcher__star" title="Vue par défaut"><i class="fa-solid fa-star"></i></span>
          }
        </ng-template>
        <ng-template let-opt pTemplate="item">
          <span>{{ opt.label }}</span>
          @if (opt.isDefault) {
            <span class="view-switcher__star" title="Vue par défaut"><i class="fa-solid fa-star"></i></span>
          }
        </ng-template>
      </p-select>
    } @else {
      <div class="view-switcher" role="tablist" aria-label="Vue active">
        @for (opt of options(); track opt.id ?? 'list') {
          <button
            type="button"
            role="tab"
            class="view-switcher__tab"
            [attr.aria-selected]="activeId() === opt.id"
            (click)="select(opt.id)">
            <span>{{ opt.label }}</span>
            @if (opt.isDefault) {
              <span class="view-switcher__star" title="Vue par défaut"><i class="fa-solid fa-star"></i></span>
            }
          </button>
        }
      </div>
    }
  `,
  styleUrl: '../shared/studio-layout.scss'
})
export class StudioViewSwitcherComponent {
  readonly views = input<CustomRecordViewDto[]>([]);
  /** `null` = vue « Liste » brute (comportement historique, capabilities off ou aucune vue). */
  readonly activeId = input<string | null>(null);
  readonly activeIdChange = output<string | null>();

  protected readonly options = computed<ViewSwitcherOption[]>(() => [
    { id: null, label: 'Liste', isDefault: false },
    ...this.views().map(v => ({ id: v.id, label: v.displayName, isDefault: v.isDefault }))
  ]);

  select(id: string | null): void {
    if (id === this.activeId()) return;
    this.activeIdChange.emit(id);
  }
}
