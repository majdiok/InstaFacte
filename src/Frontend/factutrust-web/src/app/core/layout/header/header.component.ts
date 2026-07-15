import { Component, Input, Output, EventEmitter, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { AuthService } from '../../services/auth.service';
import { WarehouseContextService } from '../../services/warehouse-context.service';
import { FirmContextService } from '../../services/firm-context.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { BreadcrumbComponent } from '@shared/components/breadcrumb/breadcrumb.component';
import { QuickAccessMenuComponent } from './quick-access-menu/quick-access-menu.component';
import { GlobalSearchComponent } from '../global-search/global-search.component';
import { GlobalSearchService } from '../../services/global-search.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule, NgbDropdownModule, BreadcrumbComponent, QuickAccessMenuComponent, GlobalSearchComponent],
  template: `
    <div class="topbar main-header">
      <nav class="navbar navbar-expand-lg">
        <div class="header-content">
          <!-- Toggle button for mobile/sidebar -->
          <button
            type="button"
            class="sidebar_toggle"
            (click)="toggleSidebar.emit()"
            aria-label="Menu">
            <i class="fa fa-bars"></i>
          </button>

          <div class="header-left-actions">
            <app-quick-access-menu />
          </div>

          <!-- Global Search -->
          <app-global-search mode="header" />

          <!-- Right Side -->
          <div class="right_topbar">
            @if (authService.isDelegatedMode() && firmContext.activeClient(); as client) {
              <div class="main-header__firm-context">
                <span>Dossier : <strong>{{ client.companyName }}</strong></span>
                <button type="button" class="btn-link" (click)="returnToFirm()">Retour au cabinet</button>
              </div>
            }
            @if (globalSearch.isEnabled()) {
              <button
                type="button"
                class="header-mobile-search"
                (click)="globalSearch.openPalette()"
                aria-label="Rechercher">
                <i class="fa-solid fa-magnifying-glass"></i>
              </button>
            }
            <!-- Warehouse Badge -->
            @if (warehouseContext.resolvedWarehouse(); as wh) {
              <div
                class="main-header__warehouse"
                role="status"
                [attr.title]="'Entrepôt : ' + wh.name"
                [attr.aria-label]="'Entrepôt actif : ' + wh.name">
                <i class="fa-solid fa-warehouse" aria-hidden="true"></i>
                <span class="main-header__warehouse-name">{{ wh.name }}</span>
              </div>
            }

            <div class="icon_info main-header__icon-actions">
              <ul class="main-header__toolbar-icons">
                @if (authService.canAccessPlatformSettings()) {
                  <li>
                    <a routerLink="/settings" aria-label="Paramètres">
                      <i class="fa-solid fa-gear"></i>
                    </a>
                  </li>
                } @else if (authService.isAccountingFirm() && !authService.isDelegatedMode()) {
                  <li>
                    <a routerLink="/firm/settings" aria-label="Paramètres cabinet">
                      <i class="fa-solid fa-gear"></i>
                    </a>
                  </li>
                }
                <li>
                  <a href="#" (click)="$event.preventDefault()" aria-label="Partager">
                    <i class="fa-solid fa-share-nodes"></i>
                  </a>
                </li>
                <li>
                  <a href="#" (click)="$event.preventDefault()" aria-label="Notifications">
                    <i class="fa-regular fa-bell"></i>
                    @if (notificationCount() > 0) {
                      <span class="badge">{{ notificationCount() }}</span>
                    }
                  </a>
                </li>
              </ul>
              <ul class="user_profile_dd">
                <li ngbDropdown placement="bottom-end">
                  <a
                    ngbDropdownToggle
                    class="dropdown-toggle main-header__user-trigger"
                    id="userDropdown">
                    <div class="user_avatar_image">
                      <img src="assets/theme/pluto/images/layout_img/user_img.jpg" alt="User avatar" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';" />
                      <div class="user_avatar_circle" style="display: none;">{{ getInitials() }}</div>
                    </div>
                  </a>
                  <div ngbDropdownMenu aria-labelledby="userDropdown">
                    <a ngbDropdownItem routerLink="/settings/profile">Mon profil</a>
                    @if (authService.canAccessPlatformSettings()) {
                      <a ngbDropdownItem routerLink="/settings">Paramètres</a>
                    } @else if (authService.isAccountingFirm() && !authService.isDelegatedMode()) {
                      <a ngbDropdownItem routerLink="/firm/settings">Paramètres cabinet</a>
                    }
                    <div ngbDropdownDivider></div>
                    <a ngbDropdownItem (click)="authService.logout()">
                      <span>Déconnexion</span>
                      <i class="fa-solid fa-arrow-right-from-bracket ms-2"></i>
                    </a>
                  </div>
                </li>
              </ul>
            </div>
          </div>
        </div>
      </nav>
    </div>
  `,
  styles: [`
    /* Header « glass » façon maquette — surcharge cosmétique au-dessus de Pluto.
       Les !important globaux (styles.scss) ne portent que sur le positionnement,
       donc background/border/backdrop ne sont pas en conflit. */
    .topbar.main-header {
      background: rgba(255, 255, 255, 0.8);
      -webkit-backdrop-filter: blur(12px);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--color-neutral-200);
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
    }

    .header-content {
      display: flex;
      align-items: center;
      width: 100%;
      min-height: 68px;
      padding: 14px 28px;
      gap: 16px;
    }

    .sidebar_toggle {
      background: transparent;
      border: none;
      color: var(--color-neutral-600);
      font-size: 20px;
      cursor: pointer;
      padding: 8px;
    }

    .header-left-actions {
      display: flex;
      align-items: center;
      flex-shrink: 0;
    }

    /* Modern Placeholder Search Bar */
    .header-mobile-search {
      display: none;
      align-items: center;
      justify-content: center;
      width: 38px;
      height: 38px;
      border: none;
      border-radius: 12px;
      background: transparent;
      color: #64748b;
      font-size: 18px;
      cursor: pointer;
    }

    .header-mobile-search:hover {
      background: #f1f5f9;
      color: #1e293b;
    }

    @media (max-width: 768px) {
      .header-mobile-search {
        display: inline-flex;
      }
    }

    .header-search {
      flex: 1;
      max-width: 400px;
      margin: 0 12px;
      
      .search-input-wrapper {
        position: relative;
        display: flex;
        align-items: center;
        background: var(--color-neutral-100);
        border: 1px solid transparent;
        border-radius: 12px;
        padding: 8px 14px;
        transition: all 0.2s ease;

        &:focus-within {
          background: #fff;
          border-color: var(--color-primary-300);
          box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.18);
        }
      }

      .search-icon {
        color: #94a3b8;
        font-size: 14px;
        margin-right: 8px;
      }

      .search-input {
        border: none;
        background: transparent;
        flex: 1;
        outline: none;
        color: #1e293b;
        font-size: 14px;
        width: 100%;

        &::placeholder {
          color: #94a3b8;
        }
      }
    }

    .main-header__warehouse {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 14px;
      background: var(--color-primary-50);
      border: 1px solid var(--color-primary-200);
      border-radius: 999px;
      color: var(--color-primary-700);
      font-size: 12px;
      font-weight: 600;
    }

    .main-header__warehouse i {
      color: var(--color-primary-600);
    }

    .right_topbar {
      display: flex;
      align-items: center;
      gap: 24px;
      margin-left: auto;
    }

    .main-header__icon-actions {
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .main-header__toolbar-icons {
      display: flex;
      align-items: center;
      gap: 8px;
      list-style: none;
      margin: 0;
      padding: 0;

      li a {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 38px;
        height: 38px;
        border-radius: 12px;
        color: #64748b;
        font-size: 18px;
        transition: all 0.2s;
        position: relative;

        &:hover {
          background: #f1f5f9;
          color: #1e293b;
        }

        .badge {
          position: absolute;
          top: 2px;
          right: 2px;
          background: #ef4444;
          color: white;
          font-size: 10px;
          padding: 2px 4px;
          border-radius: 10px;
          line-height: 1;
        }
      }
    }

    .user_profile_dd {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .main-header__user-trigger {
      display: flex;
      align-items: center;
      cursor: pointer;
      padding: 4px;
      border-radius: 50%;
      border: 2px solid transparent;
      transition: all 0.2s;

      &:hover {
        border-color: #e2e8f0;
      }
    }

    .user_avatar_image {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      overflow: hidden;
      
      img {
        width: 100%;
        height: 100%;
        object-fit: cover;
      }
    }

    .user_avatar_circle {
      width: 100%;
      height: 100%;
      background: #3b82f6;
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 14px;
      text-transform: uppercase;
    }

    @media (max-width: 768px) {
      .right_topbar { gap: 12px; }
      .main-header__toolbar-icons { gap: 0; }
    }

    .main-header__firm-context {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.35rem 0.75rem;
      background: var(--color-primary-50);
      border-radius: var(--radius-lg);
      font-size: 0.875rem;
      color: var(--color-primary-800);
    }

    .main-header__firm-context .btn-link {
      border: none;
      background: none;
      color: var(--color-primary-600);
      cursor: pointer;
      text-decoration: underline;
      font-size: 0.875rem;
    }
  `]
})
export class HeaderComponent {
  @Input() sidebarCollapsed = false;
  @Output() toggleSidebar = new EventEmitter<void>();

  readonly globalSearch = inject(GlobalSearchService);
  readonly authService = inject(AuthService);
  readonly firmContext = inject(FirmContextService);
  warehouseContext = inject(WarehouseContextService);
  breadcrumbService = inject(BreadcrumbService);

  notificationCount = () => 0;

  async returnToFirm(): Promise<void> {
    await this.firmContext.returnToFirmHome();
  }

  async clearFirmContext(): Promise<void> {
    await this.firmContext.navigateToClientList();
  }

  getInitials(): string {
    const user = this.authService.user();
    if (!user) return '?';
    if (user.firstName && user.lastName) {
      return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
    }
    return user.fullName?.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase() || 'U';
  }
}
