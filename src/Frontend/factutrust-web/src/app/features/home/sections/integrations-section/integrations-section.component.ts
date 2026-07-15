import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-integrations-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="integrations" class="integrations-section" aria-labelledby="integrations-title">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">ÉCOSYSTÈME</span>
          <h2 id="integrations-title" class="section-title">Connecté à votre écosystème</h2>
          <p class="section-description">
            InstaFact s'intègre nativement avec les outils que vous utilisez déjà. IA, banques, messageries — tout est prêt à l'emploi.
          </p>
        </div>

        <div class="integrations-grid">
          <div class="integration-card category-ai">
            <span class="category-label">INTELLIGENCE ARTIFICIELLE</span>
            <div class="integration-items">
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">🦙</div>
                <div class="item-text">
                  <strong>Modèle local</strong>
                  <span>Self-hosted, données locales</span>
                </div>
              </div>
            </div>
          </div>

          <div class="integration-card category-comm">
            <span class="category-label">COMMUNICATION CLIENT</span>
            <div class="integration-items">
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">💬</div>
                <div class="item-text">
                  <strong>WhatsApp Business</strong>
                  <span>Envoi factures et relances</span>
                </div>
              </div>
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">✈️</div>
                <div class="item-text">
                  <strong>Telegram</strong>
                  <span>Notifications temps réel</span>
                </div>
              </div>
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">📧</div>
                <div class="item-text">
                  <strong>Email SMTP</strong>
                  <span>Email transactionnel standard</span>
                </div>
              </div>
            </div>
          </div>

          <div class="integration-card category-bank">
            <span class="category-label">BANQUES TUNISIENNES</span>
            <div class="integration-items">
              <div class="integration-item">
                <div class="item-icon bank-icon" aria-hidden="true">🏦</div>
                <div class="item-text">
                  <strong>10+ banques intégrées</strong>
                  <span>BIAT, STB, Attijari, BH, Amen, BT, UIB…</span>
                </div>
              </div>
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">💳</div>
                <div class="item-text">
                  <strong>Multi-devises</strong>
                  <span>TND, EUR, USD et plus</span>
                </div>
              </div>
            </div>
          </div>

          <div class="integration-card category-export">
            <span class="category-label">IMPORT / EXPORT</span>
            <div class="integration-items">
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">📊</div>
                <div class="item-text">
                  <strong>Excel / CSV</strong>
                  <span>Import / export bidirectionnel</span>
                </div>
              </div>
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">📄</div>
                <div class="item-text">
                  <strong>PDF / XML</strong>
                  <span>Formats standards e-invoicing</span>
                </div>
              </div>
              <div class="integration-item">
                <div class="item-icon" aria-hidden="true">🔌</div>
                <div class="item-text">
                  <strong>API REST</strong>
                  <span>150+ endpoints documentés</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="integrations-footer">
          <i class="pi pi-info-circle" aria-hidden="true"></i>
          <span>Vous avez besoin d'une intégration personnalisée ? <strong>Le plan Entreprise</strong> permet des connecteurs sur mesure.</span>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .integrations-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: var(--color-neutral-50);
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

    .integrations-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-5);
      margin-bottom: var(--spacing-8);
    }

    .integration-card {
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      transition: all var(--transition-normal);

      &:hover {
        border-color: var(--color-primary-300);
        box-shadow: 0 8px 20px rgba(37, 99, 235, 0.08);
      }
    }

    .category-label {
      display: inline-block;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-primary-700);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);
      padding: 4px 10px;
      background: var(--color-primary-50);
      border-radius: var(--radius-md);
    }

    .integration-items {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4);
    }

    .integration-item {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-3);
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);
      transition: all var(--transition-normal);

      &:hover {
        background: var(--color-primary-50);
        transform: translateX(4px);
      }
    }

    .item-icon {
      width: 48px;
      height: 48px;
      border-radius: var(--radius-md);
      background: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.75rem;
      line-height: 1;
      flex-shrink: 0;
      box-shadow: var(--shadow-sm);
    }

    .item-text {
      flex: 1;
      min-width: 0;

      strong {
        display: block;
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-900);
        margin-bottom: 2px;
      }

      span {
        font-size: var(--font-size-sm);
        color: var(--color-neutral-600);
        line-height: 1.4;
      }
    }

    .integrations-footer {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-3);
      padding: var(--spacing-5) var(--spacing-6);
      background: white;
      border: 1px solid var(--color-primary-200);
      border-radius: var(--radius-xl);
      max-width: 800px;
      margin: 0 auto;
      text-align: center;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-700);

      i {
        color: var(--color-primary-600);
        font-size: 1.25rem;
        flex-shrink: 0;
      }
    }

    @media (max-width: 1024px) {
      .integrations-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (max-width: 768px) {
      .integrations-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .integrations-footer {
        flex-direction: column;
        text-align: center;
      }
    }
  `]
})
export class IntegrationsSectionComponent {}
