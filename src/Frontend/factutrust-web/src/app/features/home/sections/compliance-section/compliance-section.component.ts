import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-compliance-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="conformite" class="compliance-section" aria-labelledby="compliance-title">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">
            <i class="pi pi-verified" aria-hidden="true"></i>
            CONFORMITÉ RÉGLEMENTAIRE
          </span>
          <h2 id="compliance-title" class="section-title">
            Conformité réglementaire <span class="gradient-text">garantie</span>
          </h2>
          <p class="section-description">
            InstaFact intègre nativement les standards fiscaux, comptables et réglementaires.
            Vous restez en règle, sans avoir à y penser.
          </p>
        </div>

        <div class="compliance-grid">
          <article class="compliance-card flag-tn">
            <div class="card-header">
              <span class="card-flag" aria-hidden="true">🇹🇳</span>
              <span class="card-label">TUNISIE</span>
            </div>
            <h3 class="card-title">TEJ — Retenues à la source</h3>
            <p class="card-description">
              Calcul automatique des retenues à la source par nature de paiement, génération des certificats RS,
              déclarations XML conformes au format TEJ. <strong>Validé par les autorités fiscales tunisiennes.</strong>
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Configuration TEJ par fournisseur</li>
              <li><i class="pi pi-check"></i> Certificats RS automatiques</li>
              <li><i class="pi pi-check"></i> Export XML conforme</li>
            </ul>
          </article>

          <article class="compliance-card flag-fr">
            <div class="card-header">
              <span class="card-flag" aria-hidden="true">🇫🇷</span>
              <span class="card-label">FRANCE</span>
            </div>
            <h3 class="card-title">FEC — Fichier des Écritures Comptables</h3>
            <p class="card-description">
              Export du Fichier des Écritures Comptables au format réglementaire français, prêt à transmettre à votre expert-comptable
              ou à l'administration en cas de contrôle.
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Format réglementaire respecté</li>
              <li><i class="pi pi-check"></i> Export annuel ou par exercice</li>
              <li><i class="pi pi-check"></i> Compatible expert-comptable</li>
            </ul>
          </article>

          <article class="compliance-card">
            <div class="card-header">
              <div class="card-icon"><i class="pi pi-pencil"></i></div>
              <span class="card-label">SIGNATURE</span>
            </div>
            <h3 class="card-title">Signature électronique légale</h3>
            <p class="card-description">
              Signature de vos factures conforme aux standards eIDAS. Horodatage certifié, intégrité garantie.
              <strong>Vos factures électroniques ont la même valeur légale que les factures papier signées.</strong>
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Compatible eIDAS</li>
              <li><i class="pi pi-check"></i> Horodatage certifié</li>
              <li><i class="pi pi-check"></i> Validité juridique garantie</li>
            </ul>
          </article>

          <article class="compliance-card">
            <div class="card-header">
              <div class="card-icon"><i class="pi pi-database"></i></div>
              <span class="card-label">ARCHIVAGE</span>
            </div>
            <h3 class="card-title">Archivage légal 10 ans</h3>
            <p class="card-description">
              Vos factures et documents comptables sont automatiquement archivés et conservés
              pendant la durée légale (10 ans). Hash chain immuable pour garantir l'intégrité.
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Conservation 10 ans automatique</li>
              <li><i class="pi pi-check"></i> Hash chain immuable</li>
              <li><i class="pi pi-check"></i> Recherche instantanée</li>
            </ul>
          </article>

          <article class="compliance-card">
            <div class="card-header">
              <div class="card-icon"><i class="pi pi-shield"></i></div>
              <span class="card-label">RGPD</span>
            </div>
            <h3 class="card-title">RGPD ready</h3>
            <p class="card-description">
              Gestion du consentement utilisateur, droit à l'oubli, droit à la portabilité.
              Vous pouvez exporter ou supprimer toutes les données d'un client en quelques clics.
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Export complet des données</li>
              <li><i class="pi pi-check"></i> Suppression sur demande</li>
              <li><i class="pi pi-check"></i> Journal des consentements</li>
            </ul>
          </article>

          <article class="compliance-card highlight">
            <div class="card-header">
              <div class="card-icon"><i class="pi pi-lock"></i></div>
              <span class="card-label">AUDIT</span>
            </div>
            <h3 class="card-title">Audit chain — Piste d'audit infalsifiable</h3>
            <p class="card-description">
              Chaque modification est tracée et chaînée par hash cryptographique.
              <strong>Impossible de modifier l'historique sans laisser de trace.</strong> Idéal pour les contrôles fiscaux et les audits internes.
            </p>
            <ul class="card-features">
              <li><i class="pi pi-check"></i> Hash chain cryptographique</li>
              <li><i class="pi pi-check"></i> Traçabilité complète des actions</li>
              <li><i class="pi pi-check"></i> Auditeurs validés</li>
            </ul>
          </article>
        </div>

        <div class="compliance-footer">
          <p>
            <i class="pi pi-info-circle" aria-hidden="true"></i>
            InstaFact est conçu pour évoluer avec la réglementation. Les mises à jour de conformité sont incluses dans tous les plans.
          </p>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .compliance-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: linear-gradient(180deg, white 0%, #f8fafc 100%);
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
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      background: var(--color-success-50);
      color: var(--color-success-700);
      border: 1px solid var(--color-success-200);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);

      i {
        font-size: 0.875rem;
        color: var(--color-success-600);
      }
    }

    .section-title {
      font-size: clamp(1.875rem, 4vw, 2.5rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-4) 0;
    }

    .gradient-text {
      background: linear-gradient(135deg, var(--color-success-600) 0%, var(--color-success-700) 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .section-description {
      font-size: var(--font-size-lg);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .compliance-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: var(--spacing-5);
      margin-bottom: var(--spacing-10);
    }

    .compliance-card {
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      padding: var(--spacing-6);
      transition: all var(--transition-normal);
      position: relative;
      overflow: hidden;

      &::before {
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        height: 3px;
        background: linear-gradient(90deg, var(--color-primary-500), var(--color-success-500));
        transform: scaleX(0);
        transform-origin: left;
        transition: transform var(--transition-normal);
      }

      &:hover {
        border-color: var(--color-primary-300);
        box-shadow: 0 12px 28px rgba(37, 99, 235, 0.1);
        transform: translateY(-4px);

        &::before {
          transform: scaleX(1);
        }
      }

      &.highlight {
        background: linear-gradient(135deg, var(--color-primary-50) 0%, white 100%);
        border-color: var(--color-primary-300);

        &::before {
          transform: scaleX(1);
        }
      }
    }

    .card-header {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      margin-bottom: var(--spacing-4);
    }

    .card-flag {
      font-size: 2rem;
      line-height: 1;
    }

    .card-icon {
      width: 44px;
      height: 44px;
      border-radius: var(--radius-md);
      background: linear-gradient(135deg, var(--color-primary-100), var(--color-primary-200));
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--color-primary-700);

      i {
        font-size: 1.25rem;
      }
    }

    .card-label {
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-500);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }

    .card-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .card-description {
      font-size: var(--font-size-sm);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0 0 var(--spacing-4) 0;
    }

    .card-features {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);

      li {
        display: flex;
        align-items: flex-start;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: var(--color-neutral-700);

        i {
          color: var(--color-success-600);
          font-size: 0.875rem;
          margin-top: 3px;
          flex-shrink: 0;
        }
      }
    }

    .compliance-footer {
      text-align: center;
      padding: var(--spacing-5) var(--spacing-6);
      background: var(--color-primary-50);
      border: 1px solid var(--color-primary-200);
      border-radius: var(--radius-xl);
      max-width: 800px;
      margin: 0 auto;

      p {
        margin: 0;
        font-size: var(--font-size-sm);
        color: var(--color-primary-800);
        display: inline-flex;
        align-items: center;
        gap: var(--spacing-2);

        i {
          color: var(--color-primary-600);
          font-size: 1rem;
        }
      }
    }

    @media (max-width: 1024px) {
      .compliance-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    @media (max-width: 768px) {
      .compliance-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .compliance-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class ComplianceSectionComponent {}
