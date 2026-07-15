import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { StreetApiService, StorefrontDetail, StorefrontProduct } from '../../services/street-api.service';
import { StreetThemeService } from '../../services/street-theme.service';

@Component({
  selector: 'app-storefront-detail-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="wrap">
      <nav class="nav-back" aria-label="Navigation visite virtuelle">
        <a routerLink="/visite-virtuelle">← Rue InstaFact</a>
        <span class="sep" aria-hidden="true">·</span>
        <a routerLink="/visite-virtuelle/liste">Version liste accessible</a>
      </nav>
      @if (detail()) {
        <h1>{{ detail()!.displayName }}</h1>
        @if (detail()!.tagline) {
          <p class="tag">{{ detail()!.tagline }}</p>
        }
        <h2>Articles</h2>
        @if (!products().length) {
          <p>Aucun article public pour cette vitrine.</p>
        } @else {
          <ul class="grid">
            @for (p of products(); track p.id) {
              <li class="card">
                <div class="title">{{ p.name }}</div>
                <div class="price">{{ p.priceAmount | number: '1.3-3' }} {{ p.priceCurrency }}</div>
              </li>
            }
          </ul>
        }
      } @else if (error()) {
        <p role="alert">{{ error() }}</p>
        <p class="nav-hint">
          <a routerLink="/visite-virtuelle">Retour à la rue</a> —
          <a routerLink="/visite-virtuelle/liste">Voir toutes les vitrines (liste)</a>
        </p>
      } @else {
        <p>Chargement…</p>
      }
    </div>
  `,
  styles: [
    `
      .wrap {
        max-width: 900px;
        margin: 2rem auto;
        padding: 0 1rem;
        color: #0f172a;
      }
      .nav-back {
        margin-bottom: 1.25rem;
        font-size: 0.95rem;
      }
      .nav-back .sep {
        margin: 0 0.35rem;
        color: #94a3b8;
      }
      .nav-hint {
        margin-top: 1rem;
        font-size: 0.95rem;
      }
      .tag {
        color: #475569;
      }
      .grid {
        list-style: none;
        padding: 0;
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
        gap: 1rem;
      }
      .card {
        border: 1px solid #e2e8f0;
        border-radius: 10px;
        padding: 1rem;
        background: #fff;
      }
      .title {
        font-weight: 600;
      }
      .price {
        margin-top: 0.5rem;
        color: #0369a1;
      }
    `
  ]
})
export class StorefrontDetailPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(StreetApiService);
  private readonly theme = inject(StreetThemeService);
  private readonly title = inject(Title);

  readonly detail = signal<StorefrontDetail | null>(null);
  readonly products = signal<StorefrontProduct[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.theme.activate();
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.error.set('Adresse de vitrine incomplète.');
      this.title.setTitle('Vitrine — InstaFact');
      return;
    }
    this.api.getStorefront(slug).subscribe({
      next: d => {
        if (!d) {
          this.error.set(
            'Cette vitrine n’existe pas ou n’est pas disponible publiquement. Si vous gérez une vitrine, elle doit être publiée par la plateforme avant d’apparaître ici.'
          );
          this.title.setTitle('Vitrine indisponible — InstaFact');
          return;
        }
        this.error.set(null);
        this.detail.set(d);
        this.title.setTitle(`${d.displayName} — InstaFact`);
        this.api.getProducts(slug, 1, 100).subscribe({
          next: page => this.products.set(page.items),
          error: () => this.error.set('Impossible de charger les articles publics pour cette vitrine.')
        });
      },
      error: () => {
        this.error.set(
          'Impossible de joindre le service des vitrines publiques. Vérifiez votre connexion ou réessayez plus tard.'
        );
        this.title.setTitle('Vitrine — erreur — InstaFact');
      }
    });
  }

  ngOnDestroy(): void {
    this.theme.deactivate();
  }
}
