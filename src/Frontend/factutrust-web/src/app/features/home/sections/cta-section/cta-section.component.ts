import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-cta-section',
  standalone: true,
  imports: [CommonModule, RouterModule, ButtonModule],
  template: `
    <section class="cta-section">
      <div class="cta-container">
        <div class="cta-content">
          <h2 class="cta-title">Prêt à gagner 4 heures par mois et à dormir tranquille ?</h2>
          <p class="cta-description">
            Rejoignez les entreprises tunisiennes qui font confiance à InstaFact.
            <strong>10 factures gratuites par mois</strong> pour tester sans risque. Aucune carte bancaire, aucun engagement.
          </p>
          <p class="cta-benefit">
            ⏱️ <strong>Plus de 4h/mois économisées</strong> dès la 1ère facture
          </p>
          <div class="cta-buttons">
            <a routerLink="/auth/register" class="cta-button-primary">
              Créer mon compte gratuit — 10 factures offertes
              <i class="pi pi-arrow-right"></i>
            </a>
            <a routerLink="/auth/login" class="cta-button-secondary">
              J'ai déjà un compte
            </a>
          </div>
          <div class="cta-reassurance">
            <div class="reassurance-item">
              <i class="pi pi-check-circle"></i>
              <span><strong>10 factures gratuites</strong> par mois</span>
            </div>
            <div class="reassurance-item">
              <i class="pi pi-clock"></i>
              <span>Configuration en <strong>5 minutes</strong></span>
            </div>
            <div class="reassurance-item">
              <i class="pi pi-shield"></i>
              <span><strong>Sans carte bancaire</strong>, sans engagement</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .cta-section {
      padding: var(--spacing-24) var(--spacing-6);
      background: linear-gradient(135deg, var(--color-primary-600) 0%, var(--color-primary-700) 50%, var(--color-primary-800) 100%);
      color: white;
      position: relative;
      overflow: hidden;
      z-index: 1;
    }

    .cta-section::before {
      content: '';
      position: absolute;
      top: -50%;
      right: -20%;
      width: 800px;
      height: 800px;
      background: radial-gradient(circle, rgba(255, 255, 255, 0.15) 0%, transparent 70%);
      border-radius: 50%;
      animation: pulse 15s ease-in-out infinite;
    }

    .cta-section::after {
      content: '';
      position: absolute;
      bottom: -50%;
      left: -20%;
      width: 700px;
      height: 700px;
      background: radial-gradient(circle, rgba(255, 255, 255, 0.1) 0%, transparent 70%);
      border-radius: 50%;
      animation: pulse 20s ease-in-out infinite reverse;
    }

    @keyframes pulse {
      0%, 100% {
        transform: scale(1);
        opacity: 0.5;
      }
      50% {
        transform: scale(1.2);
        opacity: 0.8;
      }
    }

    .cta-container {
      max-width: 800px;
      margin: 0 auto;
      position: relative;
      z-index: 1;
    }

    .cta-content {
      text-align: center;
      animation: fadeInUp 0.8s ease-out;
    }

    @keyframes fadeInUp {
      from {
        opacity: 0;
        transform: translateY(30px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .cta-title {
      font-size: clamp(2rem, 5vw, 3rem);
      font-weight: var(--font-weight-bold);
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-6) 0;
      color: white;
    }

    .cta-description {
      font-size: var(--font-size-xl);
      line-height: var(--line-height-relaxed);
      color: rgba(255, 255, 255, 0.9);
      margin: 0 0 var(--spacing-4) 0;
      max-width: 600px;
      margin-left: auto;
      margin-right: auto;
    }

    .cta-benefit {
      display: inline-block;
      margin: 0 auto var(--spacing-8);
      padding: 0.55rem 1.25rem;
      background: rgba(255, 255, 255, 0.15);
      border: 1px solid rgba(255, 255, 255, 0.3);
      border-radius: 999px;
      color: #ffffff;
      font-size: 1rem;
      font-weight: 600;
    }
    .cta-benefit strong {
      color: #fbbf24;
    }

    .cta-buttons {
      display: flex;
      gap: var(--spacing-4);
      justify-content: center;
      flex-wrap: wrap;
      margin-bottom: var(--spacing-8);
    }

    .cta-button-primary {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-3);
      background: white;
      color: var(--color-primary-600);
      text-decoration: none;
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-lg);
      padding: var(--spacing-5) var(--spacing-12);
      border-radius: var(--radius-lg);
      box-shadow: 
        0 10px 30px rgba(0, 0, 0, 0.2),
        0 0 0 0 rgba(255, 255, 255, 0.4);
      transition: all var(--transition-normal);
      position: relative;
      overflow: hidden;

      &::before {
        content: '';
        position: absolute;
        top: 50%;
        left: 50%;
        width: 0;
        height: 0;
        border-radius: 50%;
        background: rgba(37, 99, 235, 0.1);
        transform: translate(-50%, -50%);
        transition: width 0.6s, height 0.6s;
      }

      &:hover {
        transform: translateY(-3px) scale(1.02);
        box-shadow: 
          0 20px 50px rgba(0, 0, 0, 0.3),
          0 0 0 8px rgba(255, 255, 255, 0.1);

        &::before {
          width: 300px;
          height: 300px;
        }

        i {
          transform: translateX(6px);
        }
      }

      &:active {
        transform: translateY(-1px) scale(1);
      }

      &:focus-visible {
        outline: 3px solid rgba(255, 255, 255, 0.9);
        outline-offset: 2px;
      }

      i {
        transition: transform var(--transition-normal);
        position: relative;
        z-index: 1;
      }

      span {
        position: relative;
        z-index: 1;
      }
    }

    .cta-button-secondary {
      display: inline-flex;
      align-items: center;
      padding: var(--spacing-5) var(--spacing-10);
      color: white;
      text-decoration: none;
      font-weight: var(--font-weight-medium);
      font-size: var(--font-size-lg);
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-radius: var(--radius-lg);
      transition: all var(--transition-normal);

      &:hover {
        background: rgba(255, 255, 255, 0.1);
        border-color: rgba(255, 255, 255, 0.5);
      }

      &:focus-visible {
        outline: 3px solid rgba(255, 255, 255, 0.9);
        outline-offset: 2px;
      }
    }

    .cta-reassurance {
      display: flex;
      justify-content: center;
      gap: var(--spacing-8);
      flex-wrap: wrap;
      padding-top: var(--spacing-8);
      border-top: 1px solid rgba(255, 255, 255, 0.2);
    }

    .reassurance-item {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      color: rgba(255, 255, 255, 0.9);
      font-size: var(--font-size-base);

      i {
        color: rgba(255, 255, 255, 0.8);
        font-size: 1.125rem;
      }
    }

    @media (max-width: 768px) {
      .cta-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .cta-buttons {
        flex-direction: column;
        width: 100%;

        .cta-button-primary,
        .cta-button-secondary {
          width: 100%;
          justify-content: center;
        }
      }

      .cta-reassurance {
        flex-direction: column;
        gap: var(--spacing-4);
      }
    }
  `]
})
export class CtaSectionComponent {}
