import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-problem-solution-section',
  standalone: true,
  imports: [CommonModule, RouterModule, CardModule],
  template: `
    <section class="problem-solution-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">
            Vous perdez du temps et de l'argent avec la facturation ?
            <span class="section-subtitle">InstaFact transforme cette corvée en 2 minutes chrono</span>
          </h2>
        </div>

        <div class="problem-solution-grid">
          <div class="problem-column">
            <h3 class="column-title">
              <i class="pi pi-times-circle"></i>
              Les problèmes courants
            </h3>
            <div class="problem-list">
              <div class="problem-item">
                <i class="pi pi-clock"></i>
                <div class="problem-content">
                  <h4>Des heures perdues chaque semaine</h4>
                  <p>Créer une facture manuellement prend 15-30 minutes. Multipliez par le nombre de factures... C'est votre temps précieux qui part.</p>
                </div>
              </div>
              <div class="problem-item">
                <i class="pi pi-exclamation-triangle"></i>
                <div class="problem-content">
                  <h4>Erreurs qui coûtent cher</h4>
                  <p>Une erreur de calcul, un oubli de TVA, une numérotation incorrecte... Les contrôles fiscaux peuvent vous coûter très cher.</p>
                </div>
              </div>
              <div class="problem-item">
                <i class="pi pi-file"></i>
                <div class="problem-content">
                  <h4>Gestion qui devient un cauchemar</h4>
                  <p>Suivre qui a payé, qui doit payer, où sont les factures... Sans système, c'est le chaos et les factures perdues.</p>
                </div>
              </div>
              <div class="problem-item">
                <i class="pi pi-shield"></i>
                <div class="problem-content">
                  <h4>La peur de mal faire</h4>
                  <p>Respecter la réglementation tunisienne sans être comptable ? C'est stressant et risqué. Une erreur = des problèmes avec le fisc.</p>
                </div>
              </div>
            </div>
          </div>

          <div class="solution-column">
            <h3 class="column-title">
              <i class="pi pi-check-circle"></i>
              La solution InstaFact
            </h3>
            <div class="solution-list">
              <div class="solution-item">
                <i class="pi pi-bolt"></i>
                <div class="solution-content">
                  <h4>2 minutes au lieu de 30</h4>
                  <p>Créez une facture professionnelle en 2 minutes chrono. Vous gagnez 28 minutes par facture. Multipliez par 10 factures = <strong>4h40 gagnées par mois</strong>.</p>
                </div>
              </div>
              <div class="solution-item">
                <i class="pi pi-verified"></i>
                <div class="solution-content">
                  <h4>Zéro erreur, zéro stress</h4>
                  <p>Les calculs sont automatiques, la TVA est correcte, la numérotation est conforme. <strong>Vous ne pouvez pas vous tromper.</strong> Dormez tranquille.</p>
                </div>
              </div>
              <div class="solution-item">
                <i class="pi pi-th-large"></i>
                <div class="solution-content">
                  <h4>Simple comme un email</h4>
                  <p>Interface intuitive, accessible à tous. <strong>Aucune formation nécessaire.</strong> Si vous savez envoyer un email, vous savez utiliser InstaFact.</p>
                </div>
              </div>
              <div class="solution-item">
                <i class="pi pi-lock"></i>
                <div class="solution-content">
                  <h4>Conformité automatique garantie</h4>
                  <p>Signature électronique légale, archivage sécurisé 10 ans, conformité fiscale vérifiée automatiquement. <strong>Vous êtes protégé.</strong></p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="solution-cta">
          <p class="cta-text">Prêt à gagner 4 heures par mois et à dormir tranquille ?</p>
          <a routerLink="/auth/register" class="cta-button">
            Commencer gratuitement — 10 factures offertes
            <i class="pi pi-arrow-right"></i>
          </a>
          <p class="cta-note">Aucune carte bancaire requise • Configuration en 5 minutes</p>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .problem-solution-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: rgba(255, 255, 255, 0.85);
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
    }

    .section-title {
      font-size: clamp(1.875rem, 4vw, 2.5rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-4) 0;
    }

    .section-subtitle {
      display: block;
      font-size: clamp(1.125rem, 2vw, 1.5rem);
      font-weight: var(--font-weight-normal);
      color: var(--color-neutral-600);
      margin-top: var(--spacing-2);
    }

    .problem-solution-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-12);
      margin-bottom: var(--spacing-16);
    }

    .column-title {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin-bottom: var(--spacing-6);
      padding-bottom: var(--spacing-4);
      border-bottom: 2px solid var(--color-neutral-200);

      i {
        font-size: 1.5rem;
      }
    }

    .problem-column .column-title i {
      color: var(--color-error-600);
    }

    .solution-column .column-title i {
      color: var(--color-success-600);
    }

    .problem-list,
    .solution-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-6);
    }

    .problem-item,
    .solution-item {
      display: flex;
      gap: var(--spacing-4);
      padding: var(--spacing-5);
      border-radius: var(--radius-xl);
      transition: all var(--transition-normal);
    }

    .problem-item {
      background: var(--color-error-50);
      border: 1px solid var(--color-error-100);

      i {
        color: var(--color-error-600);
        font-size: 1.5rem;
        flex-shrink: 0;
        margin-top: var(--spacing-1);
      }

      &:hover {
        background: var(--color-error-100);
        transform: translateX(-4px);
      }
    }

    .solution-item {
      background: var(--color-success-50);
      border: 1px solid var(--color-success-100);

      i {
        color: var(--color-success-600);
        font-size: 1.5rem;
        flex-shrink: 0;
        margin-top: var(--spacing-1);
      }

      &:hover {
        background: var(--color-success-100);
        transform: translateX(4px);
      }
    }

    .problem-content,
    .solution-content {
      flex: 1;

      h4 {
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-900);
        margin: 0 0 var(--spacing-2) 0;
      }

      p {
        font-size: var(--font-size-base);
        line-height: var(--line-height-relaxed);
        color: var(--color-neutral-700);
        margin: 0;
      }
    }

    .solution-cta {
      text-align: center;
      padding: var(--spacing-12);
      background: linear-gradient(135deg, var(--color-primary-50), var(--color-primary-100));
      border-radius: var(--radius-2xl);
      border: 1px solid var(--color-primary-200);
    }

    .cta-text {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-6) 0;
    }

    .cta-note {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);
      margin: var(--spacing-4) 0 0 0;
    }

    .cta-button {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-3);
      background: var(--color-primary-600);
      color: white;
      text-decoration: none;
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-base);
      padding: var(--spacing-4) var(--spacing-8);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-md);
      transition: all var(--transition-normal);

      &:hover {
        background: var(--color-primary-700);
        transform: translateY(-2px);
        box-shadow: var(--shadow-lg);

        i {
          transform: translateX(4px);
        }
      }

      i {
        transition: transform var(--transition-normal);
      }
    }

    @media (max-width: 1024px) {
      .problem-solution-grid {
        grid-template-columns: 1fr;
        gap: var(--spacing-10);
      }
    }

    @media (max-width: 768px) {
      .problem-solution-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .problem-item,
      .solution-item {
        flex-direction: column;
        text-align: center;
      }
    }
  `]
})
export class ProblemSolutionSectionComponent {}
