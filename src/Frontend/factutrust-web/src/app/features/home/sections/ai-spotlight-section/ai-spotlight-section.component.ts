import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-ai-spotlight-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="ia" class="ai-section" aria-labelledby="ai-title">
      <div class="ai-bg-glow blob-a" aria-hidden="true"></div>
      <div class="ai-bg-glow blob-b" aria-hidden="true"></div>

      <div class="section-container">
        <div class="ai-grid">
          <div class="ai-content">
            <span class="eyebrow">
              <i class="pi pi-bolt" aria-hidden="true"></i>
              INTELLIGENCE INTÉGRÉE
            </span>
            <h2 id="ai-title" class="ai-title">
              Votre <span class="gradient-ai">assistant intelligent</span> intégré
            </h2>
            <p class="ai-description">
              InstaFact embarque une IA conversationnelle qui comprend vos données métier.
              Posez des questions en français, extrayez du texte de vos PDF, recevez des recommandations contextuelles.
            </p>

            <div class="capabilities">
              <div class="capability">
                <div class="cap-icon"><i class="pi pi-comments"></i></div>
                <div class="cap-text">
                  <strong>Chat conversationnel</strong>
                  <span>Posez des questions sur vos ventes, vos clients, vos stocks. L'IA répond en utilisant uniquement vos données.</span>
                </div>
              </div>

              <div class="capability">
                <div class="cap-icon"><i class="pi pi-file-import"></i></div>
                <div class="cap-text">
                  <strong>OCR de factures fournisseurs</strong>
                  <span>Glissez-déposez un PDF, l'IA extrait montants, TVA, dates et fournisseur automatiquement.</span>
                </div>
              </div>

              <div class="capability">
                <div class="cap-icon"><i class="pi pi-sparkles"></i></div>
                <div class="cap-text">
                  <strong>Recommandations contextuelles</strong>
                  <span>Suggestions intelligentes de clients, produits, prix dans tous les formulaires.</span>
                </div>
              </div>
            </div>

            <div class="ai-options">
              <div class="ai-option">
                <i class="pi pi-cloud" aria-hidden="true"></i>
                <div>
                  <strong>OpenAI</strong>
                  <span>GPT-4 cloud, performance maximale</span>
                </div>
              </div>
              <div class="ai-option">
                <i class="pi pi-shield" aria-hidden="true"></i>
                <div>
                  <span>100% local, données 100% privées</span>
                </div>
              </div>
            </div>
          </div>

          <div class="ai-visual">
            <div class="chat-mockup" aria-hidden="true">
              <div class="chat-header">
                <div class="chat-avatar">
                  <i class="pi pi-bolt"></i>
                </div>
                <div class="chat-title">
                  <strong>Assistant InstaFact</strong>
                  <span>● En ligne</span>
                </div>
              </div>

              <div class="chat-body">
                <div class="chat-bubble user">
                  <p>Quels sont mes 3 meilleurs clients ce trimestre ?</p>
                </div>

                <div class="chat-bubble bot typing">
                  <p>
                    Voici vos top clients du Q1 2026 :<br>
                    <strong>1.</strong> Tech Solutions SARL — <strong>14 850 TND</strong><br>
                    <strong>2.</strong> Marina Distribution — <strong>9 420 TND</strong><br>
                    <strong>3.</strong> Atelier Méditerranée — <strong>7 180 TND</strong>
                  </p>
                  <div class="chat-chart">
                    <div class="bar bar-1"></div>
                    <div class="bar bar-2"></div>
                    <div class="bar bar-3"></div>
                  </div>
                </div>

                <div class="chat-bubble user">
                  <p>Et si on appliquait une remise de 5% à Marina ?</p>
                </div>

                <div class="chat-bubble bot">
                  <p>
                    Marina générerait <strong>~471 TND de remises</strong> sur 12 mois.
                    Recommandation : remise ciblée sur les 3 produits ABC où elle achète 80% du volume.
                  </p>
                </div>
              </div>

              <div class="chat-input" aria-hidden="true">
                <i class="pi pi-microphone"></i>
                <span class="cursor-blink"></span>
                <i class="pi pi-send"></i>
              </div>
            </div>

            <div class="floating-stat float-1">
              <i class="pi pi-bolt"></i>
              <div>
                <span class="label">Réponse en</span>
                <strong>1.2s</strong>
              </div>
            </div>

            <div class="floating-stat float-2">
              <i class="pi pi-shield"></i>
              <div>
                <span class="label">Données</span>
                <strong>100% privées</strong>
              </div>
            </div>

            <div class="ai-inclusion-badge">
              <i class="pi pi-check-circle" aria-hidden="true"></i>
              <span>Inclus dans tous les plans payants</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .ai-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: linear-gradient(135deg, #0f172a 0%, #1e1b4b 100%);
      color: white;
      position: relative;
      overflow: hidden;
      isolation: isolate;
    }

    .ai-bg-glow {
      position: absolute;
      border-radius: 50%;
      filter: blur(100px);
      pointer-events: none;
      z-index: 0;
    }

    .blob-a {
      width: 600px;
      height: 600px;
      background: radial-gradient(circle, #8b5cf6 0%, transparent 70%);
      opacity: 0.25;
      top: -200px;
      right: -150px;
    }

    .blob-b {
      width: 500px;
      height: 500px;
      background: radial-gradient(circle, #06b6d4 0%, transparent 70%);
      opacity: 0.2;
      bottom: -150px;
      left: -100px;
    }

    .section-container {
      max-width: 1280px;
      margin: 0 auto;
      position: relative;
      z-index: 1;
    }

    .ai-grid {
      display: grid;
      grid-template-columns: 1.1fr 1fr;
      gap: var(--spacing-12);
      align-items: center;
    }

    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      background: rgba(139, 92, 246, 0.15);
      color: #c4b5fd;
      border: 1px solid rgba(139, 92, 246, 0.3);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-4);

      i {
        font-size: 0.875rem;
        color: #a78bfa;
      }
    }

    .ai-title {
      font-size: clamp(2rem, 4vw, 3rem);
      font-weight: var(--font-weight-bold);
      line-height: 1.15;
      margin: 0 0 var(--spacing-5) 0;
      color: white;
    }

    .gradient-ai {
      background: linear-gradient(135deg, #c084fc 0%, #06b6d4 50%, #a5f3fc 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .ai-description {
      font-size: var(--font-size-lg);
      line-height: var(--line-height-relaxed);
      color: #cbd5e1;
      margin: 0 0 var(--spacing-8) 0;
    }

    .capabilities {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-5);
      margin-bottom: var(--spacing-8);
    }

    .capability {
      display: flex;
      gap: var(--spacing-4);
      align-items: flex-start;
    }

    .cap-icon {
      width: 48px;
      height: 48px;
      border-radius: var(--radius-lg);
      background: linear-gradient(135deg, rgba(139, 92, 246, 0.2), rgba(6, 182, 212, 0.2));
      border: 1px solid rgba(139, 92, 246, 0.3);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;

      i {
        font-size: 1.25rem;
        color: #c4b5fd;
      }
    }

    .cap-text {
      flex: 1;

      strong {
        display: block;
        font-size: var(--font-size-base);
        font-weight: var(--font-weight-semibold);
        color: white;
        margin-bottom: 4px;
      }

      span {
        font-size: var(--font-size-sm);
        line-height: 1.55;
        color: #94a3b8;
      }
    }

    .ai-options {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-3);
      padding-top: var(--spacing-5);
      border-top: 1px solid rgba(255, 255, 255, 0.1);
    }

    .ai-option {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-3) var(--spacing-4);
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: var(--radius-lg);

      i {
        font-size: 1.25rem;
        color: #06b6d4;
        flex-shrink: 0;
      }

      strong {
        display: block;
        color: white;
        font-size: var(--font-size-sm);
      }

      span {
        display: block;
        font-size: var(--font-size-xs);
        color: #94a3b8;
      }
    }

    .ai-visual {
      position: relative;
    }

    .chat-mockup {
      background: rgba(15, 23, 42, 0.8);
      backdrop-filter: blur(20px);
      border: 1px solid rgba(139, 92, 246, 0.25);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-5);
      box-shadow:
        0 25px 50px rgba(0, 0, 0, 0.5),
        0 0 100px rgba(139, 92, 246, 0.15);
    }

    .chat-header {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding-bottom: var(--spacing-4);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      margin-bottom: var(--spacing-4);
    }

    .chat-avatar {
      width: 40px;
      height: 40px;
      border-radius: var(--radius-md);
      background: linear-gradient(135deg, #8b5cf6, #06b6d4);
      display: flex;
      align-items: center;
      justify-content: center;

      i {
        color: white;
        font-size: 1.1rem;
      }
    }

    .chat-title {
      strong {
        display: block;
        color: white;
        font-size: var(--font-size-sm);
        font-weight: var(--font-weight-semibold);
      }

      span {
        font-size: 0.7rem;
        color: #10b981;
      }
    }

    .chat-body {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3);
      max-height: 380px;
      overflow: hidden;
    }

    .chat-bubble {
      max-width: 85%;
      padding: var(--spacing-3) var(--spacing-4);
      border-radius: var(--radius-xl);
      font-size: var(--font-size-sm);
      line-height: 1.5;
      animation: bubbleIn 0.4s ease-out;

      p {
        margin: 0;
      }
    }

    @keyframes bubbleIn {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (prefers-reduced-motion: reduce) {
      .chat-bubble { animation: none; }
    }

    .chat-bubble.user {
      align-self: flex-end;
      background: linear-gradient(135deg, #6366f1, #4f46e5);
      color: white;
      border-bottom-right-radius: 4px;
    }

    .chat-bubble.bot {
      align-self: flex-start;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid rgba(255, 255, 255, 0.08);
      color: #e2e8f0;
      border-bottom-left-radius: 4px;

      strong {
        color: #c4b5fd;
      }
    }

    .chat-chart {
      display: flex;
      align-items: flex-end;
      gap: 6px;
      margin-top: var(--spacing-3);
      height: 50px;
    }

    .bar {
      flex: 1;
      background: linear-gradient(180deg, #c084fc 0%, #6366f1 100%);
      border-radius: 4px 4px 0 0;
      animation: barGrow 0.8s ease-out;
    }

    .bar-1 { height: 100%; }
    .bar-2 { height: 65%; animation-delay: 0.1s; }
    .bar-3 { height: 50%; animation-delay: 0.2s; }

    @keyframes barGrow {
      from { height: 0; }
    }

    @media (prefers-reduced-motion: reduce) {
      .bar { animation: none; }
    }

    .chat-input {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      margin-top: var(--spacing-4);
      padding: var(--spacing-3) var(--spacing-4);
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: var(--radius-full);

      i {
        color: #94a3b8;
        font-size: 1rem;
      }
    }

    .cursor-blink {
      flex: 1;
      height: 16px;
      position: relative;

      &::after {
        content: '';
        position: absolute;
        left: 0;
        top: 0;
        width: 2px;
        height: 100%;
        background: #06b6d4;
        animation: blink 1s step-end infinite;
      }
    }

    @keyframes blink {
      50% { opacity: 0; }
    }

    @media (prefers-reduced-motion: reduce) {
      .cursor-blink::after { animation: none; }
    }

    .floating-stat {
      position: absolute;
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-3) var(--spacing-4);
      background: rgba(15, 23, 42, 0.9);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: var(--radius-lg);
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);
      animation: floatStat 6s ease-in-out infinite;

      i {
        font-size: 1.5rem;
      }

      .label {
        display: block;
        font-size: 0.7rem;
        color: #94a3b8;
      }

      strong {
        display: block;
        color: white;
        font-size: var(--font-size-sm);
        font-weight: var(--font-weight-semibold);
      }
    }

    .float-1 {
      top: -20px;
      right: -20px;
      animation-delay: 0s;

      i {
        color: #fbbf24;
      }
    }

    .float-2 {
      bottom: 20px;
      left: -30px;
      animation-delay: -3s;

      i {
        color: #10b981;
      }
    }

    @keyframes floatStat {
      0%, 100% { transform: translateY(0); }
      50% { transform: translateY(-10px); }
    }

    @media (prefers-reduced-motion: reduce) {
      .floating-stat { animation: none; }
    }

    .ai-inclusion-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 1.5rem;
      padding: 0.55rem 1rem;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #6ee7b7;
      border-radius: 999px;
      font-size: 0.85rem;
      font-weight: 600;
    }
    .ai-inclusion-badge i {
      color: #10b981;
      font-size: 0.95rem;
    }

    @media (max-width: 1024px) {
      .ai-grid {
        grid-template-columns: 1fr;
        gap: var(--spacing-10);
      }

      .ai-visual {
        max-width: 480px;
        margin: 0 auto;
      }

      .floating-stat {
        display: none;
      }
    }

    @media (max-width: 768px) {
      .ai-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .ai-options {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class AiSpotlightSectionComponent {}
