import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="card card--{{ variant }}"
      [class.card-interactive]="clickable"
      (click)="onClick()"
      [attr.role]="clickable ? 'button' : null"
      [attr.tabindex]="clickable ? 0 : null"
      (keydown.enter)="clickable && onClick()"
      (keydown.space)="onSpaceKey($event)">
      @if (header) {
        <div class="card-header" [class.card-header-colored]="headerColored">
          <ng-content select="[card-header]"></ng-content>
        </div>
      }
      <div class="card-body">
        <ng-content></ng-content>
      </div>
      @if (footer) {
        <div class="card-footer">
          <ng-content select="[card-footer]"></ng-content>
        </div>
      }
    </div>
  `,
  styles: [`
    .card {
      background: var(--color-background-elevated);
      border-radius: var(--radius-xl);
      border: 1px solid var(--color-border-subtle);
      transition: all var(--transition-normal);
    }

    :host-context(.theme-superieur) .card {
      border-radius: var(--superieur-card-radius, 8px);
      box-shadow: var(--superieur-card-shadow, 0 0 20px rgba(0,0,0,0.08));
      border-color: rgba(0, 0, 0, 0.04);
    }

    .card--default {
      padding: var(--card-padding);
      box-shadow: var(--shadow-sm);
    }
    .card--default:hover {
      box-shadow: var(--shadow-md);
      transform: translateY(-2px);
      border-color: var(--color-border-default);
    }

    .card--header {
      padding: 0;
      overflow: hidden;
    }
    .card--header .card-header {
      padding: var(--card-padding);
    }
    .card--header .card-header-colored {
      background: linear-gradient(135deg, var(--color-primary-600), var(--color-primary-700));
      color: white;
    }
    .card--header .card-body { padding: var(--card-padding); }

    .card--actions {
      padding: var(--card-padding);
    }
    .card--actions .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: var(--spacing-4);
      margin-bottom: var(--spacing-4);
      border-bottom: 1px solid var(--color-border-subtle);
    }

    .card-interactive {
      cursor: pointer;
    }
    .card-interactive:hover {
      box-shadow: var(--shadow-lg);
      transform: translateY(-4px);
      border-color: var(--color-primary-300);
    }
    .card-interactive:active { transform: translateY(-2px); }
    .card-interactive:focus-visible {
      outline: 3px solid var(--color-primary-500);
      outline-offset: 2px;
    }

    .card-footer {
      padding: var(--spacing-4) var(--card-padding);
      border-top: 1px solid var(--color-border-subtle);
      background: var(--color-background-subtle);
    }
  `]
})
export class CardComponent {
  @Input() variant: 'default' | 'header' | 'actions' = 'default';
  @Input() header = false;
  @Input() headerColored = false;
  @Input() footer = false;
  @Input() clickable = false;
  @Output() cardClick = new EventEmitter<void>();

  onClick(): void {
    if (this.clickable) this.cardClick.emit();
  }

  onSpaceKey(event: Event): void {
    if (!this.clickable) return;
    const keyboardEvent = event as KeyboardEvent;
    keyboardEvent.preventDefault();
    this.onClick();
  }
}
