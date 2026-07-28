import { Component, signal, inject, computed, effect, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';
import { SecondaryNavComponent } from '../secondary-nav/secondary-nav.component';
import { ChatPanelComponent } from '../../../features/ai-assistant/components/chat-panel/chat-panel.component';
import { AuthService } from '../../services/auth.service';
import { AiChatSessionService } from '../../../features/ai-assistant/services/ai-chat-session.service';
import { AiAssistantShellService } from '../../../features/ai-assistant/services/ai-assistant-shell.service';
import { AssistantAgentScope } from '../../../features/ai-assistant/models/ai-chat.models';
import { resolveScopeFromUrl } from '../../../features/ai-assistant/config/agent-scopes.config';
import { AI_ASSISTANT_MARK_SRC } from '@core/constants/ai-assistant-brand';
import { FirmContextService } from '../../services/firm-context.service';
import { LayoutRouteService } from '../layout-route.service';
import { DrawerOverlayService } from '../../services/drawer-overlay.service';
import { AppNavService } from '../../services/app-nav.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    SidebarComponent,
    HeaderComponent,
    SecondaryNavComponent,
    ChatPanelComponent
  ],
  template: `
    <div class="full_container">
      <div
        class="inner_container"
        [class.layout-has-secondary-nav]="appNav.hasSecondaryNav() && !layoutFlags().hideLayout">
        <app-sidebar
          [collapsed]="sidebarCollapsed()"
          (toggleCollapse)="toggleSidebar()"
          (requestExpand)="expandSidebar()">
        </app-sidebar>
        <div id="content">
          @if (!layoutFlags().hideLayout) {
            <app-header
              [sidebarCollapsed]="sidebarCollapsed()"
              (toggleSidebar)="toggleSidebar()">
            </app-header>
            @if (appNav.hasSecondaryNav()) {
              <app-secondary-nav />
            }
          }
          <div
            class="midde_cont"
            [class.layout-full-width]="layoutFlags().fullWidth"
            [class.layout-distraction-free]="layoutFlags().hideLayout">
            <main
              id="main-content"
              [class.layout-full-width]="layoutFlags().fullWidth">
              <router-outlet></router-outlet>
            </main>
            @if (hasAiAccess() && isAiAssistantRoute()) {
              <app-chat-panel
                [isOpen]="true"
                [embedded]="true"
                [agentScope]="routeAgentScope()"
                (close)="onAiPanelClose()"
              />
            }
          </div>
        </div>
      </div>

      @if (hasAiAccess() && !isAiAssistantRoute() && !layoutFlags().hideLayout && !drawerOverlay.isOpen()) {
        <button
          class="ai-fab"
          (click)="toggleAiPanel()"
          [class.active]="aiPanelOpen()"
          title="Assistant IA">
          @if (aiPanelOpen()) {
            <i class="fa-solid fa-xmark"></i>
          } @else {
            <img
              [src]="aiMarkSrc"
              alt=""
              class="ai-fab-mark"
              aria-hidden="true" />
          }
        </button>

        <app-chat-panel
          [isOpen]="aiPanelOpen()"
          [embedded]="false"
          [agentScope]="floatingPanelScope()"
          (close)="onAiPanelClose()"
        />
      }
    </div>
  `,
  styles: [`
    .full_container {
      --shell-main-max-width: none;
      --shell-content-padding-inline: var(--spacing-4);

      display: flex;
      width: 100%;
      height: 100vh;
      overflow: hidden;
      background-color: var(--color-neutral-50);
    }

    .inner_container {
      display: flex;
      width: 100%;
      height: 100%;
    }

    #content {
      flex: 1;
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
    }

    .midde_cont {
      flex: 1;
      overflow-y: auto;
      padding: var(--spacing-6) var(--shell-content-padding-inline);
      background-color: #fff;
      border-top-left-radius: 16px;
      box-shadow: inset 0 2px 4px rgba(0,0,0,0.02);
    }

    main#main-content {
      max-width: var(--shell-main-max-width);
      margin-inline: 0;
      width: 100%;
    }

    .midde_cont.layout-full-width {
      padding: 0;
      border-top-left-radius: 0;
      box-shadow: none;
    }

    .midde_cont.layout-distraction-free {
      overflow: hidden;
    }

    main#main-content.layout-full-width {
      max-width: none;
      height: 100%;
    }

    .ai-fab {
      position: fixed;
      bottom: 24px;
      right: 24px;
      width: 52px;
      height: 52px;
      border-radius: 16px;
      border: none;
      background: linear-gradient(135deg, #7c3aed, #c026d3);
      color: #fff;
      font-size: 20px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 8px 20px rgba(37, 99, 235, 0.35);
      transition: all 0.2s;
      z-index: var(--z-fab);
    }

    .ai-fab:hover {
      background: linear-gradient(135deg, #6d28d9, #a21caf);
      transform: scale(1.05);
    }

    .ai-fab.active {
      background: var(--color-neutral-600, #4b5563);
      box-shadow: 0 4px 12px rgba(75, 85, 99, 0.35);
    }

    .ai-fab-mark {
      width: 34px;
      height: 34px;
      object-fit: contain;
      border-radius: 50%;
      flex-shrink: 0;
      image-rendering: auto;
    }

    @media (max-width: 768px) {
      .midde_cont {
        padding: var(--spacing-4);
        border-radius: 0;
      }

      .midde_cont.layout-full-width {
        padding: 0;
      }
      .ai-fab {
        bottom: 16px;
        right: 16px;
        width: 48px;
        height: 48px;
      }

      .ai-fab-mark {
        width: 31px;
        height: 31px;
      }
    }
  `]
})
export class MainLayoutComponent implements OnInit {
  readonly aiMarkSrc = AI_ASSISTANT_MARK_SRC;

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly aiSession = inject(AiChatSessionService);
  private readonly aiAssistantShell = inject(AiAssistantShellService);
  private readonly layoutRoute = inject(LayoutRouteService);
  private readonly firmContext = inject(FirmContextService);
  readonly appNav = inject(AppNavService);
  readonly drawerOverlay = inject(DrawerOverlayService);

  readonly layoutFlags = this.layoutRoute.flags;

  readonly routerUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  readonly isAiAssistantRoute = computed(() => this.routerUrl().includes('/ai-assistant'));

  /** Scope expert dérivé de la route (pages dédiées /ai-assistant/<slug> et modules). */
  readonly routeAgentScope = computed(() => resolveScopeFromUrl(this.routerUrl()));

  /**
   * Scope de la bulle flottante : suit la route uniquement quand la bulle est FERMÉE — le scope
   * d'une conversation en cours ne change jamais sous les pieds de l'utilisateur.
   */
  readonly floatingPanelScope = signal<AssistantAgentScope>(AssistantAgentScope.None);

  sidebarCollapsed = signal(false);
  aiPanelOpen = signal(false);

  constructor() {
    effect(
      () => {
        if (this.isAiAssistantRoute()) {
          this.aiPanelOpen.set(false);
        }
      },
      { allowSignalWrites: true }
    );
    effect(
      () => {
        const tick = this.aiAssistantShell.openPanelTick();
        if (tick > 0 && this.hasAiAccess()) {
          this.aiPanelOpen.set(true);
        }
      },
      { allowSignalWrites: true }
    );
    effect(
      () => {
        const suggested = this.routeAgentScope();
        if (!this.aiPanelOpen()) {
          this.floatingPanelScope.set(suggested);
        }
      },
      { allowSignalWrites: true }
    );
  }

  ngOnInit(): void {
    this.firmContext.syncFromUser();
    if (this.hasAiAccess()) {
      this.aiSession.initialize();
    }
  }

  toggleSidebar(): void {
    this.sidebarCollapsed.update(v => !v);
  }

  expandSidebar(): void {
    this.sidebarCollapsed.set(false);
  }

  toggleAiPanel(): void {
    this.aiPanelOpen.update(v => !v);
  }

  onAiPanelClose(): void {
    if (this.isAiAssistantRoute()) {
      void this.router.navigate(['/dashboard']);
    } else {
      this.aiPanelOpen.set(false);
    }
  }

  hasAiAccess(): boolean {
    return this.auth.hasAllPermissions(['ai:chat']);
  }
}
