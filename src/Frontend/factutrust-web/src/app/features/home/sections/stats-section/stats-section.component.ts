import { Component, OnInit, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-stats-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="stats-section" aria-labelledby="stats-title">
      <div class="stats-container" #statsContainer>
        <div class="stats-header">
          <h2 id="stats-title" class="stats-title">Une plateforme complète, sans compromis</h2>
          <p class="stats-subtitle">Tout ce qu'il faut pour gérer votre activité, regroupé dans une seule solution.</p>
        </div>

        <div class="stats-grid">
          <div class="stat-item" role="group" aria-label="150+ Fonctionnalités">
            <div class="stat-number" data-count="150" data-suffix="+">0</div>
            <div class="stat-label">Fonctionnalités intégrées</div>
          </div>
          <div class="stat-item" role="group" aria-label="28 Modules">
            <div class="stat-number" data-count="28">0</div>
            <div class="stat-label">Modules métier</div>
          </div>
          <div class="stat-item" role="group" aria-label="10 Rôles">
            <div class="stat-number" data-count="10">0</div>
            <div class="stat-label">Rôles utilisateurs prédéfinis</div>
          </div>
          <div class="stat-item" role="group" aria-label="3 Canaux">
            <div class="stat-number" data-count="3">0</div>
            <div class="stat-label">Canaux de communication</div>
          </div>
        </div>

        <div class="capabilities-line">
          <span>Multi-tenant</span>
          <span class="dot">·</span>
          <span>Conformité TEJ</span>
          <span class="dot">·</span>
          <span>IA intégrée</span>
          <span class="dot">·</span>
          <span>Visite virtuelle 3D</span>
          <span class="dot">·</span>
          <span>Forecasting ML</span>
          <span class="dot">·</span>
          <span>POS</span>
          <span class="dot">·</span>
          <span>CRM</span>
          <span class="dot">·</span>
          <span>Comptabilité</span>
          <span class="dot">·</span>
          <span>Banking</span>
          <span class="dot">·</span>
          <span>Stock multi-entrepôts</span>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .stats-section {
      padding: var(--spacing-20) var(--spacing-6);
      background: linear-gradient(135deg, var(--color-primary-700) 0%, var(--color-primary-800) 100%);
      color: white;
      position: relative;
      overflow: hidden;
    }

    .stats-section::before {
      content: '';
      position: absolute;
      top: -50%;
      right: -10%;
      width: 600px;
      height: 600px;
      background: radial-gradient(circle, rgba(255, 255, 255, 0.08) 0%, transparent 70%);
      border-radius: 50%;
      pointer-events: none;
    }

    .stats-section::after {
      content: '';
      position: absolute;
      bottom: -50%;
      left: -10%;
      width: 500px;
      height: 500px;
      background: radial-gradient(circle, rgba(255, 255, 255, 0.06) 0%, transparent 70%);
      border-radius: 50%;
      pointer-events: none;
    }

    .stats-container {
      max-width: 1280px;
      margin: 0 auto;
      position: relative;
      z-index: 1;
    }

    .stats-header {
      text-align: center;
      margin-bottom: var(--spacing-12);
      max-width: 720px;
      margin-left: auto;
      margin-right: auto;
    }

    .stats-title {
      font-size: clamp(1.875rem, 4vw, 2.5rem);
      font-weight: var(--font-weight-bold);
      color: white;
      line-height: var(--line-height-tight);
      margin: 0 0 var(--spacing-3) 0;
    }

    .stats-subtitle {
      font-size: var(--font-size-lg);
      color: rgba(255, 255, 255, 0.85);
      margin: 0;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: var(--spacing-8);
      text-align: center;
      margin-bottom: var(--spacing-12);
    }

    .stat-item {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      padding: var(--spacing-4);
    }

    .stat-number {
      font-size: clamp(2.5rem, 5vw, 4rem);
      font-weight: var(--font-weight-bold);
      line-height: 1;
      color: white;
      background: linear-gradient(135deg, #ffffff 0%, #cbd5e1 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .stat-label {
      font-size: var(--font-size-base);
      color: rgba(255, 255, 255, 0.9);
      font-weight: var(--font-weight-medium);
    }

    .capabilities-line {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-2);
      max-width: 960px;
      margin: 0 auto;
      padding: var(--spacing-6) var(--spacing-4);
      border-top: 1px solid rgba(255, 255, 255, 0.15);
      color: rgba(255, 255, 255, 0.85);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      text-align: center;
    }

    .capabilities-line .dot {
      color: rgba(255, 255, 255, 0.4);
    }

    @media (max-width: 1024px) {
      .stats-grid {
        grid-template-columns: repeat(2, 1fr);
        gap: var(--spacing-12);
      }
    }

    @media (max-width: 768px) {
      .stats-section {
        padding: var(--spacing-12) var(--spacing-4);
      }

      .stats-grid {
        grid-template-columns: 1fr;
        gap: var(--spacing-8);
      }
    }
  `]
})
export class StatsSectionComponent implements OnInit, AfterViewInit {
  @ViewChild('statsContainer') statsContainer?: ElementRef<HTMLElement>;
  private countersAnimated = false;

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    if (typeof window === 'undefined') return;
    this.setupCounterAnimation();
  }

  private setupCounterAnimation(): void {
    if (!this.statsContainer) return;
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && !this.countersAnimated) {
          this.animateCounters();
          this.countersAnimated = true;
          observer.disconnect();
        }
      });
    }, { threshold: 0.4 });
    observer.observe(this.statsContainer.nativeElement);
  }

  private animateCounters(): void {
    const counters = this.statsContainer?.nativeElement.querySelectorAll<HTMLElement>('.stat-number');
    if (!counters) return;

    counters.forEach(counter => {
      const target = parseInt(counter.getAttribute('data-count') || '0', 10);
      const suffix = counter.getAttribute('data-suffix') || '';
      const duration = 1800;
      const step = target / (duration / 16);
      let current = 0;

      const updateCounter = () => {
        current += step;
        if (current < target) {
          counter.textContent = Math.floor(current).toString();
          requestAnimationFrame(updateCounter);
        } else {
          counter.textContent = target.toString() + suffix;
        }
      };

      updateCounter();
    });
  }
}
