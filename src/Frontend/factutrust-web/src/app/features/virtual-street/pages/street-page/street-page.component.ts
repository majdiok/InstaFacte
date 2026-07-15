import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { StreetApiService, StreetMapEntry } from '../../services/street-api.service';
import { StreetThemeService } from '../../services/street-theme.service';
import { Street3dCanvasComponent } from '../../components/street-3d-canvas/street-3d-canvas.component';

@Component({
  selector: 'app-street-page',
  standalone: true,
  imports: [CommonModule, RouterLink, Street3dCanvasComponent],
  template: `
    <div class="street-shell">
      <header class="street-topbar">
        <a routerLink="/" class="brand">InstaFact</a>
        <nav class="nav-links" aria-label="Navigation visite virtuelle">
          <a routerLink="/visite-virtuelle/liste" class="a11y-link">Version accessible (liste)</a>
          <a routerLink="/auth/login" class="link-muted">Espace client</a>
        </nav>
      </header>

      <main class="street-main">
        <section class="intro" aria-labelledby="street-title">
          <h1 id="street-title">Rue InstaFact</h1>
          <p class="lede">
            Explorez les vitrines publiques des entreprises présentes sur la plateforme. Vue 3D : faites
            glisser pour orienter la caméra, cliquez sur une façade pour ouvrir — ou utilisez les flèches
            puis Entrée lorsque le focus est sur la scène.
          </p>
        </section>

        @if (error()) {
          <p class="banner-error" role="alert">{{ error() }}</p>
        }

        @if (loading()) {
          <p class="loading" aria-live="polite">Chargement de la rue…</p>
        } @else if (map().length === 0) {
          <p class="empty">Aucune vitrine publiée pour le moment. Revenez bientôt.</p>
        } @else {
          <p id="street-3d-instructions" class="sr-instructions">
            Scène 3D ci-dessous. Tabulation jusqu’à la scène, puis flèches pour parcourir les vitrines,
            Entrée pour ouvrir la vitrine sélectionnée.
          </p>
          <div
            class="street-view-region"
            role="region"
            aria-labelledby="street-3d-instructions"
            [attr.aria-label]="'Vitrines publiées : ' + map().length">
            <div class="visually-hidden" aria-live="polite" aria-atomic="true">
              {{ selectionAnnouncement() }}
            </div>
            @if (!reducedMotion()) {
              <app-street-3d-canvas
                [map]="map()"
                [selectedSlug]="selectedSlug()"
                (storefrontNavigate)="navigateToStore($event)"
                (keyboardSelectionChange)="onKeyboardSelect($event)" />
            } @else {
              <p class="reduced-hint" role="note">
                Les animations 3D sont désactivées (préférence « mouvement réduit »). Utilisez la
                <a routerLink="/visite-virtuelle/liste">version liste</a>.
              </p>
            }

            <ul class="store-list" aria-label="Magasins sur la rue">
              @for (s of mapSorted(); track s.slug) {
                <li [id]="storeRowId(s.slug)">
                  <a
                    [routerLink]="['/visite-virtuelle', s.slug]"
                    class="store-link"
                    [class.store-link--selected]="selectedSlug() === s.slug"
                    (click)="selectedSlug.set(s.slug)"
                    (focusin)="selectedSlug.set(s.slug)">
                    <span class="name">{{ s.displayName }}</span>
                    @if (s.tagline) {
                      <span class="tag">{{ s.tagline }}</span>
                    }
                  </a>
                </li>
              }
            </ul>
          </div>
        }
      </main>
    </div>
  `,
  styles: [
    `
      .street-shell {
        min-height: 100vh;
        background: radial-gradient(1200px 600px at 50% -10%, #1e3a5f 0%, #0b1220 55%);
        color: #e2e8f0;
        display: flex;
        flex-direction: column;
      }
      .street-topbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1rem 1.5rem;
        border-bottom: 1px solid rgba(148, 163, 184, 0.2);
      }
      .brand {
        font-weight: 700;
        letter-spacing: -0.02em;
        color: #f8fafc;
        text-decoration: none;
      }
      .nav-links {
        display: flex;
        gap: 1rem;
        align-items: center;
      }
      .a11y-link {
        color: #38bdf8;
        font-weight: 600;
      }
      .link-muted {
        color: #94a3b8;
      }
      .street-main {
        flex: 1;
        padding: 1.5rem;
        max-width: 1100px;
        margin: 0 auto;
        width: 100%;
      }
      .intro h1 {
        margin: 0 0 0.5rem;
        font-size: 1.75rem;
      }
      .lede {
        margin: 0 0 1.5rem;
        color: #cbd5e1;
        max-width: 62ch;
      }
      .loading,
      .empty {
        color: #94a3b8;
      }
      .banner-error {
        background: rgba(239, 68, 68, 0.15);
        border: 1px solid rgba(248, 113, 113, 0.4);
        color: #fecaca;
        padding: 0.75rem 1rem;
        border-radius: 8px;
      }
      .sr-instructions {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border: 0;
      }
      .visually-hidden {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border: 0;
      }
      .street-view-region {
        position: relative;
      }
      .store-list {
        list-style: none;
        padding: 0;
        margin: 1.5rem 0 0;
        display: grid;
        gap: 0.5rem;
      }
      .store-link {
        display: flex;
        flex-direction: column;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        background: rgba(15, 23, 42, 0.65);
        border: 1px solid rgba(148, 163, 184, 0.2);
        text-decoration: none;
        color: inherit;
      }
      .store-link:hover {
        border-color: #38bdf8;
      }
      .store-link--selected {
        border-color: #38bdf8;
        box-shadow: 0 0 0 1px rgba(56, 189, 248, 0.35);
      }
      .name {
        font-weight: 600;
      }
      .tag {
        font-size: 0.85rem;
        color: #94a3b8;
      }
      .reduced-hint {
        color: #cbd5e1;
      }
      a {
        color: #38bdf8;
      }
    `
  ]
})
export class StreetPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(StreetApiService);
  private readonly theme = inject(StreetThemeService);
  private readonly router = inject(Router);

  readonly map = signal<StreetMapEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly reducedMotion = signal(
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches
  );
  readonly selectedSlug = signal<string | null>(null);

  readonly mapSorted = computed(() =>
    [...this.map()].sort((a, b) => a.streetPositionIndex - b.streetPositionIndex)
  );

  readonly selectionAnnouncement = computed(() => {
    const slug = this.selectedSlug();
    if (!slug) return '';
    const row = this.map().find(s => s.slug === slug);
    if (!row) return '';
    return `Sélection : ${row.displayName}. Entrée pour ouvrir la vitrine.`;
  });

  ngOnInit(): void {
    this.theme.activate();
    this.api.getMap().subscribe({
      next: rows => {
        this.map.set(rows);
        this.loading.set(false);
        const ordered = [...rows].sort((a, b) => a.streetPositionIndex - b.streetPositionIndex);
        if (ordered.length > 0) {
          this.selectedSlug.set(ordered[0]!.slug);
        }
      },
      error: () => {
        this.error.set('Impossible de charger la rue virtuelle. Vérifiez que l’API est disponible.');
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.theme.deactivate();
  }

  storeRowId(slug: string): string {
    return `store-row-${slug.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  }

  navigateToStore(slug: string): void {
    void this.router.navigate(['/visite-virtuelle', slug]);
  }

  onKeyboardSelect(slug: string): void {
    this.selectedSlug.set(slug);
    const id = this.storeRowId(slug);
    document.getElementById(id)?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }
}
