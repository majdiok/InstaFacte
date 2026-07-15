import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StreetApiService, StreetMapEntry } from '../../services/street-api.service';
import { StreetThemeService } from '../../services/street-theme.service';

@Component({
  selector: 'app-accessibility-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="wrap">
      <header>
        <a routerLink="/visite-virtuelle">← Rue 3D</a>
        <h1>Vitrines publiques (liste)</h1>
        <p>Parcours entièrement textuel, compatible lecteurs d’écran.</p>
      </header>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      } @else if (loading()) {
        <p>Chargement…</p>
      } @else {
        <ul>
          @for (s of stores(); track s.slug) {
            <li>
              <a [routerLink]="['/visite-virtuelle', s.slug]">{{ s.displayName }}</a>
              @if (s.tagline) {
                — <span>{{ s.tagline }}</span>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [
    `
      .wrap {
        max-width: 720px;
        margin: 2rem auto;
        padding: 0 1rem;
        color: #0f172a;
      }
      a {
        color: #0369a1;
      }
    `
  ]
})
export class AccessibilityListComponent implements OnInit, OnDestroy {
  private readonly api = inject(StreetApiService);
  private readonly theme = inject(StreetThemeService);

  readonly stores = signal<StreetMapEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.theme.activate();
    this.api.getMap().subscribe({
      next: r => {
        this.stores.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Chargement impossible.');
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.theme.deactivate();
  }
}
