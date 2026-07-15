import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-trust-logos-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="trust-logos-section" aria-labelledby="trust-logos-title">
      <div class="section-container">
        <div class="trust-header">
          <h2 id="trust-logos-title" class="trust-title">Conformité, sécurité et standards reconnus</h2>
          <p class="trust-subtitle">InstaFact s'appuie sur les standards de l'industrie pour garantir fiabilité et conformité légale.</p>
        </div>

        <div class="badges-grid">
          <div class="badge-card" role="listitem">
            <div class="badge-icon">🇹🇳</div>
            <div class="badge-content">
              <strong>TEJ Tunisie</strong>
              <span>Retenues à la source conformes</span>
            </div>
          </div>

          <div class="badge-card" role="listitem">
            <div class="badge-icon">🔐</div>
            <div class="badge-content">
              <strong>JWT + 2FA</strong>
              <span>Authentification sécurisée</span>
            </div>
          </div>

          <div class="badge-card" role="listitem">
            <div class="badge-icon">🛡️</div>
            <div class="badge-content">
              <strong>RGPD ready</strong>
              <span>Protection des données</span>
            </div>
          </div>

          <div class="badge-card" role="listitem">
            <div class="badge-icon">⚖️</div>
            <div class="badge-content">
              <strong>Audit chain</strong>
              <span>Piste d'audit immuable</span>
            </div>
          </div>

          <div class="badge-card" role="listitem">
            <div class="badge-icon">🤖</div>
            <div class="badge-content">
              <span>IA self-hosted</span>
            </div>
          </div>

          <div class="badge-card" role="listitem">
            <div class="badge-icon">📐</div>
            <div class="badge-content">
              <strong>Factur-X ready</strong>
              <span>Architecture e-invoicing</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .trust-logos-section {
      padding: var(--spacing-16) var(--spacing-6);
      background: rgba(249, 250, 251, 0.85);
      position: relative;
      backdrop-filter: blur(0.5px);
    }

    .section-container {
      max-width: 1280px;
      margin: 0 auto;
    }

    .trust-header {
      text-align: center;
      margin-bottom: var(--spacing-12);
      max-width: 720px;
      margin-left: auto;
      margin-right: auto;
    }

    .trust-title {
      font-size: clamp(1.5rem, 3vw, 2rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-3) 0;
    }

    .trust-subtitle {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .badges-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: var(--spacing-4);
    }

    .badge-card {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-lg);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
      transition: all var(--transition-normal);

      &:hover {
        border-color: var(--color-primary-300);
        box-shadow: 0 4px 12px rgba(37, 99, 235, 0.08);
        transform: translateY(-2px);
      }
    }

    .badge-icon {
      font-size: 2rem;
      line-height: 1;
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      border-radius: var(--radius-md);
      background: var(--color-primary-50);
    }

    .badge-content {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;

      strong {
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-900);
        line-height: 1.2;
      }

      span {
        font-size: var(--font-size-xs);
        color: var(--color-neutral-600);
        line-height: 1.3;
      }
    }

    @media (max-width: 1024px) {
      .badges-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    @media (max-width: 768px) {
      .trust-logos-section {
        padding: var(--spacing-10) var(--spacing-4);
      }

      .badges-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .badge-card {
        transition: none;
      }
      .badge-card:hover {
        transform: none;
      }
    }
  `]
})
export class TrustLogosSectionComponent {}
