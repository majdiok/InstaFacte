import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AuthService } from '@core/services/auth.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { FirmLeaveType } from './data-access/firm-leaves.models';
import { FirmLeaveRequestDialogComponent } from './firm-leave-request-dialog.component';

@Component({
  selector: 'app-firm-leaves-shell',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    ButtonModule, PageHeaderComponent, FirmLeaveRequestDialogComponent
  ],
  template: `
    <app-page-header title="Congés & Absences" subtitle="Gestion des demandes, soldes et planning équipe">
      <button type="button" pButton label="Nouvelle demande" icon="pi pi-plus" class="p-button-sm"
        (click)="openCreate()"></button>
    </app-page-header>

    <nav class="tabs" aria-label="Sections congés">
      @for (t of tabs; track t.path) {
        @if (!t.managerOnly || isManager()) {
          <a [routerLink]="t.path" routerLinkActive="active" class="tab">{{ t.label }}</a>
        }
      }
    </nav>

    <router-outlet (activate)="onActivate($event)" />

    <app-firm-leave-request-dialog
      [(visible)]="dialogVisible"
      [types]="types()"
      (saved)="reloadChild()" />
  `,
  styles: [`
    :host { display: block; }
    .tabs {
      display: flex; flex-wrap: wrap; gap: .25rem;
      margin: 0 0 1rem; padding: .25rem;
      background: var(--color-surface-muted, #f8fafc);
      border-radius: 12px; border: 1px solid var(--color-border-subtle, #e2e8f0);
    }
    .tab {
      padding: .5rem .9rem; border-radius: 8px; text-decoration: none;
      color: var(--color-text-muted, #64748b); font-size: .875rem; font-weight: 500;
    }
    .tab:hover { color: var(--color-text, #0f172a); background: #fff; }
    .tab.active {
      background: #fff; color: var(--color-primary, #4f46e5);
      box-shadow: 0 1px 2px rgba(15,23,42,.06);
    }
  `]
})
export class FirmLeavesShellComponent {
  private readonly auth = inject(AuthService);
  private readonly api = inject(FirmLeavesService);
  readonly isManager = this.auth.isFirmManager;
  readonly types = signal<FirmLeaveType[]>([]);
  dialogVisible = false;
  private childReload: (() => void) | null = null;

  readonly tabs = [
    { path: 'overview', label: 'Vue d’ensemble', managerOnly: false },
    { path: 'calendar', label: 'Calendrier', managerOnly: false },
    { path: 'requests', label: 'Demandes', managerOnly: false },
    { path: 'validation', label: 'Validation', managerOnly: true },
    { path: 'balances', label: 'Soldes', managerOnly: false },
    { path: 'types', label: 'Types d’absence', managerOnly: true },
    { path: 'settings', label: 'Paramètres', managerOnly: true }
  ];

  constructor() {
    this.api.listTypes(true).subscribe({
      next: res => this.types.set(res.data ?? [])
    });
  }

  openCreate(): void {
    this.dialogVisible = true;
  }

  onActivate(comp: unknown): void {
    const c = comp as { reload?: () => void };
    this.childReload = typeof c?.reload === 'function' ? () => c.reload!() : null;
  }

  reloadChild(): void {
    this.api.listTypes(true).subscribe({ next: res => this.types.set(res.data ?? []) });
    this.childReload?.();
  }
}
