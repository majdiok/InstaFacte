import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SkeletonComponent } from './skeleton.component';

export interface SkeletonColumn {
  width: string;
}

@Component({
  selector: 'app-skeleton-table',
  standalone: true,
  imports: [CommonModule, SkeletonComponent],
  template: `
    <div class="skeleton-table">
      @for (row of rowsArray; track $index) {
        <div class="skeleton-row">
          @for (col of columns; track $index) {
            <app-skeleton [width]="col.width" [height]="'1.5rem'"></app-skeleton>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .skeleton-table {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      background: white;
      border-radius: var(--radius-xl);
      border: 1px solid var(--color-neutral-200);
    }

    .skeleton-row {
      display: flex;
      gap: var(--spacing-4);
      align-items: center;
    }
  `]
})
export class SkeletonTableComponent {
  @Input() rows: number = 5;
  @Input() columns: SkeletonColumn[] = [];

  get rowsArray(): number[] {
    return Array(this.rows).fill(0).map((_, i) => i);
  }
}
