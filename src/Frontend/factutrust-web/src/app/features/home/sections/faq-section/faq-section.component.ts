import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-faq-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="faq" class="faq-section" aria-labelledby="faq-title">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">FAQ</span>
          <h2 id="faq-title" class="section-title">Questions fréquentes</h2>
          <p class="section-description">
            Tout ce que vous voulez savoir avant de commencer. Une autre question ? <a routerLink="/auth/register">Créez un compte gratuit</a> et explorez la plateforme.
          </p>
        </div>

        <div class="faq-list">
          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">InstaFact est-il conforme à la réglementation tunisienne (TEJ) ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                <strong>Oui, InstaFact est conçu pour respecter intégralement la réglementation tunisienne.</strong>
                La plateforme calcule automatiquement les retenues à la source conformément aux barèmes TEJ,
                génère les certificats RS, exporte les déclarations XML au format requis, et trace toutes les écritures
                dans une piste d'audit infalsifiable. Vous êtes prêt pour les contrôles fiscaux.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Mes données sont-elles hébergées en sécurité ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                Vos données sont chiffrées en transit (TLS) et au repos. Chaque entreprise dispose d'un espace
                <strong>isolé multi-tenant</strong> : vos données ne sont jamais mélangées avec celles d'autres clients.
                Sauvegardes automatiques quotidiennes, audit chain immuable, authentification renforcée (2FA disponible).
                Pour les besoins critiques, le plan Entreprise propose un hébergement dédié.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Puis-je importer mes anciennes factures et données ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                Oui. InstaFact accepte les imports CSV et Excel pour vos clients, produits, factures et écritures comptables.
                <strong>Notre IA peut également extraire automatiquement les données</strong> depuis vos anciennes factures PDF
                pour vous faire gagner du temps. Les plans Entreprise incluent un accompagnement de migration personnalisé.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">L'IA a-t-elle accès à mes données privées ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                Vous avez le contrôle total.
              </p>
              <ul>
                <li><strong>Mode locale (self-hosted)</strong> : 100% local, vos données ne quittent jamais votre infrastructure.</li>
              </ul>
              <p>
                Vous pouvez aussi désactiver complètement l'IA si vous le souhaitez.
                Aucune donnée n'est utilisée pour entraîner des modèles tiers.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Que se passe-t-il si je dépasse le plan gratuit ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                Vous recevez une notification quand vous approchez de la limite (10 factures/mois sur le plan gratuit).
                Vous pouvez alors :
              </p>
              <ul>
                <li>Soit passer au plan Mensuel ou Annuel — facturation immédiate au prorata.</li>
                <li>Soit attendre le mois suivant pour reprendre l'émission.</li>
              </ul>
              <p><strong>Aucune perte de données, aucune surprise sur la facture.</strong></p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Combien d'utilisateurs puis-je créer ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                InstaFact propose <strong>10 rôles utilisateurs prédéfinis</strong> avec permissions granulaires
                (Admin, Comptable, Commercial, Caissier, Magasinier, Acheteur, Auditeur…).
                Le nombre d'utilisateurs varie selon le plan : illimité sur le plan Annuel et Entreprise.
                Chaque utilisateur a sa propre authentification, son journal d'actions et son périmètre de visibilité.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Y a-t-il une application mobile ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                InstaFact est une <strong>web app responsive</strong> qui fonctionne parfaitement sur smartphone et tablette.
                Vous pouvez l'installer comme une PWA (Progressive Web App) sur votre écran d'accueil pour un accès rapide.
                Une application native iOS/Android est sur la roadmap pour 2026.
              </p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Comment migrer depuis un autre logiciel de facturation ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>
                Trois options selon votre situation :
              </p>
              <ul>
                <li><strong>Import CSV / Excel</strong> : exportez vos données depuis votre logiciel actuel et importez-les en quelques clics.</li>
                <li><strong>OCR via IA</strong> : pour les factures historiques en PDF, l'IA extrait automatiquement les données.</li>
                <li><strong>Migration assistée</strong> : avec le plan Entreprise, notre équipe vous accompagne sur toute la migration.</li>
              </ul>
              <p>Comptez 1 à 3 jours pour une migration standard, selon le volume de données.</p>
            </div>
          </details>

          <details class="faq-item">
            <summary class="faq-question">
              <span class="question-text">Quel plan choisir pour mon activité ?</span>
              <span class="question-icon" aria-hidden="true">
                <i class="pi pi-plus"></i>
                <i class="pi pi-minus"></i>
              </span>
            </summary>
            <div class="faq-answer">
              <p>Voici un guide rapide selon votre profil :</p>
              <ul>
                <li><strong>Plan Gratuit</strong> — TPE / freelance qui émet jusqu'à 10 factures par mois et veut tester la plateforme sans engagement.</li>
                <li><strong>Plan Mensuel (49 TND/mois)</strong> — PME en croissance avec un volume variable, qui préfère payer au mois et ajuster.</li>
                <li><strong>Plan Annuel (39 TND/mois)</strong> — PME établie qui a validé son besoin et souhaite économiser 20%.</li>
                <li><strong>Plan Entreprise</strong> — Groupes, cabinets comptables multi-clients, ou besoins d'intégrations sur mesure (multi-tenant, SLA, formation, API custom).</li>
              </ul>
              <p>Vous pouvez changer de plan à tout moment depuis votre espace.</p>
            </div>
          </details>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .faq-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: white;
      position: relative;
    }

    .section-container {
      max-width: 920px;
      margin: 0 auto;
    }

    .section-header {
      text-align: center;
      margin-bottom: var(--spacing-12);
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
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;

      a {
        color: var(--color-primary-600);
        text-decoration: none;
        font-weight: var(--font-weight-semibold);

        &:hover {
          text-decoration: underline;
        }
      }
    }

    .faq-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
    }

    .faq-item {
      background: var(--color-neutral-50);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      overflow: hidden;
      transition: all var(--transition-normal);

      &:hover {
        border-color: var(--color-primary-300);
      }

      &[open] {
        background: white;
        border-color: var(--color-primary-300);
        box-shadow: 0 4px 12px rgba(37, 99, 235, 0.08);

        .question-icon .pi-plus {
          display: none;
        }

        .question-icon .pi-minus {
          display: inline-block;
        }
      }
    }

    .faq-question {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-4);
      padding: var(--spacing-5) var(--spacing-6);
      cursor: pointer;
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      list-style: none;
      transition: all var(--transition-normal);

      &::-webkit-details-marker {
        display: none;
      }

      &:hover {
        color: var(--color-primary-700);
      }

      &:focus-visible {
        outline: 3px solid var(--color-primary-300);
        outline-offset: -3px;
      }
    }

    .question-text {
      flex: 1;
      line-height: 1.4;
    }

    .question-icon {
      width: 32px;
      height: 32px;
      border-radius: var(--radius-md);
      background: var(--color-primary-100);
      color: var(--color-primary-700);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      transition: all var(--transition-normal);

      i {
        font-size: 0.875rem;
      }

      .pi-minus {
        display: none;
      }
    }

    .faq-item[open] .question-icon {
      background: var(--color-primary-600);
      color: white;
    }

    .faq-answer {
      padding: 0 var(--spacing-6) var(--spacing-5);
      animation: answerSlide 0.3s ease-out;

      p {
        margin: 0 0 var(--spacing-3) 0;
        font-size: var(--font-size-base);
        line-height: var(--line-height-relaxed);
        color: var(--color-neutral-700);

        &:last-child {
          margin-bottom: 0;
        }
      }

      ul {
        margin: var(--spacing-2) 0 var(--spacing-3) 0;
        padding-left: var(--spacing-5);

        li {
          font-size: var(--font-size-base);
          line-height: var(--line-height-relaxed);
          color: var(--color-neutral-700);
          margin-bottom: var(--spacing-2);
        }
      }

      strong {
        color: var(--color-neutral-900);
      }
    }

    @keyframes answerSlide {
      from { opacity: 0; transform: translateY(-8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (prefers-reduced-motion: reduce) {
      .faq-answer { animation: none; }
    }

    @media (max-width: 768px) {
      .faq-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .faq-question {
        padding: var(--spacing-4);
        font-size: var(--font-size-base);
      }

      .faq-answer {
        padding: 0 var(--spacing-4) var(--spacing-4);
      }
    }
  `]
})
export class FaqSectionComponent {}
