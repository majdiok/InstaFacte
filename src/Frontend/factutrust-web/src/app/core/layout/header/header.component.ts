import { Component, Input, Output, EventEmitter, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { AuthService } from '../../services/auth.service';
import { AppNotification, NotificationService } from '../../services/notification.service';
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
            [attr.aria-expanded]="!sidebarCollapsed"
            [attr.aria-label]="sidebarCollapsed ? 'Agrandir le menu latéral' : 'Réduire le menu latéral'">
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
                <li ngbDropdown placement="bottom-end" (openChange)="onNotificationsOpenChange($event)">
                  <a
                    ngbDropdownToggle
                    id="notificationsDropdown"
                    role="button"
                    class="notif-bell"
                    aria-label="Notifications">
                    <i class="fa-regular fa-bell"></i>
                    @if (notifications.unreadCount() > 0) {
                      <span class="badge">{{ notifications.unreadCount() > 99 ? '99+' : notifications.unreadCount() }}</span>
                    }
                  </a>
                  <div ngbDropdownMenu aria-labelledby="notificationsDropdown" class="notif-menu">
                    <div class="notif-menu__header">
                      <span class="notif-menu__title">Notifications</span>
                      @if (notifications.unreadCount() > 0) {
                        <button type="button" class="notif-menu__mark-all" (click)="markAllRead($event)">
                          Tout marquer comme lu
                        </button>
                      }
                    </div>
                    @if (notifications.latest().length === 0) {
                      <div class="notif-menu__empty">
                        <i class="fa-regular fa-bell-slash" aria-hidden="true"></i>
                        <p>Aucune notification</p>
                      </div>
                    } @else {
                      <div class="notif-menu__list">
                        @for (n of notifications.latest(); track n.id) {
                          <button
                            type="button"
                            class="notif-item"
                            [class.notif-item--unread]="!n.readAt"
                            (click)="openNotification(n)">
                            <span class="notif-item__dot" aria-hidden="true"></span>
                            <span class="notif-item__content">
                              <span class="notif-item__title">{{ n.title }}</span>
                              <span class="notif-item__body">{{ n.body }}</span>
                              <span class="notif-item__date">{{ n.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                            </span>
                          </button>
                        }
                      </div>
                    }
                  </div>
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

    .notif-bell {
      cursor: pointer;
    }

    .notif-menu {
      width: 360px;
      max-width: calc(100vw - 32px);
      padding: 0;
      border: 1px solid var(--color-neutral-200);
      border-radius: 14px;
      box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12);
      overflow: hidden;
    }

    .notif-menu__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-neutral-200);
      background: var(--color-neutral-50, #f8fafc);
    }

    .notif-menu__title {
      font-weight: 600;
      font-size: 14px;
      color: #1e293b;
    }

    .notif-menu__mark-all {
      border: none;
      background: none;
      padding: 0;
      font-size: 12px;
      color: var(--color-primary-600);
      cursor: pointer;

      &:hover {
        text-decoration: underline;
      }
    }

    .notif-menu__empty {
      padding: 28px 16px;
      text-align: center;
      color: #94a3b8;

      i {
        font-size: 24px;
        margin-bottom: 8px;
      }

      p {
        margin: 0;
        font-size: 13px;
      }
    }

    .notif-menu__list {
      max-height: 380px;
      overflow-y: auto;
    }

    .notif-item {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      width: 100%;
      padding: 12px 16px;
      border: none;
      border-bottom: 1px solid var(--color-neutral-100, #f1f5f9);
      background: transparent;
      text-align: left;
      cursor: pointer;
      transition: background 0.15s;

      &:hover {
        background: #f1f5f9;
      }

      &:last-child {
        border-bottom: none;
      }
    }

    .notif-item__dot {
      flex-shrink: 0;
      width: 8px;
      height: 8px;
      margin-top: 6px;
      border-radius: 50%;
      background: transparent;
    }

    .notif-item--unread .notif-item__dot {
      background: var(--color-primary-600);
    }

    .notif-item__content {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    .notif-item__title {
      font-size: 13px;
      font-weight: 600;
      color: #1e293b;
    }

    .notif-item--unread .notif-item__title {
      color: var(--color-primary-700);
    }

    .notif-item__body {
      font-size: 12px;
      color: #64748b;
      overflow: hidden;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }

    .notif-item__date {
      font-size: 11px;
      color: #94a3b8;
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
  readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  warehouseContext = inject(WarehouseContextService);
  breadcrumbService = inject(BreadcrumbService);

  onNotificationsOpenChange(open: boolean): void {
    if (open) {
      this.notifications.refresh();
    }
  }

  openNotification(notification: AppNotification): void {
    if (!notification.readAt) {
      this.notifications.markRead(notification.id);
    }
    if (notification.linkUrl) {
      this.router.navigateByUrl(notification.linkUrl);
    }
  }

  markAllRead(event: Event): void {
    event.stopPropagation();
    this.notifications.markAllRead();
  }

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
