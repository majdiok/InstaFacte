import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { DtsDeclarationTabComponent } from './dts-declaration-tab.component';
import { WithholdingCertificatesTabComponent } from './withholding-certificates-tab.component';

@Component({
  selector: 'app-payroll-declarations',
  standalone: true,
  imports: [
    CommonModule,
    TabViewModule,
    PageHeaderComponent,
    DtsDeclarationTabComponent,
    WithholdingCertificatesTabComponent
  ],
  template: `
    <app-page-header
      title="Déclarations paie"
      subtitle="DTS CNSS trimestrielle et certificats de retenue à la source (IRPP/CSS). Seuls les cycles validés ou clôturés sont inclus." />

    <p-tabView styleClass="ft-tabs" [(activeIndex)]="activeTabIndex">
      <p-tabPanel>
        <ng-template pTemplate="header">
          <i class="pi pi-building-columns mr-2"></i>
          <span>DTS CNSS</span>
        </ng-template>
        <app-dts-declaration-tab
          [initialYear]="initialYear()"
          [initialQuarter]="initialQuarter()" />
      </p-tabPanel>
      <p-tabPanel>
        <ng-template pTemplate="header">
          <i class="pi pi-file-export mr-2"></i>
          <span>Certificats RS</span>
        </ng-template>
        <app-withholding-certificates-tab [initialYear]="initialYear()" />
      </p-tabPanel>
    </p-tabView>
  `
})
export class PayrollDeclarationsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);

  activeTabIndex = 0;
  readonly initialYear = signal<number | null>(null);
  readonly initialQuarter = signal<number | null>(null);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const year = Number(params.get('year'));
    const quarter = Number(params.get('quarter'));
    const tab = params.get('tab');

    if (Number.isInteger(year) && year >= 2000 && year <= 2100) {
      this.initialYear.set(year);
    }
    if (Number.isInteger(quarter) && quarter >= 1 && quarter <= 4) {
      this.initialQuarter.set(quarter);
    }
    if (tab === 'certificates') {
      this.activeTabIndex = 1;
    }
  }
}
