import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';

/**
 * Copy des forfaits : garder aligné avec GET api/subscription/plans (SubscriptionController.GetAvailablePlans)
 * et SubscriptionLimits (InstaFact.Domain) pour limites et flags XmlExport / signature / support.
 */
@Component({
  selector: 'app-pricing-section',
  standalone: true,
  imports: [CommonModule, RouterModule, ButtonModule, CardModule],
  template: `
    <section id="tarifs" class="pricing-section">
      <div class="section-container">
        <div class="section-header">
          <h2 class="section-title">Une suite ERP complète, des tarifs transparents</h2>
          <p class="section-description">
            Facturation conforme TEJ, modules métier intégrés, <strong>assistant IA</strong> et prévisions — testez la plateforme avec des quotas généreux, sans carte bancaire.
          </p>
          <p class="section-modules-cta">
            <a href="#modules" class="modules-anchor">Voir le détail des modules</a>
          </p>
          <p class="section-reassurance">
            <strong>Sans CB. Sans engagement. Annulation en un clic depuis votre espace.</strong>
          </p>
        </div>

        <div class="pricing-grid">
          <div class="pricing-card">
            <div class="pricing-header">
              <h3 class="pricing-name">Gratuit</h3>
              <div class="pricing-badge">Idéal pour tester pendant 1 mois</div>
              <div class="pricing-price">
                <span class="price-amount">0</span>
                <span class="price-currency">TND</span>
                <span class="price-period">/mois</span>
              </div>
              <div class="pricing-highlight">10 factures gratuites par mois</div>
            </div>
            <div class="pricing-body">
              <ul class="pricing-features">
                <li><i class="pi pi-check"></i> <strong>10 factures</strong> / mois</li>
                <li><i class="pi pi-check"></i> <strong>10 devis</strong> / mois</li>
                <li><i class="pi pi-check"></i> Jusqu'à <strong>20 clients</strong></li>
                <li><i class="pi pi-check"></i> Jusqu'à <strong>50 produits</strong> ou services</li>
                <li><i class="pi pi-check"></i> <strong>100 Mo</strong> de stockage documents</li>
                <li><i class="pi pi-check"></i> Jusqu'à <strong>5 utilisateurs</strong></li>
                <li><i class="pi pi-check"></i> Export <strong>PDF</strong> (factures & devis)</li>
                <li><i class="pi pi-check"></i> Découverte des <strong>modules</strong> InstaFact (sous quotas)</li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Export XML / dossiers TEJ avancés
                </li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Signature électronique
                </li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Suivi des paiements avancé
                </li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Archivage sécurisé étendu
                </li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Support prioritaire
                </li>
              </ul>
              <a routerLink="/auth/register" class="pricing-button">
                Commencer gratuitement
              </a>
              <p class="pricing-note"><strong>Aucune carte bancaire requise</strong> • Configuration en 5 minutes</p>
            </div>
          </div>

          <div class="pricing-card">
            <div class="pricing-header">
              <h3 class="pricing-name">Mensuel</h3>
              <div class="pricing-price">
                <span class="price-amount">49</span>
                <span class="price-currency">TND</span>
                <span class="price-period">/mois</span>
              </div>
            </div>
            <div class="pricing-body">
              <ul class="pricing-features">
                <li><i class="pi pi-check"></i> <strong>Factures, devis, clients & produits</strong> illimités</li>
                <li><i class="pi pi-check"></i> <strong>Achats</strong>, <strong>stock</strong> multi-entrepôts, <strong>trésorerie</strong> & banques</li>
                <li><i class="pi pi-check"></i> <strong>Comptabilité</strong>, <strong>CRM</strong>, <strong>fiscal / TEJ</strong></li>
                <li><i class="pi pi-check"></i> <strong>Assistant IA</strong> — chat, OCR, recommandations</li>
                <li><i class="pi pi-check"></i> <strong>Prévisions</strong> intelligentes (forecasting)</li>
                <li><i class="pi pi-check"></i> Signature électronique</li>
                <li><i class="pi pi-check"></i> Export PDF & <strong>XML TEJ</strong></li>
                <li><i class="pi pi-check"></i> Suivi des paiements & rapports avancés</li>
                <li><i class="pi pi-check"></i> Archivage sécurisé — <strong>5 Go</strong> inclus</li>
                <li class="feature-disabled">
                  <span class="icon-cross-3d" aria-hidden="true"></span>
                  Support prioritaire (forfait Annuel)
                </li>
              </ul>
              <a routerLink="/auth/register" class="pricing-button">
                Choisir Mensuel
              </a>
              <p class="pricing-note">Facturé mensuellement • Annulation à tout moment</p>
            </div>
          </div>

          <div class="pricing-card featured">
            <div class="pricing-badge-featured">Le plus populaire</div>
            <div class="pricing-header">
              <h3 class="pricing-name">Annuel</h3>
              <div class="pricing-badge savings">Économisez 20%</div>
              <div class="pricing-price">
                <span class="price-amount">39</span>
                <span class="price-currency">TND</span>
                <span class="price-period">/mois</span>
              </div>
              <div class="pricing-billing">Facturé 468 TND par an</div>
            </div>
            <div class="pricing-body">
              <ul class="pricing-features">
                <li><i class="pi pi-check"></i> <strong>Factures, devis, clients & produits</strong> illimités</li>
                <li><i class="pi pi-check"></i> <strong>Achats</strong>, <strong>stock</strong> multi-entrepôts, <strong>trésorerie</strong> & banques</li>
                <li><i class="pi pi-check"></i> <strong>Comptabilité</strong>, <strong>CRM</strong>, <strong>fiscal / TEJ</strong></li>
                <li><i class="pi pi-check"></i> <strong>Assistant IA</strong> — chat, OCR, recommandations</li>
                <li><i class="pi pi-check"></i> <strong>Prévisions</strong> intelligentes (forecasting)</li>
                <li><i class="pi pi-check"></i> Signature électronique</li>
                <li><i class="pi pi-check"></i> Export PDF & <strong>XML TEJ</strong></li>
                <li><i class="pi pi-check"></i> Suivi des paiements & rapports avancés</li>
                <li><i class="pi pi-check"></i> Archivage sécurisé — <strong>20 Go</strong> inclus</li>
                <li><i class="pi pi-check"></i> <strong>Support prioritaire</strong></li>
                <li><i class="pi pi-check"></i> <strong>Économie de 120 TND/an</strong> vs mensuel</li>
              </ul>
              <a routerLink="/auth/register" class="pricing-button primary">
                Choisir Annuel — Économisez 120 TND
              </a>
              <p class="pricing-note">Paiement annuel unique • Annulation possible</p>
            </div>
          </div>
        </div>

        <div class="enterprise-banner">
          <div class="enterprise-content">
            <div class="enterprise-icon" aria-hidden="true">
              <i class="pi pi-building"></i>
            </div>
            <div class="enterprise-text">
              <h3>Plan Entreprise — Sur mesure</h3>
              <p>
                Gouvernance multi-sociétés, formation dédiée, SLA, intégrations sur mesure (API, webhooks) et accompagnement renforcé sur les modules ERP et l'IA.
                Idéal pour les groupes, cabinets comptables et entreprises réglementées.
              </p>
              <ul class="enterprise-features">
                <li><i class="pi pi-check"></i> Tout le plan Annuel inclus</li>
                <li><i class="pi pi-check"></i> Multi-tenant (plusieurs entreprises)</li>
                <li><i class="pi pi-check"></i> Formation et accompagnement</li>
                <li><i class="pi pi-check"></i> SLA garanti et support prioritaire</li>
                <li><i class="pi pi-check"></i> Intégrations personnalisées (API, webhooks)</li>
              </ul>
            </div>
            <a href="#newsletter" class="enterprise-cta">
              Nous contacter
              <i class="pi pi-arrow-right"></i>
            </a>
          </div>
        </div>

        <div class="pricing-faq">
          <h3 class="faq-title">Questions fréquentes sur les tarifs</h3>
          <div class="faq-grid">
            <div class="faq-item">
              <h4>Puis-je changer de plan à tout moment ?</h4>
              <p>Oui, depuis <strong>Paramètres &gt; Abonnement</strong> vous pouvez passer d'un forfait à l'autre. Le nouveau plafond de quotas et les fonctionnalités associées s'appliquent dès la mise à jour confirmée dans l'application.</p>
            </div>
            <div class="faq-item">
              <h4>Que se passe-t-il si je dépasse la limite du plan gratuit ?</h4>
              <p>La création de nouvelles factures ou devis peut être bloquée jusqu'au renouvellement mensuel du quota, ou vous pouvez passer à un forfait payant pour lever les plafonds sur la vente et l'ensemble des modules.</p>
            </div>
            <div class="faq-item">
              <h4>Puis-je annuler mon abonnement ?</h4>
              <p>Oui, vous pouvez annuler à tout moment depuis votre espace. Aucun engagement, aucun frais caché.</p>
            </div>
            <div class="faq-item">
              <h4>Les données sont-elles conservées après annulation ?</h4>
              <p>Oui, vos données restent accessibles pendant 30 jours après annulation. Vous pouvez les exporter à tout moment.</p>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .pricing-section {
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

    .section-modules-cta {
      margin: var(--spacing-4) 0 0 0;
    }

    .modules-anchor {
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-primary-700);
      text-decoration: none;
      border-bottom: 1px solid transparent;
      transition: color 0.2s ease, border-color 0.2s ease;
    }

    .modules-anchor:hover {
      color: var(--color-primary-800);
      border-bottom-color: var(--color-primary-400);
    }

    .modules-anchor:focus-visible {
      outline: 2px solid var(--color-primary-500);
      outline-offset: 2px;
      border-radius: var(--radius-sm);
    }

    .section-reassurance {
      margin: var(--spacing-3) 0 0 0;
      display: inline-block;
      padding: 0.45rem 1rem;
      background: var(--color-success-50);
      color: var(--color-success-700);
      border-radius: var(--radius-full);
      font-size: 0.9rem;
      font-weight: var(--font-weight-semibold);
      border: 1px solid var(--color-success-200);
    }

    .pricing-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: var(--spacing-6);
      margin-bottom: var(--spacing-16);
    }

    .pricing-card {
      background: white;
      border: 2px solid var(--color-neutral-200);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      position: relative;
      transition: all var(--transition-normal);
      display: flex;
      flex-direction: column;

      &:hover {
        border-color: var(--color-primary-300);
        box-shadow: var(--shadow-lg);
        transform: translateY(-4px);
      }

      &.featured {
        border-color: var(--color-primary-600);
        box-shadow: var(--shadow-xl);
        transform: scale(1.05);

        @media (max-width: 1024px) {
          transform: scale(1);
        }
      }
    }

    .pricing-badge-featured {
      position: absolute;
      top: -12px;
      left: 50%;
      transform: translateX(-50%);
      background: var(--color-primary-600);
      color: white;
      padding: var(--spacing-2) var(--spacing-4);
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
    }

    .pricing-header {
      text-align: center;
      padding-bottom: var(--spacing-6);
      border-bottom: 1px solid var(--color-neutral-200);
      margin-bottom: var(--spacing-6);
    }

    .pricing-name {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .pricing-badge {
      display: inline-block;
      padding: var(--spacing-1) var(--spacing-3);
      background: var(--color-success-50);
      color: var(--color-success-700);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      margin-bottom: var(--spacing-3);

      &.savings {
        background: var(--color-primary-50);
        color: var(--color-primary-700);
      }
    }

    .pricing-price {
      display: flex;
      align-items: baseline;
      justify-content: center;
      gap: var(--spacing-1);
      margin-top: var(--spacing-4);
    }

    .price-amount {
      font-size: 3rem;
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      line-height: 1;
    }

    .price-currency {
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-600);
    }

    .price-period {
      font-size: var(--font-size-base);
      color: var(--color-neutral-500);
    }

    .pricing-billing {
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
      margin-top: var(--spacing-2);
    }

    .pricing-highlight {
      font-size: var(--font-size-base);
      color: var(--color-primary-700);
      font-weight: var(--font-weight-semibold);
      margin-top: var(--spacing-3);
      padding: var(--spacing-2) var(--spacing-4);
      background: var(--color-primary-50);
      border-radius: var(--radius-md);
      display: inline-block;
    }

    .pricing-body {
      flex: 1;
      display: flex;
      flex-direction: column;
    }

    .pricing-features {
      list-style: none;
      padding: 0;
      margin: 0 0 var(--spacing-6) 0;
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);

      li {
        display: flex;
        align-items: flex-start;
        gap: var(--spacing-2);
        font-size: var(--font-size-base);
        color: var(--color-neutral-700);
        line-height: var(--line-height-relaxed);

        i {
          color: var(--color-success-600);
          font-size: 1rem;
          margin-top: 2px;
          flex-shrink: 0;
        }

        &.feature-disabled {
          opacity: 0.9;
          color: var(--color-neutral-500);
          display: flex;
          align-items: flex-start;
          gap: var(--spacing-2);
        }
      }
    }

    .icon-cross-3d {
      display: inline-block;
      width: 18px;
      height: 18px;
      position: relative;
      flex-shrink: 0;
      margin-top: 2px;
      vertical-align: middle;

      &::before {
        content: '';
        position: absolute;
        top: 50%;
        left: 50%;
        background: #dc2626;
        border-radius: 1px;
        width: 14px;
        height: 2.5px;
        margin-left: -7px;
        margin-top: -1.25px;
        transform: translate(-50%, -50%) rotate(45deg);
        box-shadow: 
          0 1px 3px rgba(220, 38, 38, 0.8),
          0 2px 5px rgba(220, 38, 38, 0.6),
          0 0 2px rgba(0, 0, 0, 0.5),
          inset 0 1px 0 rgba(255, 255, 255, 0.3);
      }

      &::after {
        content: '';
        position: absolute;
        top: 50%;
        left: 50%;
        background: #dc2626;
        border-radius: 1px;
        width: 14px;
        height: 2.5px;
        margin-left: -7px;
        margin-top: -1.25px;
        transform: translate(-50%, -50%) rotate(-45deg);
        box-shadow: 
          0 1px 3px rgba(220, 38, 38, 0.8),
          0 2px 5px rgba(220, 38, 38, 0.6),
          0 0 2px rgba(0, 0, 0, 0.5),
          inset 0 1px 0 rgba(255, 255, 255, 0.3);
      }

      // Ombre globale pour effet 3D
      filter: drop-shadow(0 2px 4px rgba(220, 38, 38, 0.6))
              drop-shadow(0 1px 2px rgba(220, 38, 38, 0.5));
    }

    .pricing-features li.feature-disabled .icon-cross-3d {
      margin-right: var(--spacing-2);
    }

    .pricing-button {
      display: block;
      text-align: center;
      padding: var(--spacing-4) var(--spacing-6);
      border-radius: var(--radius-lg);
      text-decoration: none;
      font-weight: var(--font-weight-semibold);
      transition: all var(--transition-normal);
      margin-bottom: var(--spacing-3);

      &:not(.primary) {
        background: var(--color-neutral-100);
        color: var(--color-neutral-700);
        border: 2px solid var(--color-neutral-300);

        &:hover {
          background: var(--color-neutral-200);
          border-color: var(--color-neutral-400);
        }
      }

      &.primary {
        background: var(--color-primary-600);
        color: white;
        box-shadow: var(--shadow-md);

        &:hover {
          background: var(--color-primary-700);
          box-shadow: var(--shadow-lg);
          transform: translateY(-2px);
        }
      }
    }

    .pricing-note {
      text-align: center;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
      margin: 0;
    }

    .enterprise-banner {
      margin-top: var(--spacing-12);
      background: linear-gradient(135deg, var(--color-neutral-900) 0%, #1e3a8a 100%);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-10);
      color: white;
      box-shadow: var(--shadow-xl);
      position: relative;
      overflow: hidden;
    }

    .enterprise-banner::before {
      content: '';
      position: absolute;
      top: -50%;
      right: -10%;
      width: 400px;
      height: 400px;
      background: radial-gradient(circle, rgba(99, 102, 241, 0.25) 0%, transparent 70%);
      border-radius: 50%;
      pointer-events: none;
    }

    .enterprise-content {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: var(--spacing-8);
      align-items: center;
      position: relative;
      z-index: 1;
    }

    .enterprise-icon {
      width: 80px;
      height: 80px;
      background: linear-gradient(135deg, #6366f1, #06b6d4);
      border-radius: var(--radius-xl);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;

      i {
        font-size: 2.5rem;
        color: white;
      }
    }

    .enterprise-text {
      flex: 1;

      h3 {
        font-size: var(--font-size-2xl);
        font-weight: var(--font-weight-bold);
        color: white;
        margin: 0 0 var(--spacing-3) 0;
      }

      p {
        font-size: var(--font-size-base);
        line-height: var(--line-height-relaxed);
        color: rgba(255, 255, 255, 0.85);
        margin: 0 0 var(--spacing-4) 0;
      }
    }

    .enterprise-features {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-2);

      li {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: rgba(255, 255, 255, 0.9);

        i {
          color: #14b8a6;
          font-size: 0.875rem;
        }
      }
    }

    .enterprise-cta {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-4) var(--spacing-6);
      background: white;
      color: var(--color-neutral-900);
      text-decoration: none;
      font-weight: var(--font-weight-semibold);
      border-radius: var(--radius-lg);
      transition: all var(--transition-normal);
      flex-shrink: 0;
      white-space: nowrap;

      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 8px 20px rgba(0, 0, 0, 0.3);

        i {
          transform: translateX(4px);
        }
      }

      i {
        transition: transform var(--transition-normal);
      }
    }

    @media (max-width: 1024px) {
      .enterprise-content {
        grid-template-columns: 1fr;
        text-align: center;
      }

      .enterprise-icon {
        margin: 0 auto;
      }

      .enterprise-features {
        grid-template-columns: 1fr;
      }

      .enterprise-cta {
        margin: 0 auto;
      }
    }

    .pricing-faq {
      margin-top: var(--spacing-16);
      padding-top: var(--spacing-16);
      border-top: 1px solid var(--color-neutral-200);
    }

    .faq-title {
      text-align: center;
      font-size: var(--font-size-2xl);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-8) 0;
    }

    .faq-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--spacing-6);
    }

    .faq-item {
      padding: var(--spacing-5);
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-neutral-200);

      h4 {
        font-size: var(--font-size-lg);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-900);
        margin: 0 0 var(--spacing-2) 0;
      }

      p {
        font-size: var(--font-size-base);
        line-height: var(--line-height-relaxed);
        color: var(--color-neutral-600);
        margin: 0;
      }
    }

    @media (max-width: 1024px) {
      .pricing-grid {
        grid-template-columns: 1fr;
        max-width: 500px;
        margin-left: auto;
        margin-right: auto;
      }

      .faq-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (max-width: 768px) {
      .pricing-section {
        padding: var(--spacing-12) var(--spacing-4);
      }
    }
  `]
})
export class PricingSectionComponent {}
