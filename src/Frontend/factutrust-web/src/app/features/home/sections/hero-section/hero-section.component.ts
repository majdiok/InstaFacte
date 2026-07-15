import { Component, OnInit, AfterViewInit, ViewChild, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-hero-section',
  standalone: true,
  imports: [CommonModule, RouterModule, ButtonModule],
  template: `
    <section class="hero-section" id="top">
      <div class="hero-bg-blob blob-1" aria-hidden="true"></div>
      <div class="hero-bg-blob blob-2" aria-hidden="true"></div>
      <div class="hero-bg-blob blob-3" aria-hidden="true"></div>
      <div class="hero-container">
        <div class="hero-content">
          <span class="hero-eyebrow">SUITE TOUT-EN-UN POUR PME TUNISIENNES</span>
          <h1 class="hero-title">
            La <span class="gradient-text">suite tout-en-un</span><br>
            pour facturer, gérer<br>
            et <span class="gradient-text-secondary">prévoir</span> votre activité
          </h1>

          <p class="hero-subtitle">
            Factures conformes TEJ, IA intégrée, prévisions intelligentes.
            Pour les entreprises tunisiennes qui veulent gagner du temps et de la précision.
          </p>

          <div class="hero-cta">
            <a routerLink="/auth/register" class="btn-hero-primary">
              <i class="pi pi-rocket" aria-hidden="true"></i>
              Démarrer gratuitement — 10 factures/mois
            </a>
            <a href="#modules" class="btn-hero-secondary">
              <i class="pi pi-play" aria-hidden="true"></i>
              Voir la démo en 2 min
            </a>
          </div>

          <div class="hero-trust">
            <span><i class="pi pi-check" aria-hidden="true"></i> Aucune CB requise</span>
            <span><i class="pi pi-check" aria-hidden="true"></i> Configuration en 5 min</span>
            <span><i class="pi pi-check" aria-hidden="true"></i> Conforme TEJ Tunisie</span>
          </div>

          <div class="hero-stats">
            <div class="stat">
              <div class="stat-number" data-count="150" data-suffix="+">0</div>
              <div class="stat-label">Fonctionnalités intégrées</div>
            </div>
            <div class="stat">
              <div class="stat-number" data-count="28">0</div>
              <div class="stat-label">Modules métier</div>
            </div>
            <div class="stat">
              <div class="stat-number" data-count="10">0</div>
              <div class="stat-label">Rôles utilisateurs</div>
            </div>
          </div>
        </div>

        <div class="hero-visual">
          <div class="invoice-card" #invoiceCard>
            <div class="invoice-header">
              <div class="invoice-logo">📄</div>
              <div class="invoice-number">
                <span>Facture</span>
                <strong>#FAC-2026-00142</strong>
              </div>
            </div>
            <div class="invoice-details">
              <div class="detail-group">
                <label>Client</label>
                <p>Tech Solutions SAS</p>
              </div>
              <div class="detail-group">
                <label>Date</label>
                <p>25 Janvier 2026</p>
              </div>
              <div class="detail-group">
                <label>Échéance</label>
                <p>25 Février 2026</p>
              </div>
              <div class="detail-group">
                <label>Statut</label>
                <p style="color: #10b981;">✓ Payée</p>
              </div>
            </div>
            <div class="invoice-items">
              <div class="invoice-item">
                <span class="item-name">Développement Web</span>
                <span class="item-price">2 400 €</span>
              </div>
              <div class="invoice-item">
                <span class="item-name">Design UI/UX</span>
                <span class="item-price">1 200 €</span>
              </div>
              <div class="invoice-item">
                <span class="item-name">Maintenance</span>
                <span class="item-price">450 €</span>
              </div>
            </div>
            <div class="invoice-total">
              <span class="total-label">Total TTC</span>
              <span class="total-amount">4 860 €</span>
            </div>
          </div>

          <!-- Floating Elements -->
          <div class="floating-element float-1">
            <div class="icon">✓</div>
            <div class="text">
              <span class="label">Paiement reçu</span>
              <span class="value">+2 340 €</span>
            </div>
          </div>
          <div class="floating-element float-2">
            <div class="icon">📧</div>
            <div class="text">
              <span class="label">Facture envoyée</span>
              <span class="value">Client notifié</span>
            </div>
          </div>
          <div class="floating-element float-3">
            <div class="icon">📊</div>
            <div class="text">
              <span class="label">CA du mois</span>
              <span class="value">+24.5%</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .hero-section {
      padding: 8rem var(--spacing-6) 4rem;
      position: relative;
      overflow: hidden;
      min-height: 100vh;
      display: flex;
      align-items: center;
      background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 50%, #0f172a 100%);
      isolation: isolate;
    }

    .hero-bg-blob {
      position: absolute;
      border-radius: 50%;
      filter: blur(80px);
      opacity: 0.35;
      pointer-events: none;
      z-index: 0;
    }

    .blob-1 {
      width: 500px;
      height: 500px;
      background: radial-gradient(circle, #6366f1 0%, transparent 70%);
      top: -150px;
      right: -100px;
      animation: floatBlob 14s ease-in-out infinite;
    }

    .blob-2 {
      width: 400px;
      height: 400px;
      background: radial-gradient(circle, #06b6d4 0%, transparent 70%);
      bottom: -100px;
      left: -100px;
      animation: floatBlob 18s ease-in-out infinite reverse;
    }

    .blob-3 {
      width: 300px;
      height: 300px;
      background: radial-gradient(circle, #14b8a6 0%, transparent 70%);
      top: 40%;
      left: 30%;
      animation: floatBlob 22s ease-in-out infinite;
      opacity: 0.2;
    }

    @keyframes floatBlob {
      0%, 100% { transform: translate(0, 0) scale(1); }
      33% { transform: translate(30px, -40px) scale(1.05); }
      66% { transform: translate(-30px, 30px) scale(0.95); }
    }

    @media (prefers-reduced-motion: reduce) {
      .hero-bg-blob {
        animation: none;
      }
    }

    .hero-eyebrow {
      display: inline-block;
      padding: var(--spacing-2) var(--spacing-4);
      background: rgba(99, 102, 241, 0.15);
      color: #a5b4fc;
      border: 1px solid rgba(99, 102, 241, 0.3);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      letter-spacing: 0.08em;
      width: fit-content;
      margin-bottom: var(--spacing-2);
    }

    .gradient-text-secondary {
      background: linear-gradient(135deg, #14b8a6 0%, #06b6d4 50%, #6366f1 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .hero-trust {
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-5);
      padding-top: var(--spacing-2);
      font-size: var(--font-size-sm);
      color: #cbd5e1;

      span {
        display: flex;
        align-items: center;
        gap: var(--spacing-2);

        i {
          color: #14b8a6;
          font-size: 0.875rem;
        }
      }
    }

    .hero-container {
      max-width: 1280px;
      margin: 0 auto;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--spacing-16);
      align-items: center;
      position: relative;
      z-index: 1;
    }

    .hero-content {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-6);
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

    .hero-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2);
      padding: var(--spacing-2) var(--spacing-4);
      background: var(--color-success-50);
      color: var(--color-success-700);
      border-radius: var(--radius-full);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      width: fit-content;

      i {
        font-size: 1rem;
      }
    }

    .hero-title {
      font-size: clamp(2.5rem, 5vw, 4rem);
      font-weight: 800;
      line-height: 1.1;
      margin-bottom: 1.5rem;
      color: #f8fafc;
    }

    .gradient-text {
      background: linear-gradient(135deg, #6366f1, #06b6d4, #f59e0b);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .hero-subtitle {
      font-size: 1.25rem;
      color: #94a3b8;
      margin-bottom: 2rem;
      line-height: 1.7;
    }

    .hero-visual {
      position: relative;
      display: flex;
      justify-content: center;
      align-items: center;
    }

    .invoice-card {
      background: rgba(255, 255, 255, 0.05);
      backdrop-filter: blur(20px);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 24px;
      padding: 2rem;
      width: 100%;
      max-width: 420px;
      transform: perspective(1000px) rotateY(-5deg) rotateX(5deg);
      transition: transform 0.5s ease;
      box-shadow: 
        0 25px 50px rgba(0, 0, 0, 0.3),
        0 0 100px rgba(99, 102, 241, 0.1);
    }

    .invoice-card:hover {
      transform: perspective(1000px) rotateY(0deg) rotateX(0deg);
    }

    .invoice-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
      padding-bottom: 1.5rem;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    }

    .invoice-logo {
      font-size: 2rem;
    }

    .invoice-number {
      text-align: right;
    }

    .invoice-number span {
      display: block;
      color: #64748b;
      font-size: 0.8rem;
    }

    .invoice-number strong {
      color: #6366f1;
    }

    .invoice-details {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .detail-group label {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-bottom: 0.25rem;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .detail-group p {
      color: #f8fafc;
      font-weight: 500;
    }

    .invoice-items {
      background: rgba(0, 0, 0, 0.2);
      border-radius: 12px;
      padding: 1rem;
      margin-bottom: 1.5rem;
    }

    .invoice-item {
      display: flex;
      justify-content: space-between;
      padding: 0.75rem 0;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }

    .invoice-item:last-child {
      border-bottom: none;
    }

    .item-name {
      color: #94a3b8;
    }

    .item-price {
      font-weight: 600;
      color: #f8fafc;
    }

    .invoice-total {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 1rem;
      border-top: 2px solid rgba(99, 102, 241, 0.3);
    }

    .total-label {
      font-size: 1.1rem;
      color: #94a3b8;
    }

    .total-amount {
      font-size: 1.75rem;
      font-weight: 800;
      background: linear-gradient(135deg, #6366f1, #06b6d4);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    /* Floating Elements */
    .floating-element {
      position: absolute;
      background: rgba(255, 255, 255, 0.05);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 16px;
      padding: 1rem 1.25rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      animation: floatElement 6s ease-in-out infinite;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
    }

    .floating-element .icon {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
    }

    .float-1 {
      top: 10%;
      right: -10%;
      animation-delay: 0s;
    }

    .float-1 .icon {
      background: linear-gradient(135deg, #10b981, #059669);
    }

    .float-2 {
      bottom: 20%;
      left: -15%;
      animation-delay: -2s;
    }

    .float-2 .icon {
      background: linear-gradient(135deg, #6366f1, #4f46e5);
    }

    .float-3 {
      top: 40%;
      right: -20%;
      animation-delay: -4s;
    }

    .float-3 .icon {
      background: linear-gradient(135deg, #f59e0b, #d97706);
    }

    @keyframes floatElement {
      0%, 100% { transform: translateY(0); }
      50% { transform: translateY(-15px); }
    }

    .floating-element .text {
      display: flex;
      flex-direction: column;
    }

    .floating-element .label {
      font-size: 0.7rem;
      color: #64748b;
    }

    .floating-element .value {
      font-weight: 700;
      font-size: 0.95rem;
      color: #f8fafc;
    }

    .hero-cta {
      display: flex;
      gap: var(--spacing-4);
      flex-wrap: wrap;
    }

    .btn-hero-primary {
      padding: 1rem 2.5rem;
      border-radius: 50px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s ease;
      border: none;
      font-size: 1.1rem;
      background: linear-gradient(135deg, #6366f1, #06b6d4);
      color: white;
      box-shadow: 0 4px 15px rgba(99, 102, 241, 0.4);
      text-decoration: none;
      display: inline-block;

      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 6px 25px rgba(99, 102, 241, 0.6);
      }
    }

    .btn-hero-secondary {
      padding: 1rem 2.5rem;
      border-radius: 50px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s ease;
      border: 1px solid rgba(255, 255, 255, 0.2);
      font-size: 1.1rem;
      background: transparent;
      color: #f8fafc;
      text-decoration: none;
      display: inline-block;

      &:hover {
        background: rgba(255, 255, 255, 0.1);
        border-color: rgba(255, 255, 255, 0.4);
      }
    }

    .hero-stats {
      display: flex;
      gap: 3rem;
      margin-top: 3rem;
      padding-top: 2rem;
      border-top: 1px solid rgba(255, 255, 255, 0.1);
    }

    .stat {
      text-align: center;
    }

    .stat-number {
      font-size: 2.5rem;
      font-weight: 800;
      background: linear-gradient(135deg, #6366f1, #06b6d4);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .stat-label {
      color: #64748b;
      font-size: 0.9rem;
      margin-top: 0.25rem;
    }



    .animate-on-scroll {
      opacity: 0;
      transform: translateY(30px);
      transition: all 0.6s ease;
    }

    .animate-on-scroll.visible {
      opacity: 1;
      transform: translateY(0);
    }

    @media (max-width: 1024px) {
      .hero-container {
        grid-template-columns: 1fr;
        text-align: center;
      }

      .hero-buttons {
        justify-content: center;
      }

      .hero-stats {
        justify-content: center;
      }

      .hero-visual {
        order: -1;
        margin-bottom: 2rem;
      }

      .invoice-card {
        transform: none;
        max-width: 380px;
      }

      .floating-element {
        display: none;
      }
    }

    @media (max-width: 768px) {
      .hero-section {
        padding: var(--spacing-12) var(--spacing-4) var(--spacing-10);
      }

      .hero-stats {
        flex-direction: column;
        gap: 1.5rem;
      }

      .hero-cta {
        flex-direction: column;
        width: 100%;

        .btn-hero-primary,
        .btn-hero-secondary {
          width: 100%;
          justify-content: center;
        }
      }
    }
  `]
})
export class HeroSectionComponent implements OnInit, AfterViewInit {
  @ViewChild('invoiceCard') invoiceCard?: ElementRef<HTMLElement>;
  private countersAnimated = false;

  ngOnInit() {
    // Scroll animations
    this.setupScrollAnimations();
  }

  ngAfterViewInit() {
    // Counter animation
    this.setupCounterAnimation();
    // Invoice card tilt effect
    this.setupInvoiceCardTilt();
  }

  private setupScrollAnimations() {
    const observerOptions = {
      threshold: 0.1,
      rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.classList.add('visible');
        }
      });
    }, observerOptions);

    setTimeout(() => {
      document.querySelectorAll('.animate-on-scroll').forEach(el => {
        observer.observe(el);
      });
    }, 100);
  }

  private setupCounterAnimation() {
    const heroObserver = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && !this.countersAnimated) {
          this.animateCounters();
          this.countersAnimated = true;
          heroObserver.disconnect();
        }
      });
    }, { threshold: 0.5 });

    setTimeout(() => {
      const statsElement = document.querySelector('.hero-stats');
      if (statsElement) {
        heroObserver.observe(statsElement);
      }
    }, 100);
  }

  private animateCounters() {
    const counters = document.querySelectorAll<HTMLElement>('.hero-stats .stat-number');
    counters.forEach(counter => {
      const target = parseInt(counter.getAttribute('data-count') || '0', 10);
      const suffix = counter.getAttribute('data-suffix') || '';
      const duration = 2000;
      const step = target / (duration / 16);
      let current = 0;

      const updateCounter = () => {
        current += step;
        if (current < target) {
          counter.textContent = Math.floor(current).toLocaleString();
          requestAnimationFrame(updateCounter);
        } else {
          counter.textContent = target.toLocaleString() + suffix;
        }
      };

      updateCounter();
    });
  }

  private setupInvoiceCardTilt() {
    if (!this.invoiceCard || typeof window === 'undefined') return;
    
    const card = this.invoiceCard.nativeElement;
    
    if (window.innerWidth <= 1024) return;

    card.addEventListener('mousemove', (e: MouseEvent) => {
      const rect = card.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const y = e.clientY - rect.top;
      const centerX = rect.width / 2;
      const centerY = rect.height / 2;
      const rotateX = (y - centerY) / 20;
      const rotateY = (centerX - x) / 20;

      card.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
    });

    card.addEventListener('mouseleave', () => {
      card.style.transform = 'perspective(1000px) rotateY(-5deg) rotateX(5deg)';
    });
  }
}
