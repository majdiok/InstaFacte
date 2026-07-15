import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { BRAND } from '@core/constants/brand';

@Component({
  selector: 'app-public-footer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer id="newsletter" class="public-footer" role="contentinfo">
      <div class="footer-container">
        <div class="footer-cta">
          <div class="cta-text">
            <h2>Prêt à transformer votre facturation ?</h2>
            <p>
              Démarrez gratuitement en 5 minutes. <strong>10 factures/mois offertes</strong>,
              sans carte bancaire, sans engagement.
            </p>
          </div>
          <div class="cta-actions">
            <a routerLink="/auth/register" class="btn-cta-primary">
              <i class="pi pi-rocket" aria-hidden="true"></i>
              Démarrer gratuitement
            </a>
            <a routerLink="/auth/login" class="btn-cta-secondary">
              J'ai déjà un compte
            </a>
          </div>
        </div>

        <div class="footer-grid">
          <div class="footer-brand">
            <div class="brand-logo">
              <img [src]="brand.logoLockup" [alt]="brand.name" class="brand-lockup-img" />
            </div>
            <p class="brand-tagline">
              La suite tout-en-un pour facturer, gérer et prévoir l'activité des PME tunisiennes.
              Conforme TEJ, IA intégrée, sécurisée par défaut.
            </p>
            <div class="brand-badges">
              <span class="badge"><i class="pi pi-verified"></i> TEJ</span>
              <span class="badge"><i class="pi pi-shield"></i> RGPD</span>
              <span class="badge"><i class="pi pi-lock"></i> Audit</span>
            </div>
          </div>

          <nav class="footer-col" aria-labelledby="footer-product">
            <h3 id="footer-product" class="footer-title">Produit</h3>
            <ul class="footer-links">
              <li><a href="#fonctionnalites">Fonctionnalités</a></li>
              <li><a href="#modules">Modules</a></li>
              <li><a href="#tarifs">Tarifs</a></li>
              <li><a href="#conformite">Sécurité & Conformité</a></li>
              @if (storefrontEnabled) {
                <li><a routerLink="/visite-virtuelle">Visite virtuelle 3D</a></li>
              }
            </ul>
          </nav>

          <nav class="footer-col" aria-labelledby="footer-modules">
            <h3 id="footer-modules" class="footer-title">Modules</h3>
            <ul class="footer-links">
              <li><a href="#modules">Factures &amp; Devis</a></li>
              <li><a href="#modules">POS / Caisse</a></li>
              <li><a href="#modules">Stock multi-entrepôts</a></li>
              <li><a href="#modules">Comptabilité</a></li>
              <li><a href="#ia">IA Assistant</a></li>
              <li><a href="#forecasting">Forecasting</a></li>
            </ul>
          </nav>

          <nav class="footer-col" aria-labelledby="footer-resources">
            <h3 id="footer-resources" class="footer-title">Ressources</h3>
            <ul class="footer-links">
              <li><a href="#faq">FAQ</a></li>
              <li><a href="#" aria-disabled="true" title="Bientôt disponible">Documentation</a></li>
              <li><a href="#" aria-disabled="true" title="Bientôt disponible">Blog</a></li>
              <li><a href="#" aria-disabled="true" title="Bientôt disponible">API Docs</a></li>
              <li><a href="#" aria-disabled="true" title="Bientôt disponible">Statut</a></li>
            </ul>
          </nav>

          <nav class="footer-col" aria-labelledby="footer-company">
            <h3 id="footer-company" class="footer-title">Entreprise</h3>
            <ul class="footer-links">
              <li><a href="#cas-usage">À propos</a></li>
              <li><a routerLink="/auth/login">Connexion</a></li>
              <li><a routerLink="/auth/register">Créer un compte</a></li>
              <li><a href="#" aria-disabled="true" title="Bientôt disponible">Contact</a></li>
              <li><a routerLink="/legal/terms">Mentions légales</a></li>
            </ul>
          </nav>
        </div>

        <div class="footer-bottom">
          <div class="copyright">
            <span>&copy; {{ currentYear }} InstaFact.</span>
            <span class="separator">·</span>
            <span>Tous droits réservés.</span>
            <span class="separator">·</span>
            <span class="made-in">Conçu en Tunisie 🇹🇳</span>
          </div>
          <div class="legal-links">
            <a routerLink="/legal/terms">CGU</a>
            <a href="#" aria-disabled="true" title="Bientôt disponible">Confidentialité</a>
            <a href="#" aria-disabled="true" title="Bientôt disponible">Cookies</a>
          </div>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    .public-footer {
      background: #0f172a;
      color: #cbd5e1;
      padding: var(--spacing-16) var(--spacing-6) var(--spacing-8);
      position: relative;
      overflow: hidden;
    }

    .public-footer::before {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      height: 4px;
      background: linear-gradient(90deg, var(--color-primary-500), #06b6d4, #14b8a6);
    }

    .footer-container {
      max-width: 1280px;
      margin: 0 auto;
      position: relative;
      z-index: 1;
    }

    .footer-cta {
      display: grid;
      grid-template-columns: 1.4fr 1fr;
      gap: var(--spacing-8);
      align-items: center;
      padding: var(--spacing-8) var(--spacing-10);
      background: linear-gradient(135deg, #1e3a8a 0%, #3730a3 100%);
      border-radius: var(--radius-2xl);
      margin-bottom: var(--spacing-12);
      box-shadow: 0 20px 50px rgba(37, 99, 235, 0.25);
    }

    .cta-text h2 {
      font-size: clamp(1.5rem, 3vw, 2rem);
      font-weight: var(--font-weight-bold);
      color: white;
      margin: 0 0 var(--spacing-3) 0;
      line-height: var(--line-height-tight);
    }

    .cta-text p {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: rgba(255, 255, 255, 0.85);
      margin: 0;

      strong {
        color: #fbbf24;
      }
    }

    .cta-actions {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
    }

    .btn-cta-primary,
    .btn-cta-secondary {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      padding: var(--spacing-4) var(--spacing-6);
      border-radius: var(--radius-lg);
      font-weight: var(--font-weight-semibold);
      text-decoration: none;
      transition: all var(--transition-normal);
    }

    .btn-cta-primary {
      background: white;
      color: var(--color-primary-700);
      box-shadow: var(--shadow-md);

      &:hover {
        transform: translateY(-2px);
        box-shadow: var(--shadow-lg);
      }
    }

    .btn-cta-secondary {
      background: transparent;
      color: white;
      border: 1px solid rgba(255, 255, 255, 0.3);

      &:hover {
        background: rgba(255, 255, 255, 0.1);
        border-color: rgba(255, 255, 255, 0.5);
      }
    }

    .footer-grid {
      display: grid;
      grid-template-columns: 2fr 1fr 1fr 1fr 1fr;
      gap: var(--spacing-8);
      padding-bottom: var(--spacing-10);
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    }

    .footer-brand {
      max-width: 360px;
    }

    .brand-logo {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      margin-bottom: var(--spacing-4);
    }

    .brand-lockup-img {
      max-height: 48px;
      max-width: 220px;
      width: auto;
      object-fit: contain;
    }

    .brand-tagline {
      font-size: var(--font-size-sm);
      line-height: var(--line-height-relaxed);
      color: #94a3b8;
      margin: 0 0 var(--spacing-4) 0;
    }

    .brand-badges {
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-2);
    }

    .badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 4px 10px;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      color: #cbd5e1;

      i {
        font-size: 0.7rem;
        color: #14b8a6;
      }
    }

    .footer-title {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: white;
      margin: 0 0 var(--spacing-4) 0;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .footer-links {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      li {
        font-size: var(--font-size-sm);
      }

      a {
        color: #94a3b8;
        text-decoration: none;
        transition: color var(--transition-normal);

        &:hover {
          color: white;
        }

        &[aria-disabled='true'] {
          color: #64748b;
          cursor: not-allowed;
          pointer-events: none;
          opacity: 0.7;
        }
      }
    }

    .footer-bottom {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--spacing-4);
      padding-top: var(--spacing-6);
    }

    .copyright {
      font-size: var(--font-size-sm);
      color: #94a3b8;
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-2);
    }

    .copyright .separator {
      color: #475569;
    }

    .made-in {
      color: #cbd5e1;
    }

    .legal-links {
      display: flex;
      gap: var(--spacing-5);
    }

    .legal-links a {
      font-size: var(--font-size-sm);
      color: #94a3b8;
      text-decoration: none;
      transition: color var(--transition-normal);

      &:hover {
        color: white;
      }

      &[aria-disabled='true'] {
        color: #64748b;
        cursor: not-allowed;
        pointer-events: none;
      }
    }

    @media (max-width: 1024px) {
      .footer-grid {
        grid-template-columns: 1fr 1fr;
      }

      .footer-brand {
        grid-column: 1 / -1;
        max-width: none;
      }

      .footer-cta {
        grid-template-columns: 1fr;
        text-align: center;
      }

      .cta-actions {
        flex-direction: row;
        justify-content: center;
      }
    }

    @media (max-width: 768px) {
      .public-footer {
        padding: var(--spacing-10) var(--spacing-4) var(--spacing-6);
      }

      .footer-cta {
        padding: var(--spacing-6) var(--spacing-5);
      }

      .footer-grid {
        grid-template-columns: 1fr;
        gap: var(--spacing-6);
      }

      .cta-actions {
        flex-direction: column;
      }

      .footer-bottom {
        flex-direction: column;
        text-align: center;
      }

      .copyright {
        justify-content: center;
      }
    }
  `]
})
export class PublicFooterComponent {
  readonly storefrontEnabled = environment.storefrontEnabled;
  readonly currentYear = new Date().getFullYear();
  readonly brand = BRAND;
}
