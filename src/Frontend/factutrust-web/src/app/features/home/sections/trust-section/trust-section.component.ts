import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-trust-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section id="confiance" class="trust-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">Une plateforme de confiance pour votre entreprise</h2>
          <p class="section-description">
            InstaFact est conçu pour les entreprises tunisiennes qui recherchent la simplicité, la sécurité et la conformité.
            Nous mettons votre confiance au cœur de tout ce que nous faisons.
          </p>
        </div>

        <div class="trust-grid">
          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-building"></i>
            </div>
            <h3 class="trust-title">Conçu pour les entreprises tunisiennes</h3>
            <p class="trust-description">
              Développé spécifiquement pour répondre aux besoins et à la réglementation tunisienne :
              conformité TEJ, calendrier commercial local, banques tunisiennes intégrées.
            </p>
          </div>

          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-shield-check"></i>
            </div>
            <h3 class="trust-title">Sécurité de niveau entreprise</h3>
            <p class="trust-description">
              Infrastructure sécurisée, isolation des données par entreprise, chiffrement de bout en bout. 
              Vos données sont protégées selon les standards les plus stricts.
            </p>
          </div>

          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-verified"></i>
            </div>
            <h3 class="trust-title">Conformité garantie</h3>
            <p class="trust-description">
              Toutes vos factures respectent automatiquement la réglementation tunisienne. 
              Signature électronique conforme, archivage légal, traçabilité complète.
            </p>
          </div>

          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-comments"></i>
            </div>
            <h3 class="trust-title">Support réactif</h3>
            <p class="trust-description">
              Une équipe à votre écoute pour répondre à vos questions et vous accompagner. 
              Documentation complète, guides pratiques, assistance personnalisée.
            </p>
          </div>

          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-sync"></i>
            </div>
            <h3 class="trust-title">Évolutif et fiable</h3>
            <p class="trust-description">
              Une plateforme qui grandit avec votre entreprise. 
              Mises à jour régulières, nouvelles fonctionnalités, performance optimale.
            </p>
          </div>

          <div class="trust-card">
            <div class="trust-icon">
              <i class="pi pi-eye"></i>
            </div>
            <h3 class="trust-title">Transparence totale</h3>
            <p class="trust-description">
              Tarifs clairs, pas de frais cachés, pas d'engagement. 
              Vous gardez le contrôle sur vos données et votre abonnement.
            </p>
          </div>
        </div>

        <div class="trust-promise">
          <div class="promise-icon" aria-hidden="true">
            <i class="pi pi-verified"></i>
          </div>
          <div class="promise-text">
            <strong>Notre engagement</strong>
            <p>
              InstaFact est conçu pour grandir avec vous : conformité automatique, données portables à tout moment,
              tarifs sans frais cachés et un support qui répond. <strong>Vous gardez toujours le contrôle.</strong>
            </p>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .trust-section {
      padding: var(--spacing-24) var(--spacing-6);
      background: linear-gradient(180deg, rgba(255, 255, 255, 0.85) 0%, rgba(249, 250, 251, 0.85) 100%);
      position: relative;
      backdrop-filter: blur(0.5px);
    }

    .section-container {
      max-width: 1280px;
      margin: 0 auto;
    }

    .section-header {
      text-align: center;
      margin-bottom: var(--spacing-16);
      max-width: 720px;
      margin-left: auto;
      margin-right: auto;
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

    .trust-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: var(--spacing-6);
      margin-bottom: var(--spacing-16);
    }

    .trust-card {
      background: white;
      border-radius: var(--radius-xl);
      padding: var(--spacing-8);
      border: 1px solid var(--color-neutral-200);
      box-shadow: 
        0 2px 8px rgba(0, 0, 0, 0.04),
        0 0 0 1px var(--color-neutral-100);
      transition: all var(--transition-normal);
      text-align: center;
      position: relative;
      overflow: hidden;

      &::before {
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        height: 3px;
        background: linear-gradient(90deg, var(--color-primary-500), var(--color-primary-600));
        transform: scaleX(0);
        transform-origin: left;
        transition: transform var(--transition-normal);
      }

      &:hover {
        box-shadow: 
          0 12px 32px rgba(37, 99, 235, 0.12),
          0 0 0 1px var(--color-primary-200);
        transform: translateY(-6px);
        border-color: var(--color-primary-300);

        &::before {
          transform: scaleX(1);
        }

        .trust-icon {
          transform: scale(1.1);
          background: linear-gradient(135deg, var(--color-primary-200), var(--color-primary-300));
        }
      }
    }

    .trust-icon {
      width: 72px;
      height: 72px;
      background: linear-gradient(135deg, var(--color-primary-100), var(--color-primary-200));
      border-radius: var(--radius-xl);
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto var(--spacing-5);
      transition: all var(--transition-normal);
      box-shadow: 0 4px 12px rgba(37, 99, 235, 0.15);

      i {
        font-size: 2.25rem;
        color: var(--color-primary-600);
        transition: transform var(--transition-normal);
      }
    }

    .trust-card:hover .trust-icon i {
      transform: scale(1.1) rotate(5deg);
    }

    .trust-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .trust-description {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .trust-promise {
      display: flex;
      align-items: center;
      gap: var(--spacing-6);
      background: linear-gradient(135deg, var(--color-primary-50) 0%, white 100%);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-8) var(--spacing-10);
      border: 1px solid var(--color-primary-200);
      box-shadow: var(--shadow-md);
      max-width: 880px;
      margin: 0 auto;
    }

    .promise-icon {
      flex-shrink: 0;
      width: 72px;
      height: 72px;
      border-radius: var(--radius-full);
      background: linear-gradient(135deg, var(--color-primary-500), var(--color-primary-700));
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 8px 16px rgba(37, 99, 235, 0.25);

      i {
        font-size: 2rem;
        color: white;
      }
    }

    .promise-text {
      flex: 1;

      strong {
        display: block;
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: var(--color-primary-700);
        text-transform: uppercase;
        letter-spacing: 0.06em;
        margin-bottom: var(--spacing-2);
      }

      p {
        font-size: var(--font-size-base);
        line-height: var(--line-height-relaxed);
        color: var(--color-neutral-700);
        margin: 0;
      }
    }

    @media (max-width: 1024px) {
      .trust-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    @media (max-width: 768px) {
      .trust-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .trust-grid {
        grid-template-columns: 1fr;
      }

      .trust-promise {
        flex-direction: column;
        text-align: center;
        padding: var(--spacing-8) var(--spacing-4);
      }
    }
  `]
})
export class TrustSectionComponent {}
