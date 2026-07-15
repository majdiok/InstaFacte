import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-security-section',
  standalone: true,
  imports: [CommonModule, CardModule],
  template: `
    <section id="securite-detail" class="security-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">Vos données sont protégées, votre conformité est garantie</h2>
          <p class="section-description">
            <strong>Votre tranquillité d'esprit est notre priorité.</strong> Vos factures sont protégées, signées légalement et archivées conformément à la réglementation tunisienne. <strong>Vous pouvez dormir tranquille.</strong>
          </p>
        </div>

        <div class="security-grid">
          <div class="security-card">
            <div class="security-icon">
              <i class="pi pi-shield"></i>
            </div>
            <h3 class="security-title">Protection de niveau bancaire</h3>
            <p class="security-description">
              Notre infrastructure est protégée selon les standards les plus stricts. <strong>Vos données sont chiffrées</strong> comme dans une banque. Authentification renforcée, surveillance 24/7.
            </p>
            <ul class="security-features">
              <li><i class="pi pi-check"></i> Chiffrement <strong>HTTPS/TLS</strong> (comme les banques)</li>
              <li><i class="pi pi-check"></i> Protection contre <strong>toutes les attaques</strong> courantes</li>
              <li><i class="pi pi-check"></i> Authentification <strong>renforcée</strong> (2FA disponible)</li>
            </ul>
          </div>

          <div class="security-card">
            <div class="security-icon">
              <i class="pi pi-lock"></i>
            </div>
            <h3 class="security-title">Vos données sont isolées et protégées</h3>
            <p class="security-description">
              <strong>Chaque entreprise a sa propre base de données séparée.</strong> Vos données ne sont jamais mélangées avec celles d'autres clients. Isolation totale garantie, comme si vous aviez votre propre serveur.
            </p>
            <ul class="security-features">
              <li><i class="pi pi-check"></i> Base de données <strong>100% isolée</strong> par entreprise</li>
              <li><i class="pi pi-check"></i> Chiffrement <strong>au repos</strong> (même en stockage)</li>
              <li><i class="pi pi-check"></i> Sauvegardes <strong>automatiques quotidiennes</strong></li>
            </ul>
          </div>

          <div class="security-card">
            <div class="security-icon">
              <i class="pi pi-file-check"></i>
            </div>
            <h3 class="security-title">Conformité fiscale automatique garantie</h3>
            <p class="security-description">
              <strong>Vous ne pouvez pas créer une facture non conforme.</strong> La plateforme vérifie automatiquement que tout est conforme à la réglementation tunisienne. Numérotation, TVA, champs obligatoires : tout est validé.
            </p>
            <ul class="security-features">
              <li><i class="pi pi-check"></i> Validation <strong>automatique</strong> avant signature</li>
              <li><i class="pi pi-check"></i> Conformité <strong>100% garantie</strong></li>
              <li><i class="pi pi-check"></i> Traçabilité <strong>complète</strong> pour les contrôles</li>
            </ul>
          </div>

          <div class="security-card">
            <div class="security-icon">
              <i class="pi pi-database"></i>
            </div>
            <h3 class="security-title">Archivage légal automatique (10 ans)</h3>
            <p class="security-description">
              <strong>Vos factures sont archivées automatiquement pendant 10 ans</strong> (obligation légale). Plus besoin de classeurs, plus de risque de perte. Intégrité garantie, accès instantané.
            </p>
            <ul class="security-features">
              <li><i class="pi pi-check"></i> Conservation <strong>légale 10 ans</strong> automatique</li>
              <li><i class="pi pi-check"></i> Intégrité <strong>vérifiée</strong> et garantie</li>
              <li><i class="pi pi-check"></i> Accès <strong>instantané</strong> à tout moment</li>
            </ul>
          </div>
        </div>

        <div class="security-badge">
          <div class="badge-content">
            <i class="pi pi-verified"></i>
            <div class="badge-text">
              <strong>Plateforme certifiée et conforme</strong>
              <span>Respect des standards de sécurité les plus stricts et conformité fiscale tunisienne garantie. <strong>Votre tranquillité d'esprit est notre priorité.</strong></span>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .security-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: var(--color-neutral-900);
      color: white;
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
      color: white;
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-4) 0;
    }

    .section-description {
      font-size: var(--font-size-lg);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-300);
      margin: 0;
    }

    .security-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-6);
      margin-bottom: var(--spacing-12);
    }

    .security-card {
      background: var(--color-neutral-800);
      border: 1px solid var(--color-neutral-700);
      border-radius: var(--radius-xl);
      padding: var(--spacing-6);
      transition: all var(--transition-normal);

      &:hover {
        border-color: var(--color-primary-500);
        transform: translateY(-4px);
        box-shadow: 0 12px 24px rgba(0, 0, 0, 0.3);
      }
    }

    .security-icon {
      width: 64px;
      height: 64px;
      background: linear-gradient(135deg, var(--color-primary-600), var(--color-primary-700));
      border-radius: var(--radius-xl);
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: var(--spacing-4);

      i {
        font-size: 2rem;
        color: white;
      }
    }

    .security-title {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: white;
      margin: 0 0 var(--spacing-3) 0;
    }

    .security-description {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-300);
      margin: 0 0 var(--spacing-4) 0;
    }

    .security-features {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      li {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: var(--color-neutral-200);

        i {
          color: var(--color-success-500);
          font-size: 0.875rem;
        }
      }
    }

    .security-badge {
      background: var(--color-neutral-800);
      border: 2px solid var(--color-primary-500);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      text-align: center;
    }

    .badge-content {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-4);
      flex-wrap: wrap;
    }

    .badge-content i {
      font-size: 3rem;
      color: var(--color-primary-400);
    }

    .badge-text {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      text-align: left;

      strong {
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: white;
        display: block;
      }

      span {
        font-size: var(--font-size-sm);
        color: var(--color-neutral-400);
      }
    }

    @media (max-width: 1024px) {
      .security-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (max-width: 768px) {
      .security-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .badge-content {
        flex-direction: column;
        text-align: center;
      }

      .badge-text {
        text-align: center;
      }
    }
  `]
})
export class SecuritySectionComponent {}
