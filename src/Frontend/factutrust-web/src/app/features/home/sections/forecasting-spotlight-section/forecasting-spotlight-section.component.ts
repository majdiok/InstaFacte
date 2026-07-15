import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-forecasting-spotlight-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="forecasting" class="forecasting-section" aria-labelledby="forecasting-title">
      <div class="section-container">
        <div class="forecasting-grid">
          <div class="forecasting-visual" aria-hidden="true">
            <div class="chart-card">
              <div class="chart-header">
                <div>
                  <span class="chart-eyebrow">Prévisions de ventes Q2 2026</span>
                  <h3 class="chart-title">Produit A — Top vente</h3>
                </div>
                <span class="trend up">
                  <i class="pi pi-arrow-up"></i>
                  +18%
                </span>
              </div>

              <svg class="chart-svg" viewBox="0 0 480 200" preserveAspectRatio="none" role="img" aria-label="Graphique de prévision de ventes">
                <defs>
                  <linearGradient id="forecast-gradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#f59e0b" stop-opacity="0.4"></stop>
                    <stop offset="100%" stop-color="#f59e0b" stop-opacity="0"></stop>
                  </linearGradient>
                  <linearGradient id="actual-gradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3b82f6" stop-opacity="0.5"></stop>
                    <stop offset="100%" stop-color="#3b82f6" stop-opacity="0"></stop>
                  </linearGradient>
                </defs>

                <!-- Grid -->
                <line x1="0" y1="50" x2="480" y2="50" stroke="#e2e8f0" stroke-dasharray="4 4" stroke-width="1"></line>
                <line x1="0" y1="100" x2="480" y2="100" stroke="#e2e8f0" stroke-dasharray="4 4" stroke-width="1"></line>
                <line x1="0" y1="150" x2="480" y2="150" stroke="#e2e8f0" stroke-dasharray="4 4" stroke-width="1"></line>

                <!-- Actual data area -->
                <path d="M 0,150 L 60,140 L 120,120 L 180,110 L 240,90 L 240,200 L 0,200 Z"
                      fill="url(#actual-gradient)"></path>

                <!-- Actual data line -->
                <polyline points="0,150 60,140 120,120 180,110 240,90"
                          fill="none" stroke="#3b82f6" stroke-width="2.5"
                          stroke-linecap="round" stroke-linejoin="round"
                          class="line-actual"></polyline>

                <!-- Forecast area -->
                <path d="M 240,90 L 300,75 L 360,60 L 420,50 L 480,35 L 480,200 L 240,200 Z"
                      fill="url(#forecast-gradient)"></path>

                <!-- Forecast line (dashed) -->
                <polyline points="240,90 300,75 360,60 420,50 480,35"
                          fill="none" stroke="#f59e0b" stroke-width="2.5"
                          stroke-dasharray="6 4"
                          stroke-linecap="round" stroke-linejoin="round"
                          class="line-forecast"></polyline>

                <!-- Vertical marker (now) -->
                <line x1="240" y1="20" x2="240" y2="180" stroke="#94a3b8" stroke-dasharray="3 3" stroke-width="1"></line>

                <!-- Data points -->
                <circle cx="0" cy="150" r="4" fill="#3b82f6"></circle>
                <circle cx="60" cy="140" r="4" fill="#3b82f6"></circle>
                <circle cx="120" cy="120" r="4" fill="#3b82f6"></circle>
                <circle cx="180" cy="110" r="4" fill="#3b82f6"></circle>
                <circle cx="240" cy="90" r="6" fill="#3b82f6" stroke="white" stroke-width="2"></circle>

                <circle cx="300" cy="75" r="4" fill="#f59e0b"></circle>
                <circle cx="360" cy="60" r="4" fill="#f59e0b"></circle>
                <circle cx="420" cy="50" r="4" fill="#f59e0b"></circle>
                <circle cx="480" cy="35" r="6" fill="#f59e0b" stroke="white" stroke-width="2"></circle>
              </svg>

              <div class="chart-legend">
                <div class="legend-item">
                  <span class="legend-dot blue"></span>
                  <span>Réalisé</span>
                </div>
                <div class="legend-item">
                  <span class="legend-dot amber dashed"></span>
                  <span>Prévision IA</span>
                </div>
              </div>

              <div class="chart-axis">
                <span>Jan</span><span>Fév</span><span>Mar</span><span>Avr</span><span>Mai</span><span>Juin</span>
              </div>
            </div>

            <div class="abc-card">
              <span class="abc-eyebrow">Classification ABC/XYZ</span>
              <div class="abc-grid">
                <div class="abc-cell a-x">
                  <span class="abc-letter">A</span>
                  <span class="abc-count">42</span>
                </div>
                <div class="abc-cell b-x">
                  <span class="abc-letter">B</span>
                  <span class="abc-count">128</span>
                </div>
                <div class="abc-cell c-x">
                  <span class="abc-letter">C</span>
                  <span class="abc-count">340</span>
                </div>
              </div>
              <span class="abc-caption">Vos produits stars (A) génèrent 80% de votre CA</span>
            </div>

            <div class="calendar-badge">
              <i class="pi pi-calendar"></i>
              <span>Calendrier commercial tunisien intégré</span>
            </div>
          </div>

          <div class="forecasting-content">
            <span class="eyebrow">
              <i class="pi pi-chart-line" aria-hidden="true"></i>
              SCIENCE DES DONNÉES
            </span>
            <h2 id="forecasting-title" class="forecasting-title">
              Anticipez votre activité avec la <span class="gradient-text">science des données</span>
            </h2>
            <p class="forecasting-description">
              InstaFact ne se contente pas d'enregistrer vos ventes : il les analyse, les classe et les projette.
              Identifiez vos produits stars, anticipez les ruptures, planifiez vos achats au bon moment.
            </p>

            <div class="features-list">
              <div class="feature-item">
                <div class="feat-num">01</div>
                <div class="feat-text">
                  <strong>Prévisions de ventes</strong>
                  <p>Modèle statistique enrichi par l'historique. Visualisez votre CA des 6 prochains mois avec un intervalle de confiance.</p>
                </div>
              </div>

              <div class="feature-item">
                <div class="feat-num">02</div>
                <div class="feat-text">
                  <strong>Classification ABC / XYZ</strong>
                  <p>Identifiez automatiquement vos produits stars (A) et dormants (C). Concentrez vos efforts là où ça compte.</p>
                </div>
              </div>

              <div class="feature-item">
                <div class="feat-num">03</div>
                <div class="feat-text">
                  <strong>Recommandations d'achat</strong>
                  <p>"Commandez 50 unités du produit X dans 12 jours" — l'IA calcule quantités et dates optimales selon votre historique.</p>
                </div>
              </div>

              <div class="feature-item">
                <div class="feat-num">04</div>
                <div class="feat-text">
                  <strong>Calendrier commercial tunisien</strong>
                  <p>Ramadan, Aïd, fêtes nationales, rentrée scolaire… La saisonnalité tunisienne est intégrée par défaut.</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .forecasting-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: linear-gradient(180deg, #fffbeb 0%, white 100%);
      position: relative;
    }

    .section-container {
      max-width: 1280px;
      margin: 0 auto;
    }

    .forecasting-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-12);
      align-items: center;
    }

    .forecasting-visual {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4);
    }

    .chart-card {
      background: white;
      border-radius: var(--radius-2xl);
      padding: var(--spacing-6);
      box-shadow: 0 12px 40px rgba(245, 158, 11, 0.12), 0 2px 8px rgba(0, 0, 0, 0.04);
      border: 1px solid #fde68a;
    }

    .chart-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: var(--spacing-4);
    }

    .chart-eyebrow {
      display: block;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: #b45309;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 4px;
    }

    .chart-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin: 0;
    }

    .trend {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 4px 10px;
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);

      &.up {
        background: #dcfce7;
        color: #166534;
      }

      i {
        font-size: 0.75rem;
      }
    }

    .chart-svg {
      width: 100%;
      height: 200px;
      margin-bottom: var(--spacing-3);
    }

    .line-forecast,
    .line-actual {
      stroke-dashoffset: 0;
      animation: drawLine 1.6s ease-out;
    }

    @keyframes drawLine {
      from { stroke-dashoffset: 1000; }
      to { stroke-dashoffset: 0; }
    }

    @media (prefers-reduced-motion: reduce) {
      .line-forecast,
      .line-actual { animation: none; }
    }

    .chart-legend {
      display: flex;
      gap: var(--spacing-5);
      margin-bottom: var(--spacing-2);
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
    }

    .legend-dot {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 50%;

      &.blue {
        background: #3b82f6;
      }

      &.amber.dashed {
        background: transparent;
        border: 2px dashed #f59e0b;
      }
    }

    .chart-axis {
      display: flex;
      justify-content: space-between;
      font-size: var(--font-size-xs);
      color: var(--color-neutral-500);
      padding-top: var(--spacing-2);
      border-top: 1px solid var(--color-neutral-100);
    }

    .abc-card {
      background: white;
      border-radius: var(--radius-2xl);
      padding: var(--spacing-5);
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--color-neutral-200);
    }

    .abc-eyebrow {
      display: block;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-primary-700);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: var(--spacing-3);
    }

    .abc-grid {
      display: grid;
      grid-template-columns: 1fr 2fr 5fr;
      gap: var(--spacing-2);
      margin-bottom: var(--spacing-3);
    }

    .abc-cell {
      padding: var(--spacing-3);
      border-radius: var(--radius-md);
      display: flex;
      flex-direction: column;
      gap: 4px;
      color: white;
      text-align: center;

      &.a-x {
        background: linear-gradient(135deg, #10b981, #059669);
      }

      &.b-x {
        background: linear-gradient(135deg, #3b82f6, #2563eb);
      }

      &.c-x {
        background: linear-gradient(135deg, #94a3b8, #64748b);
      }
    }

    .abc-letter {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-bold);
    }

    .abc-count {
      font-size: var(--font-size-sm);
      opacity: 0.9;
    }

    .abc-caption {
      display: block;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);
    }

    .calendar-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3) var(--spacing-5);
      background: linear-gradient(135deg, #fef3c7, #fde68a);
      border: 1px solid #fbbf24;
      border-radius: var(--radius-full);
      color: #92400e;
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      align-self: flex-start;

      i {
        font-size: 1rem;
        color: #d97706;
      }
    }

    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      background: #fef3c7;
      color: #92400e;
      border: 1px solid #fde68a;
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);

      i {
        font-size: 0.875rem;
        color: #d97706;
      }
    }

    .forecasting-title {
      font-size: clamp(2rem, 4vw, 3rem);
      font-weight: var(--font-weight-bold);
      line-height: 1.15;
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-5) 0;
    }

    .gradient-text {
      background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .forecasting-description {
      font-size: var(--font-size-lg);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0 0 var(--spacing-8) 0;
    }

    .features-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-5);
    }

    .feature-item {
      display: flex;
      gap: var(--spacing-4);
      padding: var(--spacing-4);
      background: white;
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-xl);
      transition: all var(--transition-normal);

      &:hover {
        border-color: #fbbf24;
        box-shadow: 0 4px 12px rgba(245, 158, 11, 0.1);
      }
    }

    .feat-num {
      width: 40px;
      height: 40px;
      border-radius: var(--radius-md);
      background: linear-gradient(135deg, #f59e0b, #d97706);
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: var(--font-weight-bold);
      font-size: var(--font-size-sm);
      flex-shrink: 0;
    }

    .feat-text {
      flex: 1;

      strong {
        display: block;
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: var(--color-neutral-900);
        margin-bottom: 4px;
      }

      p {
        font-size: var(--font-size-sm);
        line-height: 1.55;
        color: var(--color-neutral-600);
        margin: 0;
      }
    }

    @media (max-width: 1024px) {
      .forecasting-grid {
        grid-template-columns: 1fr;
        gap: var(--spacing-10);
      }

      .forecasting-visual {
        max-width: 600px;
        margin: 0 auto;
      }
    }

    @media (max-width: 768px) {
      .forecasting-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .chart-header {
        flex-direction: column;
        gap: var(--spacing-2);
      }

      .abc-grid {
        grid-template-columns: 1fr 1fr 1fr;
      }
    }
  `]
})
export class ForecastingSpotlightSectionComponent {}
