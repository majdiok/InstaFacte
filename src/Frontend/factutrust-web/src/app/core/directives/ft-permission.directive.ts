import {
  Directive,
  Input,
  OnChanges,
  SimpleChanges,
  TemplateRef,
  ViewContainerRef,
  inject,
  effect
} from '@angular/core';
import { AuthService } from '@core/services/auth.service';

/**
 * Structural directive: renders the template only when permission checks pass.
 * Uses the same rules as {@link AuthService.hasAllPermissions} / {@link AuthService.hasAnyPermission}.
 *
 * @example
 * ```html
 * <ng-container *ftPermission="PERMISSIONS.products.create">...</ng-container>
 * <ng-container *ftPermission="[p1, p2]; mode: 'any'">...</ng-container>
 * ```
 */
@Directive({
  selector: '[ftPermission]',
  standalone: true
})
export class FtPermissionDirective implements OnChanges {
  private readonly tpl = inject(TemplateRef<unknown>);
  private readonly vcr = inject(ViewContainerRef);
  private readonly auth = inject(AuthService);

  /** One permission string or several (see {@link ftPermissionMode}). */
  @Input() ftPermission: string | readonly string[] | null | undefined;

  /** When multiple keys: require all (default) or any. */
  @Input() ftPermissionMode: 'all' | 'any' = 'all';

  private embedded = false;

  constructor() {
    effect(() => {
      this.auth.user();
      this.apply();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['ftPermission'] || changes['ftPermissionMode']) {
      this.apply();
    }
  }

  private apply(): void {
    const raw = this.ftPermission;
    if (raw == null || raw === '') {
      this.clear();
      return;
    }
    const keys = (Array.isArray(raw) ? raw : [raw]).filter(Boolean) as string[];
    if (keys.length === 0) {
      this.clear();
      return;
    }
    const ok =
      this.ftPermissionMode === 'any'
        ? this.auth.hasAnyPermission(keys)
        : this.auth.hasAllPermissions(keys);
    if (ok) {
      if (!this.embedded) {
        this.vcr.createEmbeddedView(this.tpl);
        this.embedded = true;
      }
    } else {
      this.clear();
    }
  }

  private clear(): void {
    if (this.embedded) {
      this.vcr.clear();
      this.embedded = false;
    }
  }
}
