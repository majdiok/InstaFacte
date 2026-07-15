import { Component, ChangeDetectionStrategy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

const STORAGE_KEY = 'factutrust_promo_banner_dismissed_v1';
const REAPPEAR_AFTER_MS = 7 * 24 * 60 * 60 * 1000; // 7 jours

@Component({
  selector: 'app-promo-banner',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <aside class="promo-banner" role="region" aria-label="Promotion de lancement">
        <div class="promo-banner-inner">
          <span class="promo-emoji" aria-hidden="true">🎁</span>
          <p class="promo-text">
            <strong>Lancement Tunisie 2026</strong> —
            <span class="promo-detail">10 factures/mois gratuites + Configuration assistée. <strong>Aucune CB requise.</strong></span>
          </p>
          <a routerLink="/auth/register" class="promo-cta">
            Démarrer
            <span class="promo-cta-arrow" aria-hidden="true">→</span>
          </a>
          <button
            type="button"
            class="promo-close"
            aria-label="Fermer le bandeau de promotion"
            (click)="dismiss()">
            <span aria-hidden="true">×</span>
          </button>
        </div>
      </aside>
    }
  `,
  styles: [`
    .promo-banner {
      width: 100%;
      background: linear-gradient(90deg, #6366f1 0%, #06b6d4 100%);
      color: #ffffff;
      box-shadow: 0 1px 3px rgba(15, 23, 42, 0.1);
      position: relative;
      z-index: 1100;
    }

    .promo-banner-inner {
      max-width: 1280px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.65rem 1.25rem;
    }

    .promo-emoji {
      font-size: 1.25rem;
      line-height: 1;
      flex-shrink: 0;
    }

    .promo-text {
      flex: 1;
      margin: 0;
      font-size: 0.92rem;
      line-height: 1.45;
      color: #ffffff;
    }

    .promo-text strong {
      color: #ffffff;
      font-weight: 700;
    }

    .promo-detail {
      opacity: 0.95;
    }

    .promo-cta {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.45rem 1rem;
      border-radius: 999px;
      background: #ffffff;
      color: #1e3a8a;
      text-decoration: none;
      font-weight: 700;
      font-size: 0.9rem;
      flex-shrink: 0;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }

    .promo-cta:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 10px rgba(15, 23, 42, 0.15);
    }

    .promo-cta:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: 2px;
    }

    .promo-cta-arrow {
      transition: transform 0.2s ease;
    }

    .promo-cta:hover .promo-cta-arrow {
      transform: translateX(2px);
    }

    .promo-close {
      flex-shrink: 0;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.15);
      border: none;
      color: #ffffff;
      font-size: 1.4rem;
      line-height: 1;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.2s ease;
    }

    .promo-close:hover {
      background: rgba(255, 255, 255, 0.3);
    }

    .promo-close:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: 2px;
    }

    @media (max-width: 768px) {
      .promo-banner-inner {
        flex-wrap: wrap;
        gap: 0.5rem;
        padding: 0.65rem 0.85rem;
      }

      .promo-text {
        flex: 1 1 100%;
        order: 2;
        font-size: 0.85rem;
      }

      .promo-emoji {
        order: 1;
      }

      .promo-cta {
        order: 3;
        font-size: 0.85rem;
        padding: 0.4rem 0.85rem;
      }

      .promo-close {
        order: 4;
        width: 28px;
        height: 28px;
        font-size: 1.2rem;
      }

      .promo-detail {
        display: block;
      }
    }
  `]
})
export class PromoBannerComponent implements OnInit {
  readonly visible = signal(true);

  ngOnInit(): void {
    if (typeof window === 'undefined') return;
    try {
      const dismissedAt = window.localStorage.getItem(STORAGE_KEY);
      if (dismissedAt) {
        const elapsed = Date.now() - Number(dismissedAt);
        if (!isNaN(elapsed) && elapsed < REAPPEAR_AFTER_MS) {
          this.visible.set(false);
        }
      }
    } catch {
      // localStorage indisponible (mode privé) → on garde le bandeau visible
    }
  }

  dismiss(): void {
    this.visible.set(false);
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(STORAGE_KEY, String(Date.now()));
    } catch {
      // ignore
    }
  }
}
