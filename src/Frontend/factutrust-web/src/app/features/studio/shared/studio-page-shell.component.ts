import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

@Component({
  selector: 'app-studio-page-shell',
  standalone: true,
  imports: [CommonModule, BreadcrumbComponent, PageHeaderComponent],
  template: `
    <div class="studio-page">
      @if (breadcrumbs.length) {
        <app-breadcrumb [items]="breadcrumbs" />
      }
      @if (title) {
        <app-page-header [title]="title" [subtitle]="subtitle ?? ''">
          <ng-content select="[studioActions]" />
        </app-page-header>
      }
      <ng-content />
    </div>
  `,
  styleUrl: './studio-layout.scss',
})
export class StudioPageShellComponent {
  @Input() title = '';
  @Input() subtitle: string | null = null;
  @Input() breadcrumbs: BreadcrumbItem[] = [];
}