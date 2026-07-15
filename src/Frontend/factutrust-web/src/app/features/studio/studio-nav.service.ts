import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { StudioService } from './studio.service';
import { StudioNavNode } from './studio.models';

@Injectable({ providedIn: 'root' })
export class StudioNavService {
  private readonly auth = inject(AuthService);
  private readonly studio = inject(StudioService);

  private readonly _nodes = signal<StudioNavNode[]>([]);
  readonly nodes = this._nodes.asReadonly();

  /** @deprecated flat list for backward compat — prefer nodes() */
  readonly items = computed(() => this.flattenNodes(this._nodes()));

  readonly visible = computed(() =>
    this.auth.hasPermission(PERMISSIONS.studio.designEntities) ||
    this.auth.hasPermission(PERMISSIONS.customData.recordsRead));

  constructor() {
    effect(() => {
      this.auth.user();
      if (this.auth.isAccountingFirm() && !this.auth.isDelegatedMode()) {
        this._nodes.set([]);
        return;
      }
      if (this.auth.hasPermission(PERMISSIONS.customData.recordsRead)) {
        this.refresh();
      } else {
        this._nodes.set([]);
      }
    });
  }

  refresh(): void {
    this.studio.getNav({ skipGlobalErrorUi: true }).subscribe({
      next: res => this._nodes.set(res.success ? (res.data ?? []) : []),
      error: () => this._nodes.set([])
    });
  }

  private flattenNodes(nodes: StudioNavNode[]): { key: string; label: string; icon: string | null; route: string }[] {
    const out: { key: string; label: string; icon: string | null; route: string }[] = [];
    for (const n of nodes) {
      if (n.kind === 'system') {
        if (n.route) out.push({ key: n.key, label: n.label, icon: n.icon, route: n.route });
        for (const c of n.children ?? []) {
          if (c.route) out.push({ key: c.key, label: `  ${c.label}`, icon: c.icon, route: c.route });
        }
      } else if (n.route) {
        out.push({ key: n.key, label: n.label, icon: n.icon, route: n.route });
      }
    }
    return out;
  }
}
