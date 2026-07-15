import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DynamicReportComponent } from '@shared/studio-runtime/dynamic-report.component';
import { StudioService } from './studio.service';
import { ReportResult } from './studio.models';
import { StudioPageShellComponent } from './shared/studio-page-shell.component';
import { STUDIO_BREADCRUMBS } from './shared/studio-breadcrumb.util';
import { BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-studio-report-view',
  standalone: true,
  imports: [CommonModule, RouterModule, ToastModule, DynamicReportComponent, StudioPageShellComponent],
  template: `
    <p-toast></p-toast>
    <app-studio-page-shell
      [title]="reportName()"
      subtitle="Rapport analytique — consultation et export."
      [breadcrumbs]="breadcrumbs()">
      <app-dynamic-report
        [result]="result()"
        [loading]="loading()"
        [exportName]="reportName()" />
    </app-studio-page-shell>
  `,
  styleUrl: './shared/studio-layout.scss',
})
export class StudioReportViewComponent implements OnInit {
  private readonly studio = inject(StudioService);
  private readonly toast = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  readonly result = signal<ReportResult | null>(null);
  readonly loading = signal(true);
  readonly reportName = signal('Rapport');
  readonly breadcrumbs = signal<BreadcrumbItem[]>(STUDIO_BREADCRUMBS.reportView('Rapport'));

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.studio.getReport(id).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.reportName.set(res.data.displayName);
          this.breadcrumbs.set(STUDIO_BREADCRUMBS.reportView(res.data.displayName));
        }
      }
    });
    this.studio.runReport(id).subscribe({
      next: res => { this.loading.set(false); if (res.success) this.result.set(res.data); },
      error: () => { this.loading.set(false); this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Rapport introuvable.' }); }
    });
  }
}
