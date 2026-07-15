import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-use-cases-section',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="cas-usage" class="usecases-section" aria-labelledby="usecases-title">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">CAS D'USAGE</span>
          <h2 id="usecases-title" class="section-title">Une plateforme qui s'adapte à votre métier</h2>
          <p class="section-description">
            Que vous soyez prestataire de services, commerçant, distributeur ou comptable, InstaFact répond à vos besoins spécifiques.
          </p>
        </div>

        <div class="usecases-grid">
          <article class="usecase-card persona-services">
            <div class="usecase-emoji" aria-hidden="true">🏢</div>
            <h3 class="usecase-title">PME de services</h3>
            <p class="usecase-subtitle">Cabinets conseil, agences digitales, consultants</p>
            <p class="usecase-description">
              Des devis professionnels, facturation rapide, suivi des paiements et CRM léger pour piloter votre activité commerciale.
            </p>
            <ul class="usecase-modules">
              <li><i class="pi pi-file-edit"></i> Factures & Devis</li>
              <li><i class="pi pi-users"></i> CRM Commercial</li>
              <li><i class="pi pi-wallet"></i> Suivi paiements</li>
              <li><i class="pi pi-chart-bar"></i> Reporting</li>
            </ul>
            <a routerLink="/auth/register" class="usecase-cta">
              Démarrer
              <i class="pi pi-arrow-right"></i>
            </a>
          </article>

          <article class="usecase-card persona-retail">
            <div class="usecase-emoji" aria-hidden="true">🛒</div>
            <h3 class="usecase-title">Commerce / Retail</h3>
            <p class="usecase-subtitle">Boutiques, restaurants, points de vente</p>
            <p class="usecase-description">
              Caisse moderne tactile, gestion de stock multi-entrepôts, encaissement multi-moyens, sessions de caisse.
            </p>
            <ul class="usecase-modules">
              <li><i class="pi pi-shopping-cart"></i> POS / Caisse</li>
              <li><i class="pi pi-box"></i> Stock & Inventaire</li>
              <li><i class="pi pi-credit-card"></i> Encaissement</li>
              <li><i class="pi pi-chart-line"></i> Forecasting</li>
            </ul>
            <a routerLink="/auth/register" class="usecase-cta">
              Démarrer
              <i class="pi pi-arrow-right"></i>
            </a>
          </article>

          <article class="usecase-card persona-distrib">
            <div class="usecase-emoji" aria-hidden="true">📦</div>
            <h3 class="usecase-title">Distributeur B2B</h3>
            <p class="usecase-subtitle">Importateurs, grossistes, négociants</p>
            <p class="usecase-description">
              Bons de commande, factures fournisseurs, retenues à la source TEJ, réconciliation bancaire et prévisions de réapprovisionnement.
            </p>
            <ul class="usecase-modules">
              <li><i class="pi pi-shopping-bag"></i> Achats & Fournisseurs</li>
              <li><i class="pi pi-warehouse"></i> Multi-entrepôts</li>
              <li><i class="pi pi-credit-card"></i> Banking</li>
              <li><i class="pi pi-chart-line"></i> Forecasting ABC/XYZ</li>
            </ul>
            <a routerLink="/auth/register" class="usecase-cta">
              Démarrer
              <i class="pi pi-arrow-right"></i>
            </a>
          </article>

          <article class="usecase-card persona-accountant">
            <div class="usecase-emoji" aria-hidden="true">📊</div>
            <h3 class="usecase-title">Comptable indépendant</h3>
            <p class="usecase-subtitle">Cabinets comptables, auditeurs, fiscalistes</p>
            <p class="usecase-description">
              Multi-tenant pour gérer plusieurs clients, exports FEC, lettrage, audit chain et journaux comptables conformes.
            </p>
            <ul class="usecase-modules">
              <li><i class="pi pi-calculator"></i> Comptabilité complète</li>
              <li><i class="pi pi-id-card"></i> Multi-tenant</li>
              <li><i class="pi pi-lock"></i> Audit chain</li>
              <li><i class="pi pi-download"></i> Export FEC</li>
            </ul>
            <a routerLink="/auth/register" class="usecase-cta">
              Démarrer
              <i class="pi pi-arrow-right"></i>
            </a>
          </article>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .usecases-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: white;
      position: relative;
    }

    .section-container {
      max-width: 1280px;
      margin: 0 auto;
    }

    .section-header {
      text-align: center;
      margin-bottom: var(--spacing-12);
      max-width: 720px;
      margin-left: auto;
      margin-right: auto;
    }

    .eyebrow {
      display: inline-block;
      padding: var(--spacing-2) var(--spacing-4);
      background: var(--color-primary-50);
      color: var(--color-primary-700);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);
    }

    .section-title {
      font-size: clamp(1.875rem, 4vw, 2.5rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-4) 0;
    }

    .section-description {
      font-size: var(--font-size-lg);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .usecases-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: var(--spacing-5);
    }

    .usecase-card {
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
      transition: all var(--transition-normal);
      position: relative;
      overflow: hidden;

      &::before {
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        height: 4px;
        background: linear-gradient(90deg, var(--color-primary-400), var(--color-primary-600));
        transform: scaleX(0);
        transform-origin: left;
        transition: transform var(--transition-normal);
      }

      &:hover {
        border-color: var(--color-primary-300);
        box-shadow: 0 16px 40px rgba(37, 99, 235, 0.12);
        transform: translateY(-6px);

        &::before {
          transform: scaleX(1);
        }

        .usecase-emoji {
          transform: scale(1.15) rotate(-5deg);
        }

        .usecase-cta {
          background: var(--color-primary-600);
          color: white;
          border-color: var(--color-primary-600);

          i {
            transform: translateX(4px);
          }
        }
      }
    }

    .persona-retail::before {
      background: linear-gradient(90deg, #f59e0b, #d97706);
    }

    .persona-distrib::before {
      background: linear-gradient(90deg, #8b5cf6, #6d28d9);
    }

    .persona-accountant::before {
      background: linear-gradient(90deg, #ec4899, #be185d);
    }

    .usecase-emoji {
      font-size: 3rem;
      line-height: 1;
      transition: transform var(--transition-normal);
    }

    .usecase-title {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      margin: 0;
    }

    .usecase-subtitle {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
      margin: 0;
      font-style: italic;
    }

    .usecase-description {
      font-size: var(--font-size-sm);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0 0 var(--spacing-2) 0;
      flex: 1;
    }

    .usecase-modules {
      list-style: none;
      padding: 0;
      margin: 0 0 var(--spacing-4) 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      li {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: var(--color-neutral-700);

        i {
          color: var(--color-primary-600);
          font-size: 0.875rem;
        }
      }
    }

    .usecase-cta {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3) var(--spacing-4);
      background: white;
      color: var(--color-primary-700);
      border: 1px solid var(--color-primary-300);
      border-radius: var(--radius-lg);
      text-decoration: none;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      transition: all var(--transition-normal);

      i {
        transition: transform var(--transition-normal);
      }
    }

    @media (max-width: 1024px) {
      .usecases-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    @media (max-width: 768px) {
      .usecases-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .usecases-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class UseCasesSectionComponent {}
