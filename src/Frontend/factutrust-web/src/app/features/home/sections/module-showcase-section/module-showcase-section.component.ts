import { Component, OnInit, OnDestroy, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

interface ModuleTab {
  id: string;
  icon: string;
  title: string;
  tagline: string;
  description: string;
  features: string[];
  highlight: string;
  accent: 'blue' | 'teal' | 'purple' | 'amber' | 'pink' | 'green';
}

@Component({
  selector: 'app-module-showcase-section',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="modules" class="modules-section" aria-labelledby="modules-title" (mouseenter)="pauseRotation()" (mouseleave)="resumeRotation()">
      <div class="section-container">
        <div class="section-header">
          <span class="eyebrow">TOUT EST INCLUS · 10 MODULES</span>
          <h2 id="modules-title" class="section-title">Une plateforme, dix modules métier intégrés</h2>
          <p class="section-description">
            Explorez les modules clés de InstaFact. Tous les modules communiquent entre eux pour vous offrir une vision unifiée de votre activité.
          </p>
        </div>

        <div class="showcase-layout">
          <div class="tabs-list" role="tablist" aria-orientation="vertical">
            @for (tab of modules; track tab.id; let i = $index) {
              <button
                type="button"
                role="tab"
                [id]="'tab-' + tab.id"
                [attr.aria-selected]="activeIndex() === i"
                [attr.aria-controls]="'panel-' + tab.id"
                [attr.tabindex]="activeIndex() === i ? 0 : -1"
                class="tab-button"
                [class.active]="activeIndex() === i"
                [class.accent-blue]="tab.accent === 'blue'"
                [class.accent-teal]="tab.accent === 'teal'"
                [class.accent-purple]="tab.accent === 'purple'"
                [class.accent-amber]="tab.accent === 'amber'"
                [class.accent-pink]="tab.accent === 'pink'"
                [class.accent-green]="tab.accent === 'green'"
                (click)="selectTab(i)"
                (keydown)="onKeydown($event, i)">
                <span class="tab-icon" aria-hidden="true">
                  <i [class]="'pi ' + tab.icon"></i>
                </span>
                <span class="tab-title">{{ tab.title }}</span>
                <span class="tab-arrow" aria-hidden="true">
                  <i class="pi pi-chevron-right"></i>
                </span>
              </button>
            }
          </div>

          <div
            class="tab-panel"
            role="tabpanel"
            [id]="'panel-' + activeTab().id"
            [attr.aria-labelledby]="'tab-' + activeTab().id">
            <div class="panel-content" [@.disabled]="false" [class]="'accent-' + activeTab().accent">
              <div class="panel-header">
                <span class="panel-eyebrow">{{ activeTab().tagline }}</span>
                <h3 class="panel-title">{{ activeTab().title }}</h3>
                <p class="panel-description">{{ activeTab().description }}</p>
              </div>

              <ul class="panel-features">
                @for (feature of activeTab().features; track feature) {
                  <li>
                    <i class="pi pi-check-circle" aria-hidden="true"></i>
                    <span>{{ feature }}</span>
                  </li>
                }
              </ul>

              <div class="panel-highlight">
                <i class="pi pi-bolt" aria-hidden="true"></i>
                <span>{{ activeTab().highlight }}</span>
              </div>

              <div class="panel-visual" aria-hidden="true">
                <div class="visual-icon">
                  <i [class]="'pi ' + activeTab().icon"></i>
                </div>
                <div class="visual-orbit"></div>
                <div class="visual-orbit visual-orbit-2"></div>
              </div>
            </div>
          </div>
        </div>

        <div class="rotation-status" aria-hidden="true">
          @for (tab of modules; track tab.id; let i = $index) {
            <button
              type="button"
              class="rotation-dot"
              [class.active]="activeIndex() === i"
              [attr.aria-label]="'Aller à ' + tab.title"
              (click)="selectTab(i)"></button>
          }
        </div>
      </div>
    </section>
  `,
  styles: [`
    .modules-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: white;
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

    .showcase-layout {
      display: grid;
      grid-template-columns: 320px 1fr;
      gap: var(--spacing-8);
      align-items: stretch;
      margin-bottom: var(--spacing-8);
    }

    .tabs-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      background: var(--color-neutral-50);
      padding: var(--spacing-3);
      border-radius: var(--radius-2xl);
      border: 1px solid var(--color-neutral-200);
    }

    .tab-button {
      display: flex;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-3) var(--spacing-4);
      background: transparent;
      border: 1px solid transparent;
      border-radius: var(--radius-lg);
      cursor: pointer;
      font-family: inherit;
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
      text-align: left;
      transition: all var(--transition-normal);
      width: 100%;

      &:hover {
        background: white;
        color: var(--color-neutral-900);
      }

      &:focus-visible {
        outline: 3px solid var(--color-primary-300);
        outline-offset: 2px;
      }

      &.active {
        background: white;
        border-color: var(--color-primary-200);
        box-shadow: var(--shadow-md);
        color: var(--color-neutral-900);

        .tab-icon {
          background: var(--color-primary-100);
          color: var(--color-primary-700);
        }

        .tab-arrow {
          opacity: 1;
          transform: translateX(2px);
        }
      }
    }

    .tab-icon {
      width: 36px;
      height: 36px;
      border-radius: var(--radius-md);
      background: var(--color-neutral-200);
      color: var(--color-neutral-600);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      transition: all var(--transition-normal);

      i {
        font-size: 1.1rem;
      }
    }

    .tab-title {
      flex: 1;
      min-width: 0;
    }

    .tab-arrow {
      opacity: 0;
      color: var(--color-primary-600);
      transition: all var(--transition-normal);
    }

    .tab-panel {
      background: linear-gradient(135deg, var(--color-neutral-50) 0%, white 100%);
      border: 1px solid var(--color-neutral-200);
      border-radius: var(--radius-2xl);
      padding: var(--spacing-10);
      position: relative;
      overflow: hidden;
      min-height: 460px;
    }

    .panel-content {
      position: relative;
      z-index: 1;
      animation: panelFade 0.4s ease-out;
    }

    @keyframes panelFade {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (prefers-reduced-motion: reduce) {
      .panel-content {
        animation: none;
      }
    }

    .panel-header {
      max-width: 520px;
      margin-bottom: var(--spacing-6);
    }

    .panel-eyebrow {
      display: inline-block;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-primary-700);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin-bottom: var(--spacing-2);
    }

    .panel-title {
      font-size: clamp(1.5rem, 2.5vw, 1.875rem);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-3) 0;
    }

    .panel-description {
      font-size: var(--font-size-base);
      line-height: var(--line-height-relaxed);
      color: var(--color-neutral-600);
      margin: 0;
    }

    .panel-features {
      list-style: none;
      padding: 0;
      margin: 0 0 var(--spacing-6) 0;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-3);
      max-width: 600px;

      li {
        display: flex;
        align-items: flex-start;
        gap: var(--spacing-2);
        font-size: var(--font-size-sm);
        color: var(--color-neutral-700);
        line-height: 1.5;

        i {
          color: var(--color-success-600);
          font-size: 1rem;
          margin-top: 2px;
          flex-shrink: 0;
        }
      }
    }

    .panel-highlight {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-3) var(--spacing-5);
      background: var(--color-primary-50);
      color: var(--color-primary-700);
      border: 1px solid var(--color-primary-200);
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);

      i {
        color: var(--color-primary-600);
      }
    }

    .panel-visual {
      position: absolute;
      top: 50%;
      right: var(--spacing-8);
      transform: translateY(-50%);
      width: 220px;
      height: 220px;
      pointer-events: none;
    }

    .visual-icon {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: 100px;
      height: 100px;
      border-radius: var(--radius-2xl);
      background: linear-gradient(135deg, var(--color-primary-500), var(--color-primary-700));
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 16px 40px rgba(37, 99, 235, 0.25);

      i {
        font-size: 3rem;
        color: white;
      }
    }

    .visual-orbit {
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: 180px;
      height: 180px;
      border: 1.5px dashed var(--color-primary-200);
      border-radius: 50%;
      animation: orbit 20s linear infinite;
    }

    .visual-orbit-2 {
      width: 240px;
      height: 240px;
      border-color: var(--color-primary-100);
      animation-duration: 28s;
      animation-direction: reverse;
    }

    @keyframes orbit {
      from { transform: translate(-50%, -50%) rotate(0deg); }
      to { transform: translate(-50%, -50%) rotate(360deg); }
    }

    @media (prefers-reduced-motion: reduce) {
      .visual-orbit { animation: none; }
    }

    .accent-teal .visual-icon { background: linear-gradient(135deg, #14b8a6, #0d9488); box-shadow: 0 16px 40px rgba(20, 184, 166, 0.25); }
    .accent-teal .panel-eyebrow { color: #0d9488; }
    .accent-teal .panel-highlight { background: #ccfbf1; color: #115e59; border-color: #99f6e4; i { color: #0d9488; } }

    .accent-purple .visual-icon { background: linear-gradient(135deg, #8b5cf6, #6d28d9); box-shadow: 0 16px 40px rgba(139, 92, 246, 0.25); }
    .accent-purple .panel-eyebrow { color: #6d28d9; }
    .accent-purple .panel-highlight { background: #ede9fe; color: #5b21b6; border-color: #ddd6fe; i { color: #7c3aed; } }

    .accent-amber .visual-icon { background: linear-gradient(135deg, #f59e0b, #d97706); box-shadow: 0 16px 40px rgba(245, 158, 11, 0.25); }
    .accent-amber .panel-eyebrow { color: #b45309; }
    .accent-amber .panel-highlight { background: #fef3c7; color: #92400e; border-color: #fde68a; i { color: #d97706; } }

    .accent-pink .visual-icon { background: linear-gradient(135deg, #ec4899, #be185d); box-shadow: 0 16px 40px rgba(236, 72, 153, 0.25); }
    .accent-pink .panel-eyebrow { color: #be185d; }
    .accent-pink .panel-highlight { background: #fce7f3; color: #9d174d; border-color: #fbcfe8; i { color: #db2777; } }

    .accent-green .visual-icon { background: linear-gradient(135deg, #10b981, #047857); box-shadow: 0 16px 40px rgba(16, 185, 129, 0.25); }
    .accent-green .panel-eyebrow { color: #047857; }
    .accent-green .panel-highlight { background: #d1fae5; color: #065f46; border-color: #a7f3d0; i { color: #059669; } }

    .rotation-status {
      display: flex;
      justify-content: center;
      gap: var(--spacing-2);
    }

    .rotation-dot {
      width: 24px;
      height: 4px;
      border-radius: 2px;
      background: var(--color-neutral-300);
      border: none;
      cursor: pointer;
      padding: 0;
      transition: all var(--transition-normal);

      &:hover {
        background: var(--color-neutral-400);
      }

      &.active {
        width: 40px;
        background: var(--color-primary-600);
      }

      &:focus-visible {
        outline: 3px solid var(--color-primary-300);
        outline-offset: 2px;
      }
    }

    @media (max-width: 1024px) {
      .showcase-layout {
        grid-template-columns: 1fr;
      }

      .tabs-list {
        flex-direction: row;
        overflow-x: auto;
        scroll-snap-type: x mandatory;
        gap: var(--spacing-2);
      }

      .tab-button {
        flex-shrink: 0;
        scroll-snap-align: start;
        min-width: 200px;
      }

      .tab-arrow {
        display: none;
      }

      .panel-visual {
        position: relative;
        top: auto;
        right: auto;
        transform: none;
        margin: var(--spacing-6) auto 0;
      }
    }

    @media (max-width: 768px) {
      .modules-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .tab-panel {
        padding: var(--spacing-6);
        min-height: auto;
      }

      .panel-features {
        grid-template-columns: 1fr;
      }

      .panel-visual {
        width: 160px;
        height: 160px;
      }

      .visual-icon {
        width: 80px;
        height: 80px;

        i {
          font-size: 2.25rem;
        }
      }
    }
  `]
})
export class ModuleShowcaseSectionComponent implements OnInit, OnDestroy {
  readonly modules: ModuleTab[] = [
    {
      id: 'invoices',
      icon: 'pi-file-edit',
      title: 'Factures & Devis',
      tagline: 'CŒUR MÉTIER',
      description: 'Créez des factures et devis conformes en quelques clics. Wizard 6 étapes, conversion devis→facture, signature électronique, envoi multi-canal.',
      features: [
        'Wizard guidé en 6 étapes',
        'Conversion devis → facture en 1 clic',
        'Numérotation automatique conforme',
        'Signature électronique légale',
        'Export PDF professionnel',
        'Envoi WhatsApp / Email / Telegram'
      ],
      highlight: 'Une facture créée en moins de 2 minutes',
      accent: 'blue'
    },
    {
      id: 'pos',
      icon: 'pi-shopping-cart',
      title: 'POS / Caisse',
      tagline: 'POINT DE VENTE',
      description: 'Encaissez rapidement avec une caisse moderne tactile. Sessions, multi-moyens de paiement, suivi des fonds en temps réel.',
      features: [
        'Interface tactile moderne',
        'Sessions de caisse avec ouverture/clôture',
        'Multi-moyens : espèces, CB, virement, chèque',
        'Auto-linking facture ↔ caisse',
        'Suivi des fonds en temps réel',
        'Tickets clients personnalisables'
      ],
      highlight: 'Conçu pour les boutiques et restaurants',
      accent: 'amber'
    },
    {
      id: 'stock',
      icon: 'pi-box',
      title: 'Stock & Inventaire',
      tagline: 'GESTION D\'ENTREPÔT',
      description: 'Gérez plusieurs entrepôts depuis une seule interface. Mouvements en temps réel, comptages physiques, alertes intelligentes.',
      features: [
        'Multi-entrepôts et magasins',
        'Mouvements de stock temps réel',
        'Transferts inter-entrepôts',
        'Comptages physiques avec variance',
        'Alertes de stock bas',
        'Valuation FIFO automatique'
      ],
      highlight: 'Visibilité complète sur vos stocks',
      accent: 'teal'
    },
    {
      id: 'purchasing',
      icon: 'pi-shopping-bag',
      title: 'Achats & Fournisseurs',
      tagline: 'APPROVISIONNEMENT',
      description: 'Pilotez vos achats : bons de commande, factures fournisseurs, retenues à la source TEJ. Workflow complet de la commande à la réception.',
      features: [
        'Bons de commande workflow complet',
        'Factures fournisseurs auto-linkées',
        'Retenues à la source TEJ',
        'Certificats RS automatiques',
        'Suivi des dettes fournisseurs',
        'Lettrage comptable automatique'
      ],
      highlight: 'Conformité TEJ Tunisie intégrée',
      accent: 'purple'
    },
    {
      id: 'banking',
      icon: 'pi-credit-card',
      title: 'Banking & Trésorerie',
      tagline: 'TRÉSORERIE',
      description: 'Connectez vos 10+ banques tunisiennes. Versements, dépôts, réconciliation automatique. Maîtrisez votre cash flow.',
      features: [
        'Comptes multi-devises',
        'Dépôts et versements bancaires',
        'Réconciliation automatique',
        'Suivi du cash flow temps réel',
        'Lettrage des écritures bancaires'
      ],
      highlight: 'BIAT, STB, Attijari, BH, Amen et plus',
      accent: 'blue'
    },
    {
      id: 'accounting',
      icon: 'pi-calculator',
      title: 'Comptabilité',
      tagline: 'COMPTA & FISCAL',
      description: 'Plan comptable, journaux, grand livre, lettrage. Audit chain immuable.',
      features: [
        'Plan comptable configurable',
        'Journaux (ventes, achats, caisse, banque)',
        'Grand livre et balance',
        'Saisie manuelle d\'écritures',
        'Lettrage des dettes'
      ],
      highlight: 'Compatible expert-comptable tunisien',
      accent: 'pink'
    },
    {
      id: 'crm',
      icon: 'pi-users',
      title: 'CRM Commercial',
      tagline: 'PIPELINE DE VENTES',
      description: 'Suivez vos opportunités, activités et objectifs commerciaux. Top clients, top produits, rapports équipe en un coup d\'œil.',
      features: [
        'Pipeline d\'opportunités',
        'Activités commerciales (calls, RDV)',
        'Sales targets par commercial',
        'Top clients et top produits',
        'Rapports équipe automatisés',
        'Liaison avec devis/factures'
      ],
      highlight: 'Augmentez vos taux de conversion',
      accent: 'green'
    },
    {
      id: 'ai',
      icon: 'pi-bolt',
      title: 'IA Assistant',
      tagline: 'INTELLIGENCE INTÉGRÉE',
      description: 'Chat conversationnel, OCR de documents, recommandations intelligentes. Tout est conçu pour protéger vos données : IA local, pas de cloud, confidentialité maximale.',
      features: [
        'Chat IA contextuel sur vos données',
        'OCR factures fournisseurs (PDF → données)',
        'Recommandations clients/produits',
        'IA local',
        'Données 100% privées',
        'Suggestions automatiques formulaires'
      ],
      highlight: 'Les seules questions, des réponses immédiates',
      accent: 'purple'
    },
    {
      id: 'forecasting',
      icon: 'pi-chart-line',
      title: 'Forecasting & Prévisions',
      tagline: 'SCIENCE DES DONNÉES',
      description: 'Prévisions de ventes, classification ABC/XYZ, recommandations d\'achat. Calendrier commercial tunisien intégré (Ramadan, Aïd…).',
      features: [
        'Prévisions de ventes (statistique + ML)',
        'Classification ABC/XYZ produits ',
        'Recommandations replenishment',
        'Détection des fenêtres promotionnelles',
        'Calendrier commercial tunisien intégré',
        'Saisonnalité Ramadan, Aïd, fêtes'
      ],
      highlight: 'Anticipez Ramadan et l\'Aïd automatiquement',
      accent: 'amber'
    },
    {
      id: 'reporting',
      icon: 'pi-chart-bar',
      title: 'Reporting & Analytics',
      tagline: 'TABLEAUX DE BORD',
      description: 'Tableaux de bord, exports PDF/Excel, KPIs en temps réel. Comprenez votre activité d\'un coup d\'œil.',
      features: [
        'Dashboard avec KPIs en temps réel',
        'Rapports ventes, achats, stock, trésorerie',
        'Top clients, top produits, top vendeurs',
        'Évolution du CA',
        'Export PDF / Excel',
        'Filtres par période, entrepôt, équipe'
      ],
      highlight: 'Toutes vos données, prêtes à analyser',
      accent: 'teal'
    }
  ];

  readonly activeIndex = signal(0);
  readonly activeTab = computed(() => this.modules[this.activeIndex()]);

  private rotationTimer?: ReturnType<typeof setInterval>;
  private isPaused = false;

  ngOnInit(): void {
    if (typeof window === 'undefined') return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    this.startRotation();
  }

  ngOnDestroy(): void {
    this.stopRotation();
  }

  selectTab(index: number): void {
    this.activeIndex.set(index);
    // Restart rotation when user manually picks
    this.stopRotation();
    if (typeof window !== 'undefined' && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      this.startRotation();
    }
  }

  pauseRotation(): void {
    this.isPaused = true;
  }

  resumeRotation(): void {
    this.isPaused = false;
  }

  onKeydown(event: KeyboardEvent, index: number): void {
    let newIndex = index;
    if (event.key === 'ArrowDown' || event.key === 'ArrowRight') {
      event.preventDefault();
      newIndex = (index + 1) % this.modules.length;
    } else if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') {
      event.preventDefault();
      newIndex = (index - 1 + this.modules.length) % this.modules.length;
    } else if (event.key === 'Home') {
      event.preventDefault();
      newIndex = 0;
    } else if (event.key === 'End') {
      event.preventDefault();
      newIndex = this.modules.length - 1;
    } else {
      return;
    }
    this.selectTab(newIndex);
    const tabId = `tab-${this.modules[newIndex].id}`;
    requestAnimationFrame(() => {
      document.getElementById(tabId)?.focus();
    });
  }

  private startRotation(): void {
    this.rotationTimer = setInterval(() => {
      if (this.isPaused) return;
      const next = (this.activeIndex() + 1) % this.modules.length;
      this.activeIndex.set(next);
    }, 5000);
  }

  private stopRotation(): void {
    if (this.rotationTimer) {
      clearInterval(this.rotationTimer);
      this.rotationTimer = undefined;
    }
  }
}
