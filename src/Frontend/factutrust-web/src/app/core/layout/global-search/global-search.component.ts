import {
  Component,
  ElementRef,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  ViewChild,
  effect,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { GlobalSearchResult, GlobalSearchService } from '../../services/global-search.service';

@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (searchService.isEnabled()) {
      <div class="global-search" [class.global-search--header]="mode === 'header'">
        <div class="global-search__input-wrap" #inputWrap>
          <i class="fa-solid fa-magnifying-glass global-search__icon" aria-hidden="true"></i>
          <input
            #searchInput
            type="search"
            class="global-search__input"
            [class.global-search__input--header]="mode === 'header'"
            [placeholder]="placeholder"
            [(ngModel)]="query"
            (focus)="onFocus()"
            (input)="onQueryChange()"
            (keydown)="onInputKeydown($event)"
            role="combobox"
            aria-autocomplete="list"
            [attr.aria-expanded]="dropdownOpen() || paletteOpen()"
            aria-label="Rechercher" />
          @if (mode === 'header') {
            <kbd class="global-search__hint" aria-hidden="true">Ctrl+K</kbd>
          }
        </div>

        @if (dropdownOpen() && mode === 'header' && !paletteOpen()) {
          <div class="global-search__dropdown" role="listbox">
            <ng-container *ngTemplateOutlet="resultsTemplate"></ng-container>
          </div>
        }
      </div>

      @if (paletteOpen()) {
        <div class="global-search__backdrop" (click)="closeAll()" aria-hidden="true"></div>
        <div class="global-search__palette" role="dialog" aria-modal="true" aria-label="Recherche globale">
          <div class="global-search__palette-inner">
            <div class="global-search__input-wrap global-search__input-wrap--palette">
              <i class="fa-solid fa-magnifying-glass global-search__icon" aria-hidden="true"></i>
              <input
                #paletteInput
                type="search"
                class="global-search__input global-search__input--palette"
                placeholder="Rechercher une page, un document, un client…"
                [(ngModel)]="query"
                (input)="onQueryChange()"
                (keydown)="onInputKeydown($event)"
                autofocus />
            </div>
            <div class="global-search__palette-results" role="listbox">
              <ng-container *ngTemplateOutlet="resultsTemplate"></ng-container>
            </div>
          </div>
        </div>
      }
    }

    <ng-template #resultsTemplate>
      @if (loading()) {
        <div class="global-search__empty">Recherche en cours…</div>
      } @else if (displayResults().length === 0) {
        <div class="global-search__empty">
          @if (query.trim().length >= 2) {
            Aucun résultat pour « {{ query.trim() }} »
          } @else {
            Tapez pour rechercher ou parcourez les raccourcis
          }
        </div>
      } @else {
        @for (group of groupedResults(); track group.label) {
          <div class="global-search__group">
            <div class="global-search__group-label">{{ group.label }}</div>
            @for (item of group.items; track item.id) {
              <button
                type="button"
                class="global-search__item"
                [class.global-search__item--active]="item.id === activeId()"
                (click)="select(item)"
                (mouseenter)="activeId.set(item.id)"
                role="option">
                <i [class]="item.icon.includes('fa-') ? item.icon : 'fa-solid ' + item.icon" aria-hidden="true"></i>
                <span class="global-search__item-text">
                  <span class="global-search__item-title">{{ item.title }}</span>
                  @if (item.subtitle) {
                    <span class="global-search__item-sub">{{ item.subtitle }}</span>
                  }
                </span>
              </button>
            }
          </div>
        }
      }
    </ng-template>
  `,
  styles: [`
    .global-search { position: relative; flex: 1; max-width: 560px; }
    .global-search--header { margin: 0 auto; flex: 1; padding: 0 16px; }
    .global-search__input-wrap { position: relative; display: flex; align-items: center; }
    .global-search__icon { position: absolute; left: 14px; color: var(--color-text-secondary, #64748b); font-size: 14px; pointer-events: none; }
    .global-search__input {
      width: 100%; border: 1px solid var(--color-border-subtle, #e2e8f0); border-radius: 999px;
      padding: 10px 72px 10px 40px; background: rgba(255,255,255,0.9); font-size: 14px;
    }
    .global-search__input--header { min-width: 280px; }
    .global-search__input--palette { border-radius: 12px; padding-left: 40px; font-size: 16px; }
    .global-search__hint {
      position: absolute; right: 12px; font-size: 11px; padding: 2px 6px; border-radius: 6px;
      border: 1px solid var(--color-border-subtle, #e2e8f0); color: var(--color-text-secondary, #64748b); background: #fff;
    }

    /* Mode header — style Cubic minimaliste sur dégradé topbar */
    .global-search--header .global-search__icon {
      color: var(--topbar-fg-muted, rgba(255, 255, 255, 0.75));
    }
    .global-search--header .global-search__input {
      background: transparent;
      border: none;
      border-radius: 0;
      color: var(--topbar-fg, #fff);
      padding: 6px 72px 6px 40px;
    }
    .global-search--header .global-search__input::placeholder {
      color: var(--topbar-fg-muted, rgba(255, 255, 255, 0.75));
    }
    .global-search--header .global-search__input:focus {
      outline: none;
      box-shadow: none;
    }
    .global-search--header .global-search__input-wrap:focus-within {
      background: rgba(255, 255, 255, 0.08);
      border-radius: 4px;
    }
    .global-search--header .global-search__hint {
      border: 1px solid var(--topbar-divider, rgba(255, 255, 255, 0.2));
      color: var(--topbar-fg-muted, rgba(255, 255, 255, 0.75));
      background: transparent;
    }
    .global-search__dropdown {
      position: absolute; top: calc(100% + 8px); left: 0; right: 0; z-index: 1200;
      background: #fff; border: 1px solid var(--color-border-subtle, #e2e8f0); border-radius: 12px;
      box-shadow: 0 16px 40px rgba(15,23,42,0.12); max-height: 420px; overflow: auto; padding: 8px;
    }
    .global-search__backdrop { position: fixed; inset: 0; background: rgba(15,23,42,0.45); z-index: 1300; }
    .global-search__palette {
      position: fixed; inset: 0; z-index: 1310; display: flex; align-items: flex-start; justify-content: center; padding: 10vh 16px;
    }
    .global-search__palette-inner {
      width: min(640px, 100%); background: rgba(255,255,255,0.96); backdrop-filter: blur(12px);
      border-radius: 16px; border: 1px solid var(--color-border-subtle, #e2e8f0);
      box-shadow: 0 24px 60px rgba(15,23,42,0.18); overflow: hidden;
    }
    .global-search__input-wrap--palette { padding: 16px 16px 8px; }
    .global-search__palette-results { max-height: 60vh; overflow: auto; padding: 0 8px 12px; }
    .global-search__group-label {
      font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em;
      color: var(--color-text-secondary, #64748b); padding: 8px 10px 4px;
    }
    .global-search__item {
      width: 100%; display: flex; align-items: flex-start; gap: 10px; border: none; background: transparent;
      text-align: left; padding: 10px; border-radius: 10px; cursor: pointer; color: inherit;
    }
    .global-search__item--active, .global-search__item:hover { background: var(--color-primary-50, #eff6ff); }
    .global-search__item i { width: 18px; margin-top: 2px; color: var(--color-primary-600, #2563eb); }
    .global-search__item-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .global-search__item-title { font-weight: 600; font-size: 14px; }
    .global-search__item-sub { font-size: 12px; color: var(--color-text-secondary, #64748b); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .global-search__empty { padding: 16px; color: var(--color-text-secondary, #64748b); font-size: 14px; }
    @media (max-width: 768px) {
      .global-search--header .global-search__input-wrap { display: none; }
      .global-search__hint { display: none; }
    }
  `]
})
export class GlobalSearchComponent implements OnInit, OnDestroy {
  @Input() mode: 'header' | 'mobile' = 'header';
  @Input() placeholder = 'Rechercher…';
  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('paletteInput') paletteInput?: ElementRef<HTMLInputElement>;
  @ViewChild('inputWrap') inputWrap?: ElementRef<HTMLElement>;

  readonly searchService = inject(GlobalSearchService);
  private readonly router = inject(Router);
  private sub?: Subscription;

  query = '';
  readonly dropdownOpen = signal(false);
  readonly loading = signal(false);
  readonly displayResults = signal<GlobalSearchResult[]>([]);
  readonly activeId = signal<string | null>(null);

  paletteOpen = this.searchService.paletteOpen;

  constructor() {
    effect(() => {
      if (this.searchService.paletteOpen()) {
        queueMicrotask(() => this.paletteInput?.nativeElement?.focus());
      }
    });
  }

  ngOnInit(): void {
    this.sub = this.searchService.documentResults$.subscribe(docs => {
      this.loading.set(false);
      this.displayResults.set(this.searchService.searchAll(this.query, docs));
      this.ensureActiveSelection();
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.dropdownOpen() || this.paletteOpen()) return;
    const target = event.target as Node;
    if (this.inputWrap?.nativeElement.contains(target)) return;
    this.dropdownOpen.set(false);
  }

  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    if (!this.searchService.isEnabled()) return;
    const isK = event.key.toLowerCase() === 'k';
    if ((event.ctrlKey || event.metaKey) && isK) {
      event.preventDefault();
      this.searchService.openPalette();
      this.dropdownOpen.set(false);
      queueMicrotask(() => this.paletteInput?.nativeElement?.focus());
    }
    if (event.key === 'Escape') {
      this.closeAll();
    }
  }

  onFocus(): void {
    if (this.mode === 'header') {
      this.dropdownOpen.set(true);
      this.refreshResults();
    }
  }

  onQueryChange(): void {
    this.refreshResults();
    if (this.query.trim().length >= 2) {
      this.loading.set(true);
      this.searchService.requestDocumentSearch(this.query);
    } else {
      this.loading.set(false);
      this.displayResults.set(this.searchService.searchAll(this.query, []));
      this.ensureActiveSelection();
    }
  }

  onInputKeydown(event: KeyboardEvent): void {
    const results = this.displayResults();
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveSelection(1, results);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveSelection(-1, results);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const active = results.find(r => r.id === this.activeId());
      if (active) this.select(active);
    }
  }

  openPaletteFromMobile(): void {
    this.searchService.openPalette();
  }

  groupedResults(): { label: string; items: GlobalSearchResult[] }[] {
    const groups = new Map<string, GlobalSearchResult[]>();
    for (const item of this.displayResults()) {
      const label = item.group || 'Autres';
      const list = groups.get(label) ?? [];
      list.push(item);
      groups.set(label, list);
    }
    return Array.from(groups.entries()).map(([label, items]) => ({ label, items }));
  }

  select(result: GlobalSearchResult): void {
    this.searchService.recordSelection(result);
    const nav = this.searchService.parseRouteSelection(result);
    this.closeAll();
    this.router.navigate(nav.route, { queryParams: nav.queryParams });
  }

  closeAll(): void {
    this.dropdownOpen.set(false);
    this.searchService.closePalette();
  }

  private refreshResults(): void {
    if (this.query.trim().length < 2) {
      this.displayResults.set(this.searchService.searchAll(this.query, []));
      this.ensureActiveSelection();
    } else {
      this.displayResults.set(this.searchService.searchNavigation(this.query));
      this.ensureActiveSelection();
    }
  }

  private moveSelection(delta: number, results: GlobalSearchResult[]): void {
    if (!results.length) return;
    const idx = results.findIndex(r => r.id === this.activeId());
    const next = idx < 0 ? 0 : Math.max(0, Math.min(results.length - 1, idx + delta));
    this.activeId.set(results[next].id);
  }

  private ensureActiveSelection(): void {
    const results = this.displayResults();
    if (!results.length) {
      this.activeId.set(null);
      return;
    }
    if (!results.some(r => r.id === this.activeId())) {
      this.activeId.set(results[0].id);
    }
  }
}
