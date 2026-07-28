import {
  Component,
  QueryList,
  ViewChildren,
  computed,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { NgbDropdown, NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { filter, map, startWith } from 'rxjs';
import { NavItem, NavSubItem } from '@core/config/app-navigation.registry';
import { secondaryNavDisplayLabel } from '@core/config/secondary-nav.config';
import { AppNavService } from '@core/services/app-nav.service';
import { FirmContextService } from '@core/services/firm-context.service';
import {
  isNavChildActive,
  isNavRouteActive,
  findLongestMatchingChildInParent,
  findMatchingDirectChild,
  stripPathForMatch,
  pathMatchesRoute
} from '@core/utils/nav-path-match';

@Component({
  selector: 'app-secondary-nav',
  standalone: true,
  imports: [CommonModule, RouterModule, NgbDropdownModule],
  templateUrl: './secondary-nav.component.html',
  styleUrl: './secondary-nav.component.scss'
})
export class SecondaryNavComponent {
  private readonly appNav = inject(AppNavService);
  private readonly router = inject(Router);
  private readonly firmContext = inject(FirmContextService);

  @ViewChildren(NgbDropdown) private readonly dropdowns!: QueryList<NgbDropdown>;

  readonly sections = this.appNav.secondaryNavSections;

  /** Label of the nested flyout currently open (hover / focus / click). */
  readonly openSubmenuLabel = signal<string | null>(null);

  private readonly routerUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  constructor() {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        this.closeAllDropdowns();
        this.openSubmenuLabel.set(null);
      });
  }

  readonly activeSectionLabel = computed(() => {
    const url = this.routerUrl();
    const path = stripPathForMatch(url);
    let bestLabel: string | null = null;
    let bestLen = -1;

    for (const section of this.sections()) {
      if (section.children?.length) {
        const child = findLongestMatchingChildInParent(path, section);
        if (child?.route && child.route.length > bestLen) {
          bestLen = child.route.length;
          bestLabel = section.label;
        }
      } else if (section.route && isNavRouteActive(url, section.route) && section.route.length > bestLen) {
        bestLen = section.route.length;
        bestLabel = section.label;
      }
    }
    return bestLabel;
  });

  displayLabel(section: NavItem): string {
    return secondaryNavDisplayLabel(section.label);
  }

  isSectionActive(section: NavItem): boolean {
    return this.activeSectionLabel() === section.label;
  }

  isChildActive(section: NavItem, child: NavSubItem): boolean {
    const path = stripPathForMatch(this.routerUrl());
    if (child.children?.length) {
      return findMatchingDirectChild(path, section)?.label === child.label;
    }
    if (!child.route) {
      return false;
    }
    const best = findLongestMatchingChildInParent(path, section);
    return best?.route === child.route;
  }

  isNestedChildActive(grand: NavSubItem): boolean {
    if (!grand.route) {
      return false;
    }
    return pathMatchesRoute(stripPathForMatch(this.routerUrl()), grand.route);
  }

  isTopLevelActive(section: NavItem): boolean {
    const url = this.routerUrl();
    if (section.children?.length) {
      return isNavChildActive(url, section);
    }
    return isNavRouteActive(url, section.route);
  }

  hasNestedChildren(child: NavSubItem): boolean {
    return !!child.children?.length;
  }

  /** Hub overview link prepended in nested flyouts. */
  nestedHubItem(child: NavSubItem): NavSubItem {
    return {
      label: 'Tous les états',
      route: child.route,
      icon: child.icon
    };
  }

  isSubmenuOpen(child: NavSubItem): boolean {
    return this.openSubmenuLabel() === child.label;
  }

  openSubmenu(child: NavSubItem): void {
    if (child.children?.length) {
      this.openSubmenuLabel.set(child.label);
    }
  }

  closeSubmenu(): void {
    this.openSubmenuLabel.set(null);
  }

  toggleSubmenu(child: NavSubItem, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (!child.children?.length) {
      return;
    }
    this.openSubmenuLabel.update(current => (current === child.label ? null : child.label));
  }

  onSubmenuKeydown(child: NavSubItem, event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.closeSubmenu();
      return;
    }
    if (event.key === 'ArrowRight' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openSubmenu(child);
    }
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.closeSubmenu();
    }
  }

  menuId(section: NavItem): string {
    return `secondary-nav-menu-${section.label.replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-|-$/g, '')}`;
  }

  submenuId(child: NavSubItem): string {
    return `secondary-nav-submenu-${child.label.replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-|-$/g, '')}`;
  }

  async onChildClick(child: NavSubItem, dropdown: NgbDropdown, event: Event): Promise<void> {
    event.preventDefault();
    this.closeSubmenu();
    dropdown.close();
    if (child.action === 'returnToFirm') {
      await this.firmContext.returnToFirmHome();
      return;
    }
    if (child.action === 'changeDossier') {
      await this.firmContext.navigateToClientList();
      return;
    }
    if (child.route) {
      await this.router.navigateByUrl(child.route);
    }
  }

  async onTopLevelClick(section: NavItem, event: Event): Promise<void> {
    event.preventDefault();
    this.closeAllDropdowns();
    this.closeSubmenu();
    if (section.route) {
      await this.router.navigateByUrl(section.route);
    }
  }

  private closeAllDropdowns(): void {
    this.dropdowns?.forEach(dd => dd.close());
  }
}
