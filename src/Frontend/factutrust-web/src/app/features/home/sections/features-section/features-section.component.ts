import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-features-section',
  standalone: true,
  imports: [CommonModule, CardModule],
  template: `
    <section id="fonctionnalites" class="features-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">Ce que vous obtenez avec InstaFact</h2>
          <p class="section-description">
            <strong>9 fonctionnalités essentielles</strong>, présentées simplement et sans jargon. Tout ce qu'il faut pour facturer, encaisser, suivre et grandir — au sein d'une seule plateforme.
          </p>
        </div>

        <div class="features-grid">
          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-file-edit"></i>
            </div>
            <h3 class="feature-title">Création de factures en 2 minutes</h3>
            <p class="feature-description">
              Créez des factures professionnelles en quelques clics. <strong>La TVA se calcule toute seule</strong>, la numérotation est automatique, les erreurs sont impossibles.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> <strong>2 minutes</strong> pour créer une facture</li>
              <li><i class="pi pi-check"></i> TVA calculée <strong>automatiquement</strong></li>
              <li><i class="pi pi-check"></i> Numérotation <strong>conforme</strong> automatique</li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-pencil"></i>
            </div>
            <h3 class="feature-title">Signature électronique légale</h3>
            <p class="feature-description">
              Signez vos factures en un clic. <strong>Conforme à la réglementation tunisienne.</strong> Vos factures sont valides légalement, sans papier, sans stress.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> Signature <strong>légale</strong> en un clic</li>
              <li><i class="pi pi-check"></i> Horodatage <strong>certifié</strong></li>
              <li><i class="pi pi-check"></i> <strong>Zéro papier</strong>, zéro archivage manuel</li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-database"></i>
            </div>
            <h3 class="feature-title">Archivage automatique sécurisé</h3>
            <p class="feature-description">
              Vos factures sont archivées <strong>automatiquement</strong> et <strong>sécurisées</strong> pendant 10 ans (obligation légale). Plus besoin de classeurs, plus de risque de perte.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> Archivage <strong>automatique</strong></li>
              <li><i class="pi pi-check"></i> Recherche <strong>instantanée</strong></li>
              <li><i class="pi pi-check"></i> Conservation <strong>légale 10 ans</strong></li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-download"></i>
            </div>
            <h3 class="feature-title">Export PDF / XML en un clic</h3>
            <p class="feature-description">
              Envoyez une facture à votre client (PDF) ou à votre comptable (XML) <strong>en un clic</strong>. Formats standards, compatibilité garantie.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> PDF <strong>professionnel</strong> pour vos clients</li>
              <li><i class="pi pi-check"></i> XML <strong>comptable</strong> standard</li>
              <li><i class="pi pi-check"></i> Export <strong>instantané</strong></li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-wallet"></i>
            </div>
            <h3 class="feature-title">Suivi des paiements simplifié</h3>
            <p class="feature-description">
              Voyez d'un coup d'œil qui a payé, qui doit payer, qui est en retard. <strong>Plus besoin de tableaux Excel</strong> ou de notes éparpillées.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> Vue d'ensemble <strong>instantanée</strong></li>
              <li><i class="pi pi-check"></i> Alertes <strong>automatiques</strong> de retard</li>
              <li><i class="pi pi-check"></i> <strong>Zéro oubli</strong> de relance</li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-users"></i>
            </div>
            <h3 class="feature-title">Gestion des clients centralisée</h3>
            <p class="feature-description">
              Toutes les infos de vos clients au même endroit. <strong>Créez une facture en 2 clics</strong> : sélectionnez le client, c'est tout. Plus besoin de ressaisir les données.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> Fiches clients <strong>complètes</strong></li>
              <li><i class="pi pi-check"></i> Historique <strong>automatique</strong> des factures</li>
              <li><i class="pi pi-check"></i> Facturation <strong>ultra-rapide</strong></li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-id-card"></i>
            </div>
            <h3 class="feature-title">Multi-utilisateurs et rôles avancés</h3>
            <p class="feature-description">
              <strong>10 rôles prédéfinis</strong> (Admin, Comptable, Commercial, Caissier, Magasinier, Acheteur, Auditeur…) avec permissions granulaires. Chaque utilisateur voit uniquement ce dont il a besoin.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> <strong>10 rôles</strong> prêts à l'emploi</li>
              <li><i class="pi pi-check"></i> Permissions <strong>granulaires</strong> par module</li>
              <li><i class="pi pi-check"></i> Audit complet <strong>des actions</strong></li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-warehouse"></i>
            </div>
            <h3 class="feature-title">Multi-entrepôts et points de vente</h3>
            <p class="feature-description">
              Gérez plusieurs <strong>magasins, dépôts et points de vente</strong> depuis une seule plateforme. Transferts inter-entrepôts, valuation FIFO, alertes de stock bas en temps réel.
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> Stocks <strong>multi-entrepôts</strong></li>
              <li><i class="pi pi-check"></i> <strong>Transferts</strong> inter-magasins</li>
              <li><i class="pi pi-check"></i> Alertes <strong>temps réel</strong></li>
            </ul>
          </div>

          <div class="feature-card">
            <div class="feature-icon">
              <i class="pi pi-send"></i>
            </div>
            <h3 class="feature-title">Notifications multi-canal</h3>
            <p class="feature-description">
              Envoyez factures et relances par <strong>WhatsApp, Telegram ou Email</strong>. Vos clients reçoivent leurs documents là où ils sont actifs. <strong>Plus de retards de paiement.</strong>
            </p>
            <ul class="feature-list">
              <li><i class="pi pi-check"></i> <strong>WhatsApp Business</strong> intégré</li>
              <li><i class="pi pi-check"></i> Bot <strong>Telegram</strong> natif</li>
              <li><i class="pi pi-check"></i> <strong>Email</strong> transactionnel</li>
            </ul>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .features-section {
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

    .features-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: var(--spacing-6);
    }

    .feature-card {
      background: white;
      border-radius: var(--radius-xl);
      padding: var(--spacing-8);
      border: 1px solid var(--color-neutral-200);
      box-shadow: 
        0 2px 8px rgba(0, 0, 0, 0.04),
        0 0 0 1px var(--color-neutral-100);
      transition: all var(--transition-normal);
      display: flex;
      flex-direction: column;
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

        .feature-icon {
          transform: scale(1.1);
          background: linear-gradient(135deg, var(--color-primary-200), var(--color-primary-300));
        }
      }
    }

    .feature-icon {
      width: 72px;
      height: 72px;
      background: linear-gradient(135deg, var(--color-primary-100), var(--color-primary-200));
      border-radius: var(--radius-xl);
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: var(--spacing-5);
      transition: all var(--transition-normal);
      box-shadow: 0 4px 12px rgba(37, 99, 235, 0.15);

      i {
        font-size: 2.25rem;
        color: var(--color-primary-600);
        transition: transform var(--transition-normal);
      }
    }

    .feature-card:hover .feature-icon i {
      transform: scale(1.1) rotate(5deg);
    }

    .feature-title {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .feature-description {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0 0 var(--spacing-4) 0;
      flex: 1;
    }

    .feature-list {
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
        color: var(--color-neutral-700);

        i {
          color: var(--color-success-600);
          font-size: 0.875rem;
        }
      }
    }

    @media (max-width: 1024px) {
      .features-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    @media (max-width: 768px) {
      .features-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .features-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class FeaturesSectionComponent {}
