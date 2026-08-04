import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountingService, JournalEntryTemplateDto } from '../../services/accounting.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-entry-recurring-tab',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <section class="recurring-tab card">
      @if (loading()) {
        <p>Chargement…</p>
      } @else if (templates().length === 0) {
        <p class="empty">Aucune écriture récurrente configurée.</p>
        <a routerLink="/accounting/entry-templates" class="link">Configurer des modèles récurrents →</a>
      } @else {
        <table class="recurring-table">
          <thead>
            <tr>
              <th>Modèle</th>
              <th>Fréquence</th>
              <th>Prochaine exécution</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (t of templates(); track t.id) {
              <tr>
                <td><strong>{{ t.name }}</strong></td>
                <td>{{ freqLabel(t.recurrenceFrequency) }}</td>
                <td>{{ t.nextRunDate ? (t.nextRunDate | date:'dd/MM/yyyy') : '—' }}</td>
                <td>
                  <button type="button" class="btn btn-sm btn-primary"
                          [disabled]="runningId() === t.id"
                          (click)="runNow(t)">
                    {{ runningId() === t.id ? 'Génération…' : 'Générer maintenant' }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
      <a routerLink="/accounting/entry-templates" class="link">Gérer les modèles récurrents →</a>
    </section>
  `,
  styles: `
    .recurring-tab { padding:var(--spacing-5); }
    .recurring-table { width:100%; border-collapse:collapse; font-size:var(--font-size-sm); margin-bottom:var(--spacing-4); }
    .recurring-table th, .recurring-table td { padding:var(--spacing-2) var(--spacing-3); border-bottom:1px solid var(--color-border-subtle); text-align:left; }
    .empty { color:var(--color-text-secondary); }
    .link { font-size:var(--font-size-sm); color:var(--color-primary-600); }
  `
})
export class EntryRecurringTabComponent implements OnInit {
  private readonly api = inject(AccountingService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly templates = signal<JournalEntryTemplateDto[]>([]);
  readonly loading = signal(true);
  readonly runningId = signal<string | null>(null);

  ngOnInit(): void {
    this.api.getJournalTemplates(true)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          const all = res.success && res.data ? res.data : [];
          this.templates.set(all.filter(t => t.isRecurring));
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }

  freqLabel(f: number): string {
    switch (f) {
      case 1: return 'Mensuelle';
      case 2: return 'Trimestrielle';
      case 3: return 'Annuelle';
      default: return '—';
    }
  }

  runNow(t: JournalEntryTemplateDto): void {
    this.runningId.set(t.id);
    this.api.runTemplateRecurrence(t.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.runningId.set(null);
          if (res.success) {
            this.toast.add({ severity: 'success', summary: 'Écriture générée', detail: `Le modèle « ${t.name} » a été exécuté.`, life: 5000 });
          } else {
            this.toast.add({ severity: 'error', summary: 'Échec', detail: res.error ?? 'Erreur', life: 6000 });
          }
        },
        error: () => {
          this.runningId.set(null);
          this.toast.add({ severity: 'error', summary: 'Erreur réseau', detail: 'Impossible de générer l\'écriture.', life: 6000 });
        }
      });
  }
}
