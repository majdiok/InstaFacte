import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { RouterLink } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-time-sheets-toolbar',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule, ButtonModule, RouterLink],
  template: `
    <div class="toolbar fc-card">
      <label>Année
        <p-select [options]="yearOptions" [(ngModel)]="selectedYear" (onChange)="yearChange.emit(selectedYear)" [ngModelOptions]="{standalone: true}" />
      </label>
      <label>Mois
        <p-select [options]="monthOptions" [(ngModel)]="selectedMonth" (onChange)="monthChange.emit(selectedMonth)" [ngModelOptions]="{standalone: true}" />
      </label>
      @if (!hideModes) {
        <div class="view-switch">
          <button type="button" pButton class="p-button-sm p-button-outlined" [label]="'Liste'" [severity]="mode === 'list' ? 'primary' : 'secondary'" (click)="modeChange.emit('list')"></button>
          <button type="button" pButton class="p-button-sm p-button-outlined" [label]="'Semaine'" [severity]="mode === 'week' ? 'primary' : 'secondary'" (click)="modeChange.emit('week')"></button>
          <button type="button" pButton class="p-button-sm p-button-outlined" [label]="'Jour'" [severity]="mode === 'day' ? 'primary' : 'secondary'" (click)="modeChange.emit('day')"></button>
        </div>
      }
      @if (auth.isFirmManager()) {
        <a routerLink="/firm/governance/dossier-time-profitability" class="link-btn">Analyse rentabilité dossiers</a>
      }
    </div>
  `,
  styles: [`
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .toolbar { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .toolbar label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 120px; }
    .view-switch { display: inline-flex; gap: .35rem; }
    .link-btn { margin-left: auto; font-size: .875rem; color: var(--color-primary, #0f766e); }
  `]
})
export class TimeSheetsToolbarComponent {
  readonly auth = inject(AuthService);

  @Input() yearOptions: { label: string; value: number }[] = [];
  @Input() monthOptions: { label: string; value: number | null }[] = [];
  @Input() selectedYear = new Date().getFullYear();
  @Input() selectedMonth: number | null = new Date().getMonth() + 1;
  @Input() mode: 'list' | 'week' | 'day' = 'list';
  @Input() hideModes = false;
  @Output() yearChange = new EventEmitter<number>();
  @Output() monthChange = new EventEmitter<number | null>();
  @Output() modeChange = new EventEmitter<'list' | 'week' | 'day'>();
}
