import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { DepreciationRunResultDto, FixedAssetsService } from '../services/fixed-assets.service';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { AccountingCorrectionBannerComponent } from '../shared/accounting-correction-banner.component';

@Component({
  selector: 'app-depreciation-run',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    PageHeaderComponent,
    ButtonComponent,
    AccountingStatusBannerComponent,
    AccountingCorrectionBannerComponent
  ],
  template: `
    <app-page-header
      title="Dotations immobilisations"
      subtitle="Comptabiliser les amortissements de l'exercice (écritures 681 / 281 — journal JIM)" />

    <app-accounting-correction-banner />

    <div class="card">
      <p class="hint">
        Le tableau d'amortissement est généré automatiquement à la mise en service de chaque actif.
        Si une dotation attendue n'apparaît pas, ouvrez la fiche de l'actif et générez son tableau.
      </p>
      <label>
        Exercice fiscal
        <input type="number" class="accounting-filter-input" [(ngModel)]="fiscalYear" min="2000" max="2100" />
      </label>
      <div class="actions">
        <app-button variant="primary" type="button" (click)="run()" [disabled]="loading()">
          Comptabiliser les dotations
        </app-button>
        <app-button variant="outline" type="button" icon="pi-download" (click)="exportReport()" [disabled]="loading()">
          Export Excel dotations
        </app-button>
        <app-button variant="secondary" type="button" routerLink="/accounting/fixed-assets">Retour au registre</app-button>
        <app-button
          variant="secondary"
          type="button"
          routerLink="/accounting/fixed-assets/amortization-table"
          [queryParams]="{ refresh: 1 }">
          Voir tableau amortissements
        </app-button>
      </div>
    </div>

    <app-accounting-status-banner [message]="error() ?? ''" variant="error" *ngIf="error()" />

    <div class="card result" *ngIf="result()">
      <h3>Résultat — exercice {{ result()!.fiscalYear }}</h3>
      <p *ngIf="rerunMessage() as msg"><strong>{{ msg }}</strong></p>
      <ng-container *ngIf="!rerunMessage()">
        <p><strong>Dotations comptabilisées :</strong> {{ result()!.postedCount }}</p>
        <p><strong>Ignorées / en erreur :</strong> {{ result()!.skippedCount }}</p>
        <p><strong>Total dotations :</strong> {{ result()!.totalDepreciationAmount | number: '1.3-3' }} TND</p>
        <ul *ngIf="result()!.errors?.length">
          <li *ngFor="let e of result()!.errors">{{ e }}</li>
        </ul>
      </ng-container>
    </div>
  `,
  styles: [
    `
      label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        max-width: 240px;
      }
      .hint {
        font-size: 0.8rem;
        color: #64748b;
        margin: 0 0 0.75rem;
      }
      .actions {
        display: flex;
        gap: 0.75rem;
        margin-top: 1rem;
        flex-wrap: wrap;
      }
      .result p {
        margin: 0.35rem 0;
      }
    `
  ]
})
export class DepreciationRunComponent implements OnInit {
  private readonly api = inject(FixedAssetsService);
  private readonly route = inject(ActivatedRoute);

  fiscalYear = new Date().getFullYear();
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<DepreciationRunResultDto | null>(null);

  // T14 (re-run) — quand aucune nouvelle dotation n'est comptabilisée mais que des dotations
  // étaient déjà postées pour cet exercice, on affiche un message explicite au lieu du « 0 »
  // cryptique (capture 2410).
  readonly rerunMessage = computed(() => {
    const r = this.result();
    if (!r) return null;
    const alreadyPosted = r.alreadyPostedCount ?? 0;
    if (r.postedCount === 0 && alreadyPosted > 0) {
      return `Aucune nouvelle dotation — ${alreadyPosted} dotation(s) déjà comptabilisée(s) pour cet exercice.`;
    }
    return null;
  });

  ngOnInit(): void {
    const fy = this.route.snapshot.queryParamMap.get('fiscalYear');
    if (fy) {
      const year = Number(fy);
      if (!Number.isNaN(year)) this.fiscalYear = year;
    }
  }

  run(): void {
    // C7 — garde anti double-clic : en plus du [disabled] du bouton, on court-circuite tout appel
    // tant qu'un run est déjà en cours (une race entre deux clics peut contourner le disabled).
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.api.postDepreciationRun(this.fiscalYear).subscribe({
      next: res => {
        this.result.set(res.data ?? null);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message ?? 'Erreur lors de la comptabilisation des dotations.');
        this.loading.set(false);
      }
    });
  }

  exportReport(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.exportDepreciationReportExcel(this.fiscalYear).subscribe({
      next: blob => {
        this.loading.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `dotations-immobilisations-${this.fiscalYear}.xlsx`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Erreur export Excel.');
      }
    });
  }
}