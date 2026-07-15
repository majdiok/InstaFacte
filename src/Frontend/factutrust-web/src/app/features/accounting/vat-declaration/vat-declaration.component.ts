import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AccountingService, VatDeclarationDto } from '../services/accounting.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { wrapLegacyAnalyzePayload } from '@features/ai-assistant/utils/ai-screen-payload.factory';
import { AccountingStatusBannerComponent } from '../shared/accounting-status-banner.component';
import { VatDeclarationToolbarComponent } from './vat-declaration-toolbar.component';
import { VatDeclarationGeneralInfoComponent } from './vat-declaration-general-info.component';
import { VatDeclarationStatusPanelComponent } from './vat-declaration-status-panel.component';
import { VatDeclarationTaxesTableComponent } from './vat-declaration-taxes-table.component';
import { VatDeclarationVatDetailTableComponent } from './vat-declaration-vat-detail-table.component';
import { VatDeclarationDocLinksComponent } from './vat-declaration-doc-links.component';
import { VatDeclarationSummaryPanelComponent } from './vat-declaration-summary-panel.component';
import {
  VatDeclarationExtras,
  VatExtrasKey,
  buildChartSegments,
  buildDocLinks,
  buildTaxRows,
  clampExtrasValue,
  computeTotalCollected,
  computeTotalDeductible,
  computeTotalToPay,
  shiftPeriod
} from './vat-declaration.view-model';

@Component({
  selector: 'app-vat-declaration',
  standalone: true,
  imports: [
    CommonModule,
    PageHeaderComponent,
    AccountingStatusBannerComponent,
    VatDeclarationToolbarComponent,
    VatDeclarationGeneralInfoComponent,
    VatDeclarationStatusPanelComponent,
    VatDeclarationTaxesTableComponent,
    VatDeclarationVatDetailTableComponent,
    VatDeclarationDocLinksComponent,
    VatDeclarationSummaryPanelComponent
  ],
  template: `
    <app-page-header
      title="Déclaration mensuelle des impôts et taxes"
      subtitle="Préremplissage à partir des ventes et achats du mois" />

    <app-vat-declaration-toolbar
      [year]="year"
      [month]="month"
      [loading]="loading()"
      [hasData]="!!data()"
      [showCompanySettings]="!auth.isFirmDelegatedReadonly()"
      [payloadBuilder]="buildVatAnalyzePayload"
      (yearChange)="onYearChange($event)"
      (monthChange)="onMonthChange($event)"
      (refresh)="load()"
      (prevPeriod)="navigatePeriod(-1)"
      (nextPeriod)="navigatePeriod(1)"
      (exportPdf)="exportPdf()"
      (previewPdf)="previewPdf()" />

    <app-accounting-status-banner
      variant="error"
      [message]="error() ?? ''"
      [showRetry]="!!error()"
      retryLabel="Réessayer"
      (retry)="load()" />

    @if (data(); as d) {
      <div class="vat-page-layout">
        <div class="vat-main-column">
          <app-vat-declaration-general-info
            [declaration]="d"
            [year]="year"
            [month]="month" />

          <app-vat-declaration-status-panel [declaration]="d" />

          <app-vat-declaration-taxes-table
            [rows]="taxRows()"
            [extras]="extras()"
            [disabled]="loading()"
            (extraChange)="onExtraChange($event.key, $event.value)" />

          <app-vat-declaration-vat-detail-table [declaration]="d" />

          <app-vat-declaration-doc-links [links]="docLinks()" />
        </div>

        <app-vat-declaration-summary-panel
          class="vat-side-column"
          [declaration]="d"
          [totalToPay]="totalToPay()"
          [currency]="d.currency"
          [loading]="loading()"
          [v2Enabled]="d.monthlyDeclarationV2Enabled"
          [chartSegments]="chartSegments()"
          (saveDraft)="save(false)"
          (submit)="save(true)"
          (rectificative)="save(true, true)"
          (exportPdf)="exportPdf()" />
      </div>
    }
  `,
  styles: `
    .vat-page-layout {
      display: grid;
      grid-template-columns: 1fr;
      gap: var(--spacing-4);
      align-items: start;
    }
    @media (min-width: 64rem) {
      .vat-page-layout {
        grid-template-columns: minmax(0, 1.65fr) minmax(18rem, 1fr);
      }
    }
    .vat-main-column { min-width: 0; }
    .vat-side-column { min-width: 0; }
  `
})
export class VatDeclarationComponent implements OnInit {
  private readonly api = inject(AccountingService);
  readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly title = inject(Title);
  private readonly route = inject(ActivatedRoute);
  private readonly errorHandler = inject(ErrorHandlerService);

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;

  readonly data = signal<VatDeclarationDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  readonly extras = signal<VatDeclarationExtras>({
    fodec: 0, droitTimbre: 0, tcl: 0, tfp: 0, foprolos: 0, withholdingTax: 0, acomptes: 0
  });

  readonly totalToPay = computed(() => {
    const d = this.data();
    if (!d) return 0;
    return computeTotalToPay(d, this.extras());
  });

  readonly taxRows = computed(() => {
    const d = this.data();
    if (!d) return [];
    return buildTaxRows(d, this.extras(), this.totalToPay(), d.fodecRatePercent ?? 1, d.tclRatePercent ?? 0.2);
  });

  readonly chartSegments = computed(() => {
    const d = this.data();
    if (!d) return [];
    return buildChartSegments(d, this.extras(), this.totalToPay());
  });

  readonly docLinks = computed(() => {
    const d = this.data();
    if (!d) return [];
    return buildDocLinks(d, this.year, this.month);
  });

  ngOnInit(): void {
    this.title.setTitle('Déclaration mensuelle - InstaFact');
    // Deep-link depuis l'échéancier fiscal : ?year=2026&month=7 ouvre la bonne période.
    const params = this.route.snapshot.queryParamMap;
    const year = Number(params.get('year'));
    const month = Number(params.get('month'));
    if (Number.isInteger(year) && year >= 2000 && year <= 2100) this.year = year;
    if (Number.isInteger(month) && month >= 1 && month <= 12) this.month = month;
    this.load();
  }

  onYearChange(value: number): void {
    this.year = value;
  }

  onMonthChange(value: number): void {
    this.month = value;
  }

  navigatePeriod(delta: number): void {
    const next = shiftPeriod(this.year, this.month, delta);
    this.year = next.year;
    this.month = next.month;
    this.load();
  }

  onExtraChange(key: VatExtrasKey, value: unknown): void {
    this.patchExtras(key, value);
  }

  patchExtras(key: VatExtrasKey, value: unknown): void {
    this.extras.update(e => ({ ...e, [key]: clampExtrasValue(value) }));
  }

  readonly buildVatAnalyzePayload = (): unknown => {
    const d = this.data();
    if (!d) {
      return wrapLegacyAnalyzePayload(
        'accounting-vat-declaration',
        { screen: 'accounting-vat-declaration', noData: true } as Record<string, unknown>
      );
    }
    const e = this.extras();
    return wrapLegacyAnalyzePayload(
      'accounting-vat-declaration',
      {
        screen: 'accounting-vat-declaration',
        period: { year: this.year, month: this.month },
        collectedVat: {
          rate19: d.collectedVat19,
          rate13: d.collectedVat13,
          rate7: d.collectedVat7,
          total: computeTotalCollected(d)
        },
        deductibleVat: {
          goods: d.deductibleVatGoods,
          assets: d.deductibleVatAssets,
          total: computeTotalDeductible(d)
        },
        previousCredit: d.previousCredit,
        vatDue: d.vatDue,
        creditToCarry: d.creditToCarry,
        status: d.status,
        currency: d.currency,
        otherTaxes: d.monthlyDeclarationV2Enabled ? {
          fodec: e.fodec,
          droitTimbre: e.droitTimbre,
          tcl: e.tcl,
          tfp: e.tfp,
          foprolos: e.foprolos,
          withholdingTax: e.withholdingTax,
          acomptes: e.acomptes,
          totalToPay: this.totalToPay()
        } : undefined,
        filingDeadline: d.filingDeadline,
        version: d.version,
        isRectificative: d.isRectificative
      } as Record<string, unknown>
    );
  };

  load(): void {
    this.clampPeriod();
    this.loading.set(true);
    this.error.set(null);

    this.api.getVatDeclaration(this.year, this.month).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.data.set(res.data);
          const d = res.data;
          this.extras.set({
            fodec: d.fodec,
            droitTimbre: d.droitTimbre,
            tcl: d.tcl,
            tfp: d.tfp,
            foprolos: d.foprolos,
            withholdingTax: d.withholdingTax,
            acomptes: d.acomptes
          });
        } else {
          this.error.set(res.error ?? 'Erreur');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.errorHandler.extractErrorMessage(err));
      }
    });
  }

  save(submit: boolean, rectificative = false): void {
    this.clampPeriod();
    this.loading.set(true);
    this.error.set(null);
    const v2 = this.data()?.monthlyDeclarationV2Enabled ?? false;
    const extrasPayload = v2 ? { ...this.extras(), isRectificative: rectificative } : undefined;
    this.api.saveVatDeclaration(this.year, this.month, submit, extrasPayload).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: rectificative ? 'Rectificative enregistrée' : (submit ? 'Déclaration soumise' : 'Brouillon enregistré'),
            detail: rectificative
              ? 'La déclaration rectificative a été enregistrée.'
              : (submit ? 'La déclaration a été soumise.' : 'Le brouillon de déclaration a été enregistré.'),
            life: 5000
          });
          this.load();
        } else {
          this.error.set(res.error ?? 'Erreur');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.errorHandler.extractErrorMessage(err));
      }
    });
  }

  exportPdf(): void {
    this.clampPeriod();
    this.api.exportVatDeclarationPdf(this.year, this.month).subscribe({
      next: blob => this.downloadPdf(blob),
      error: () => this.toast.add({ severity: 'error', summary: 'Export PDF', detail: "L'export PDF a échoué.", life: 5000 })
    });
  }

  previewPdf(): void {
    this.clampPeriod();
    this.api.exportVatDeclarationPdf(this.year, this.month).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener,noreferrer');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Aperçu PDF', detail: "L'aperçu PDF a échoué.", life: 5000 })
    });
  }

  private downloadPdf(blob: Blob): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `declaration_${this.year}_${String(this.month).padStart(2, '0')}.pdf`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private clampPeriod(): void {
    const y = Math.min(2100, Math.max(2000, Math.floor(Number(this.year)) || new Date().getFullYear()));
    const m = Math.min(12, Math.max(1, Math.floor(Number(this.month)) || 1));
    this.year = y;
    this.month = m;
  }
}
