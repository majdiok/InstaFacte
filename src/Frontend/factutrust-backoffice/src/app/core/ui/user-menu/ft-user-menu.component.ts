import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Input,
  OnDestroy,
  ViewChild,
  inject
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { FtOverlayCleanupService } from '@core/services/ft-overlay-cleanup.service';

/**
 * Menu profil utilisateur (popup PrimeNG) avec gestion robuste des overlays.
 *
 * Conventions alignées sur {@link FtCellActionsMenuComponent} :
 * - `appendTo="body"` + `baseZIndex="1000"`
 * - `hide()` à la destruction et avant navigation
 * - nettoyage des overlays orphelins (PrimeNG #19213)
 */
@Component({
  selector: 'ft-user-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MenuModule],
  template: `
    <p-menu
      #menu
      [model]="items"
      [popup]="true"
      appendTo="body"
      [baseZIndex]="1000"
      styleClass="ft-user-menu-panel"
    />
    <button
      type="button"
      class="user-btn"
      (click)="onToggle($event)"
      [attr.aria-label]="ariaLabel"
      [attr.aria-haspopup]="true"
      [attr.aria-expanded]="menuVisible"
    >
      <ng-content />
    </button>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }

      .user-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.55rem;
        padding: 0.3rem 0.6rem 0.3rem 0.4rem;
        background: transparent;
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        cursor: pointer;
        color: var(--ft-text);
        transition: background var(--duration-fast) var(--easing-standard),
          border-color var(--duration-fast) var(--easing-standard);
      }

      .user-btn:hover {
        background: var(--ft-surface-3);
        border-color: var(--ft-border-strong);
      }

      .user-btn:focus-visible {
        outline: 2px solid var(--ft-accent);
        outline-offset: 2px;
      }
    `
  ]
})
export class FtUserMenuComponent implements OnDestroy {
  @Input({ required: true }) items: MenuItem[] = [];
  @Input({ required: true }) ariaLabel = 'Menu utilisateur';

  @ViewChild('menu') menu?: Menu;

  menuVisible = false;

  private readonly router = inject(Router);
  private readonly overlayCleanup = inject(FtOverlayCleanupService);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationStart => event instanceof NavigationStart),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        this.hide();
        this.overlayCleanup.clearOrphanOverlays();
      });
  }

  ngOnDestroy(): void {
    this.hide();
    this.overlayCleanup.clearOrphanOverlays();
  }

  onToggle(event: Event): void {
    event.stopPropagation();
    this.menu?.toggle(event);
    this.menuVisible = !this.menuVisible;
  }

  hide(): void {
    this.menu?.hide();
    this.menuVisible = false;
  }

  navigateAndClose(path: string | string[]): void {
    this.hide();
    this.overlayCleanup.clearOrphanOverlays();
    if (Array.isArray(path)) {
      void this.router.navigate(path);
    } else {
      void this.router.navigateByUrl(path);
    }
  }

  runAndClose(action: () => void): void {
    this.hide();
    this.overlayCleanup.clearOrphanOverlays();
    action();
  }
}
