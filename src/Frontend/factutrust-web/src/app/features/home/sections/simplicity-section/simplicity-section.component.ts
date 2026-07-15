import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-simplicity-section',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <section class="simplicity-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">Créer une facture en 3 étapes simples</h2>
          <p class="section-description">
            <strong>Aucun jargon comptable, aucune formation nécessaire.</strong> Si vous savez envoyer un email, vous savez utiliser InstaFact.
          </p>
        </div>

        <div class="steps-container">
          <div class="step-item">
            <div class="step-number">1</div>
            <div class="step-content">
              <h3 class="step-title">Renseignez les informations</h3>
              <p class="step-description">
                Sélectionnez votre client (ou créez-le en 30 secondes), ajoutez vos produits ou services. <strong>C'est tout.</strong> La TVA et les totaux se calculent automatiquement.
              </p>
              <div class="step-visual">
                <div class="visual-card">
                  <div class="visual-line"></div>
                  <div class="visual-line short"></div>
                  <div class="visual-line"></div>
                </div>
              </div>
            </div>
          </div>

          <div class="step-arrow">
            <i class="pi pi-arrow-down"></i>
          </div>

          <div class="step-item">
            <div class="step-number">2</div>
            <div class="step-content">
              <h3 class="step-title">Vérifiez l'aperçu</h3>
              <p class="step-description">
                Consultez votre facture avant validation. <strong>Tout est déjà calculé et vérifié.</strong> La conformité est garantie automatiquement. Il ne reste qu'à valider.
              </p>
              <div class="step-visual">
                <div class="visual-card">
                  <div class="visual-check">
                    <i class="pi pi-check-circle"></i>
                  </div>
                  <div class="visual-line"></div>
                </div>
              </div>
            </div>
          </div>

          <div class="step-arrow">
            <i class="pi pi-arrow-down"></i>
          </div>

          <div class="step-item">
            <div class="step-number">3</div>
            <div class="step-content">
              <h3 class="step-title">Signez et c'est terminé</h3>
              <p class="step-description">
                Signez électroniquement en un clic. <strong>Votre facture est automatiquement archivée</strong> de manière sécurisée et conforme. Vous pouvez l'envoyer à votre client ou l'exporter.
              </p>
              <div class="step-visual">
                <div class="visual-card">
                  <div class="visual-seal">
                    <i class="pi pi-lock"></i>
                    <span>Signée</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="simplicity-cta">
          <p class="cta-text">C'est aussi simple que ça. <strong>2 minutes pour créer votre première facture.</strong></p>
          <a routerLink="/auth/register" class="cta-button">
            Essayer gratuitement — 10 factures offertes
            <i class="pi pi-arrow-right"></i>
          </a>
          <p class="cta-note">Aucune carte bancaire requise • Configuration en 5 minutes</p>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .simplicity-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: rgba(249, 250, 251, 0.85);
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

    .steps-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-8);
      margin-bottom: var(--spacing-16);
    }

    .step-item {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: var(--spacing-6);
      align-items: start;
      max-width: 800px;
      width: 100%;
    }

    .step-number {
      width: 64px;
      height: 64px;
      background: linear-gradient(135deg, var(--color-primary-600), var(--color-primary-700));
      border-radius: var(--radius-full);
      display: flex;
      align-items: center;
      justify-content: center;
      color: white;
      font-size: var(--font-size-2xl);
      font-weight: var(--font-weight-bold);
      box-shadow: var(--shadow-lg);
      flex-shrink: 0;
    }

    .step-content {
      flex: 1;
    }

    .step-title {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .step-description {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0 0 var(--spacing-4) 0;
    }

    .step-visual {
      margin-top: var(--spacing-4);
    }

    .visual-card {
      background: var(--color-neutral-50);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-lg);
      padding: var(--spacing-4);
    }

    .visual-line {
      height: 8px;
      background: var(--color-neutral-300);
      border-radius: var(--radius-sm);
      margin-bottom: var(--spacing-2);

      &:last-child {
        margin-bottom: 0;
      }

      &.short {
        width: 60%;
      }
    }

    .visual-check {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      background: var(--color-success-100);
      border-radius: var(--radius-full);
      color: var(--color-success-600);
      font-size: 1.5rem;
      margin-bottom: var(--spacing-3);
    }

    .visual-seal {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3) var(--spacing-4);
      background: var(--color-primary-100);
      border-radius: var(--radius-md);
      color: var(--color-primary-700);
      font-weight: var(--font-weight-semibold);
      width: fit-content;
      margin: 0 auto;

      i {
        font-size: 1.25rem;
      }
    }

    .step-arrow {
      display: flex;
      justify-content: center;
      color: var(--color-primary-500);
      font-size: 2rem;
    }

    .simplicity-cta {
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

    @media (max-width: 768px) {
      .simplicity-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .step-item {
        grid-template-columns: 1fr;
        text-align: center;
      }

      .step-number {
        margin: 0 auto var(--spacing-4);
      }

      .step-arrow {
        transform: rotate(90deg);
      }
    }
  `]
})
export class SimplicitySectionComponent {}
