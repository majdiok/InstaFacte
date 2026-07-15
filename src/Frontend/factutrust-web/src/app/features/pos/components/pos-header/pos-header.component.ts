import { Component, inject, OnInit, OnDestroy, signal, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PosOfflineService } from '../../services/pos-offline.service';
import { PosAudioService } from '../../services/pos-audio.service';
import { PosThemeService, PosThemeId } from '../../services/pos-theme.service';
import { PosStateService } from '../../services/pos-state.service';
import { PosVoiceCommandService } from '../../services/pos-voice-command.service';
import { PosIdleService } from '../../services/pos-idle.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';

@Component({
  selector: 'app-pos-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="pos-header">
      <div class="pos-header__left">
        <button class="pos-header__back" (click)="goBack()" title="Retour au tableau de bord" aria-label="Retour">
          <i class="pi pi-arrow-left"></i>
        </button>
        <div class="pos-header__brand">
          <div class="pos-header__logo">
            <i class="pi pi-shopping-cart"></i>
          </div>
          <h1 class="pos-header__title">Point de Vente</h1>
        </div>
          <button class="pos-header__pill" type="button" (click)="onOpenHistory.emit()" title="Historique des transactions" aria-label="Historique">
          <i class="pi pi-folder-open"></i>
          <span>Historiques</span>
        </button>
      </div>

      

      <div class="pos-header__center">
        <div class="pos-header__center-group">
          @if (warehouseContext.resolvedWarehouse(); as wh) {
            <div
              class="pos-header__warehouse"
              role="status"
              [attr.title]="'Entrepôt : ' + wh.name"
              [attr.aria-label]="'Entrepôt actif : ' + wh.name">
              <i class="pi pi-building" aria-hidden="true"></i>
              <span class="pos-header__warehouse-name">{{ wh.name }}</span>
            </div>
          }
          <div class="pos-header__clock" aria-live="off">
            <i class="pi pi-clock" aria-hidden="true"></i>
            <span>{{ currentTime() }}</span>
          </div>
        </div>
      </div>

      <div class="pos-header__right">
        @if (!offlineService.isOnline()) {
          <span class="pos-header__offline" title="Hors ligne">
            <i class="pi pi-wifi-off"></i>
            <span>Hors ligne</span>
          </span>
        }
        @if (heldCount > 0) {
          <button class="pos-header__pill pos-header__pill--held" type="button" (click)="onOpenHeld.emit()" [title]="'En attente (' + heldCount + ')'" [attr.aria-label]="'En attente ' + heldCount + ' commandes'">
            <i class="pi pi-pause"></i>
            <span>En attente ({{ heldCount }})</span>
          </button>
        }
       
        <button
          class="pos-header__pill"
          [class.pos-header__pill--active]="audioService.isSoundEnabled()"
          type="button"
          (click)="audioService.toggleSound()"
          [title]="audioService.isSoundEnabled() ? 'Desactiver le son' : 'Activer le son'"
          [attr.aria-label]="audioService.isSoundEnabled() ? 'Desactiver le son' : 'Activer le son'">
          <i class="pi" [class.pi-volume-up]="audioService.isSoundEnabled()" [class.pi-volume-off]="!audioService.isSoundEnabled()"></i>
          <span>Son</span>
        </button>
        <button
          class="pos-header__pill"
          [class.pos-header__pill--active]="isQuickMode"
          type="button"
          (click)="onQuickModeToggle.emit()"
          title="Mode rapide (F4)"
          aria-label="Mode rapide">
          <i class="pi pi-bolt"></i>
          <span>Mode rapide</span>
        </button>

        <div class="pos-header__theme" (click)="showThemeMenu = !showThemeMenu">
          <button type="button" class="pos-header__pill" title="Theme" aria-label="Changer le theme">
            <i class="pi pi-palette"></i>
            <span>Theme</span>
          </button>
          @if (showThemeMenu) {
            <div class="pos-header__theme-menu">
              @for (t of themeOptions; track t.id) {
                <button type="button" class="pos-header__theme-option" [class.pos-header__theme-option--active]="themeService.currentTheme() === t.id" (click)="setTheme(t.id); showThemeMenu = false">
                  {{ t.label }}
                </button>
              }
            </div>
          }
        </div>
        <button class="pos-header__pill" type="button" (click)="showShortcuts = !showShortcuts" title="Raccourcis clavier" aria-label="Shortcuts">
          <i class="pi pi-th-large"></i>
          <span>Raccourcis</span>
        </button>
        <button class="pos-header__new-order" (click)="onNewOrder.emit()" title="Nouvelle commande (F9)">
          <i class="pi pi-plus"></i>
          <span>Nouveau</span>
        </button>
      </div>

      @if (showShortcuts) {
        <div class="pos-header__shortcuts-panel" (click)="showShortcuts = false">
          <div class="pos-header__shortcuts-content" (click)="$event.stopPropagation()">
            <div class="pos-header__shortcuts-header">
              <h3>Raccourcis clavier</h3>
              <p class="pos-header__shortcuts-desc">Accelerez votre saisie avec ces raccourcis</p>
              <button class="pos-header__shortcuts-close" (click)="showShortcuts = false" aria-label="Fermer">
                <i class="pi pi-times"></i>
              </button>
            </div>
            <div class="shortcut-grid">
              <div class="shortcut-item">
                <kbd>F2</kbd>
                <span>Rechercher un produit</span>
              </div>
              <div class="shortcut-separator"></div>
              <div class="shortcut-item">
                <kbd>F5</kbd>
                <span>Valider & Imprimer</span>
              </div>
              <div class="shortcut-separator"></div>
              <div class="shortcut-item">
                <kbd>F9</kbd>
                <span>Nouvelle commande</span>
              </div>
              <div class="shortcut-separator"></div>
              <div class="shortcut-item">
                <kbd>Echap</kbd>
                <span>Annuler / Fermer</span>
              </div>
              <div class="shortcut-separator"></div>
              <div class="shortcut-item">
                <kbd>F6</kbd>
                <span>Lire le total (accessibilite)</span>
              </div>
            </div>
          </div>
        </div>
      }
    </header>
  `,
  styles: [`
    .pos-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      height: 64px;
      padding: 0 var(--spacing-5);
      background: linear-gradient(135deg, #1a5c4c 0%, #0f4c3d 50%, #0d3d32 100%);
      position: relative;
      z-index: var(--z-sticky);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
    }

    .pos-header__left {
      display: flex;
      align-items: center;
      gap: var(--spacing-4);
    }

    .pos-header__back {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border: none;
      border-radius: var(--radius-lg);
      background: rgba(255, 255, 255, 0.15);
      color: var(--color-white);
      cursor: pointer;
      transition: all var(--transition-fast);
    }

    .pos-header__back:hover {
      background: rgba(255, 255, 255, 0.25);
    }

    .pos-header__brand {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
    }

    .pos-header__logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: var(--radius-lg);
      background: rgba(255, 255, 255, 0.2);
      color: var(--color-white);
      font-size: 1.15rem;
      transition: transform 300ms cubic-bezier(0.4, 0, 0.2, 1);
    }

    .pos-header__logo:hover {
      transform: scale(1.05);
    }

    .pos-header__title {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
      color: var(--color-white);
      line-height: 1.2;
      margin: 0;
      letter-spacing: -0.02em;
    }

    .pos-header__center {
      position: absolute;
      left: 50%;
      transform: translateX(-50%);
      max-width: min(42vw, 420px);
    }

    .pos-header__center-group {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
    }

    .pos-header__warehouse {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: 6px 14px;
      min-width: 0;
      max-width: 220px;
      background: linear-gradient(135deg, #1e3a5f 0%, #0f2744 100%);
      border: 1px solid rgba(96, 165, 250, 0.35);
      border-radius: var(--radius-full);
      box-shadow: 0 1px 0 rgba(255, 255, 255, 0.1) inset;
      color: var(--color-white);
      font-size: var(--font-size-sm);
    }

    .pos-header__warehouse-name {
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.02em;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      min-width: 0;
    }

    .pos-header__warehouse i {
      font-size: 0.9rem;
      color: inherit;
      flex-shrink: 0;
    }

    .pos-header__clock {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: 6px 16px;
      background: rgba(255, 255, 255, 0.2);
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      font-family: 'SF Mono', 'Monaco', 'Consolas', monospace;
      color: var(--color-white);
      font-variant-numeric: tabular-nums;
    }

    .pos-header__clock i {
      font-size: 0.85rem;
      color: inherit;
      flex-shrink: 0;
    }

    .pos-header__right {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
    }

    .pos-header__pill {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      border: none;
      border-radius: var(--radius-full);
      background: rgba(255, 255, 255, 0.2);
      color: var(--color-white);
      cursor: pointer;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      transition: all var(--transition-fast);
    }

    .pos-header__pill:hover {
      background: rgba(255, 255, 255, 0.3);
    }

    .pos-header__pill--active {
      background: rgba(45, 139, 111, 0.5);
      color: var(--color-white);
    }

    .pos-header__pill--demo {
      background: rgba(234, 179, 8, 0.35);
      color: #fef3c7;
    }

    .pos-header__theme {
      position: relative;
    }

    .pos-header__theme-menu {
      position: absolute;
      top: 100%;
      right: 0;
      margin-top: var(--spacing-2);
      background: var(--color-white);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-2xl);
      padding: var(--spacing-2);
      min-width: 140px;
      z-index: 100;
    }

    .pos-header__theme-option {
      display: block;
      width: 100%;
      padding: var(--spacing-2) var(--spacing-3);
      border: none;
      border-radius: var(--radius-md);
      background: transparent;
      color: var(--color-text-primary);
      font-size: var(--font-size-sm);
      text-align: left;
      cursor: pointer;
    }

    .pos-header__theme-option:hover {
      background: var(--color-neutral-100);
    }

    .pos-header__theme-option--active {
      background: var(--color-primary-100);
      color: var(--color-primary-700);
    }

    .pos-header__pill i {
      font-size: 0.9rem;
      color: inherit;
      flex-shrink: 0;
    }

    .pos-header__back i,
    .pos-header__logo i,
    .pos-header__offline i {
      color: inherit;
      flex-shrink: 0;
    }

    .pos-header__new-order i {
      color: inherit;
      flex-shrink: 0;
    }

    .pos-header__new-order {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-5);
      background: linear-gradient(135deg, #2d8b6f 0%, #238b6a 100%);
      color: var(--color-white);
      border: none;
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      cursor: pointer;
      transition: all var(--transition-fast);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    }

    .pos-header__new-order:hover {
      background: linear-gradient(135deg, #35a07d 0%, #2a9b75 100%);
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
    }

    .pos-header__new-order:active {
      transform: translateY(0) scale(0.98);
    }

    .pos-header__offline {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      background: rgba(234, 179, 8, 0.2);
      color: #eab308;
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
    }

    .pos-header__shortcuts-panel {
      position: fixed;
      inset: 0;
      z-index: var(--z-modal);
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(15, 23, 42, 0.25);
      backdrop-filter: blur(4px);
      -webkit-backdrop-filter: blur(4px);
      animation: fadeIn 200ms ease-out;
    }

    .pos-header__shortcuts-content {
      background: var(--color-white);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      box-shadow: var(--shadow-2xl);
      min-width: 400px;
      border: 1px solid var(--color-border-subtle);
      animation: scaleInBounce 280ms cubic-bezier(0.34, 1.2, 0.64, 1) forwards;
    }

    .pos-header__shortcuts-header {
      position: relative;
      margin-bottom: var(--spacing-4);
    }

    .pos-header__shortcuts-content h3 {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
      margin: 0 0 var(--spacing-1) 0;
      color: var(--color-text-primary);
      letter-spacing: -0.02em;
    }

    .pos-header__shortcuts-desc {
      font-size: var(--font-size-sm);
      color: var(--color-text-tertiary);
      margin: 0;
    }

    .pos-header__shortcuts-close {
      position: absolute;
      top: -4px;
      right: -4px;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border: none;
      border-radius: var(--radius-lg);
      background: var(--color-neutral-100);
      color: var(--color-text-tertiary);
      cursor: pointer;
      transition: all 200ms ease;
    }

    .pos-header__shortcuts-close:hover {
      background: var(--color-neutral-200);
      color: var(--color-text-primary);
    }

    .shortcut-grid {
      display: flex;
      flex-direction: column;
      gap: 0;
    }

    .shortcut-item {
      display: flex;
      align-items: center;
      gap: var(--spacing-4);
      padding: var(--spacing-3) 0;
    }

    .shortcut-separator {
      height: 1px;
      background: var(--color-border-subtle);
      margin: 0;
    }

    .shortcut-item kbd {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 64px;
      width: 64px;
      height: 32px;
      padding: 0 var(--spacing-3);
      background: linear-gradient(180deg, var(--color-neutral-100) 0%, var(--color-neutral-200) 100%);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-lg);
      font-family: 'SF Mono', 'Monaco', 'Consolas', monospace;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
      box-shadow: inset 0 2px 0 rgba(255, 255, 255, 0.5), 0 2px 0 rgba(0, 0, 0, 0.06);
    }

    .shortcut-item span {
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @media (max-width: 1024px) {
      .pos-header {
        height: 56px;
        padding: 0 var(--spacing-4);
      }
    }

    @media (max-width: 768px) {
      .pos-header {
        height: 56px;
      }

      .pos-header__back {
        width: 44px;
        height: 44px;
      }

      .pos-header__pill span { display: none; }
      .pos-header__pill i {
        font-size: 1rem;
      }
      .pos-header__new-order span { display: none; }
      .pos-header__new-order {
        min-width: 44px;
        min-height: 44px;
        padding: var(--spacing-2);
      }
      .pos-header__new-order i {
        font-size: 1rem;
      }

      .pos-header__center {
        max-width: min(36vw, 160px);
      }

      .pos-header__clock {
        display: none;
      }

      .pos-header__warehouse {
        max-width: 120px;
        padding: 6px 10px;
      }
    }
  `]
})
export class PosHeaderComponent implements OnInit, OnDestroy {
  @Input() heldCount = 0;
  @Input() isDualScreenOpen = false;
  @Input() isQuickMode = false;
  @Output() onNewOrder = new EventEmitter<void>();
  @Output() onOpenHeld = new EventEmitter<void>();
  @Output() onOpenHistory = new EventEmitter<void>();
  @Output() onDualScreenToggle = new EventEmitter<void>();
  @Output() onQuickModeToggle = new EventEmitter<void>();

  private readonly router = inject(Router);
  readonly offlineService = inject(PosOfflineService);
  readonly audioService = inject(PosAudioService);
  readonly themeService = inject(PosThemeService);
  readonly posState = inject(PosStateService);
  readonly voiceCommandService = inject(PosVoiceCommandService);
  readonly warehouseContext = inject(WarehouseContextService);

  themeOptions: { id: PosThemeId; label: string }[] = [
    { id: 'default', label: 'Defaut' },
    { id: 'christmas', label: 'Noel' },
    { id: 'ramadan', label: 'Ramadan' },
    { id: 'sales', label: 'Soldes' }
  ];
  showThemeMenu = false;

  currentTime = signal(this.getFormattedTime());
  showShortcuts = false;
  private clockInterval?: ReturnType<typeof setInterval>;

  setTheme(id: PosThemeId): void {
    this.themeService.setTheme(id);
  }

  toggleVoiceUndo(): void {
    if (this.voiceCommandService.listening()) {
      this.voiceCommandService.stop();
    } else {
      this.voiceCommandService.startListeningForUndo();
    }
  }

  ngOnInit(): void {
    this.clockInterval = setInterval(() => {
      this.currentTime.set(this.getFormattedTime());
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.clockInterval) clearInterval(this.clockInterval);
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }

  private getFormattedTime(): string {
    return new Date().toLocaleTimeString('fr-TN', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }
}
