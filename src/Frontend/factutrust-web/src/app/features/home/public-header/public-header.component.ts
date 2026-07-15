import { Component, HostListener, signal, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { Subject, takeUntil } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BRAND } from '@core/constants/brand';

@Component({
  selector: 'app-public-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <header
      class="header-area header-sticky"
      [class.background-header]="isScrolled()"
      role="banner">
      <div class="container">
        <div class="row">
          <div class="col-12">
            <nav class="main-nav" aria-label="Navigation principale">
              <a routerLink="/" class="logo" [attr.aria-current]="isHomePage() ? 'page' : null">
                <img [src]="brand.logoLockup" [alt]="brand.name + ' — ' + brand.taglineLong">
              </a>
              <ul class="nav">
                <li class="scroll-to-section"><a href="#top" (click)="scrollToSection($event, 'top')" [class.active]="isHomePage()">Accueil</a></li>
                <li class="scroll-to-section"><a href="#modules" (click)="scrollToSection($event, 'modules')">Fonctionnalités</a></li>
                <li class="scroll-to-section"><a href="#tarifs" (click)="scrollToSection($event, 'tarifs')">Tarifs</a></li>
                <li class="scroll-to-section"><a href="#about" (click)="scrollToSection($event, 'about')">À propos</a></li>
                <li class="scroll-to-section"><a href="#securite" (click)="scrollToSection($event, 'securite')">Sécurité</a></li>
                @if (storefrontEnabled) {
                  <li>
                    <a routerLink="/visite-virtuelle" class="nav-street-link">Visite virtuelle</a>
                  </li>
                }
                <li><div class="gradient-button"><a routerLink="/auth/login" aria-label="Se connecter"><i class="fa fa-sign-in-alt"></i> Se connecter</a></div></li>
              </ul>
              <a class="menu-trigger" (click)="toggleMobileMenu()" [attr.aria-expanded]="mobileMenuOpen()" aria-label="Menu" role="button">
                <span>Menu</span>
              </a>
            </nav>
          </div>
        </div>
      </div>

      <!-- Mobile menu (Angular-driven, Chain-compatible) -->
      <nav
        id="mobile-menu"
        class="mobile-menu chain-mobile-nav"
        [class.open]="mobileMenuOpen()"
        role="navigation"
        aria-label="Navigation mobile">
        <div class="mobile-menu-overlay" (click)="closeMobileMenu()"></div>
        <div class="mobile-menu-content">
          <a routerLink="/" class="mobile-nav-link" (click)="closeMobileMenu()" [attr.aria-current]="isHomePage() ? 'page' : null">Accueil</a>
          <a href="#modules" class="mobile-nav-link" (click)="scrollToSection($event, 'modules'); closeMobileMenu()">Fonctionnalités</a>
          <a href="#tarifs" class="mobile-nav-link" (click)="scrollToSection($event, 'tarifs'); closeMobileMenu()">Tarifs</a>
          <a href="#about" class="mobile-nav-link" (click)="scrollToSection($event, 'about'); closeMobileMenu()">À propos</a>
          <a href="#securite" class="mobile-nav-link" (click)="scrollToSection($event, 'securite'); closeMobileMenu()">Sécurité</a>
          @if (storefrontEnabled) {
            <a routerLink="/visite-virtuelle" class="mobile-nav-link" (click)="closeMobileMenu()">Visite virtuelle</a>
          }
          <a routerLink="/auth/login" class="mobile-nav-link" (click)="closeMobileMenu()">Se connecter</a>
          <a routerLink="/auth/register" class="mobile-nav-link primary" (click)="closeMobileMenu()">Créer un compte</a>
        </div>
      </nav>
    </header>
  `,
  styles: [`
    /* Header logo: transparent background (no inherited or hover/focus background) */
    .main-nav .logo {
      background: transparent;
      background-color: transparent;
    }
    .main-nav .logo:hover,
    .main-nav .logo:focus,
    .main-nav .logo:focus-visible {
      background: transparent;
      background-color: transparent;
    }
    /* Header logo: horizontal lockup, preserve aspect ratio */
    .main-nav .logo img {
      background: transparent;
      background-color: transparent;
      max-height: 56px;
      width: auto;
      height: auto;
      object-fit: contain;
    }
    :host-context(.chain-theme-root) .header-area {
      height: 90px;
    }
    :host-context(.chain-theme-root) .header-area .main-nav {
      min-height: 70px;
    }
    :host-context(.chain-theme-root) .header-area .main-nav .logo {
      line-height: 90px;
      width: auto;
      max-width: 280px;
      overflow: visible;
    }
    :host-context(.chain-theme-root) .header-area.background-header .main-nav .logo {
      line-height: 70px;
    }
    :host-context(.chain-theme-root) .main-nav .logo img {
      max-height: 56px;
      width: auto;
      transform: none;
    }
    @media (max-width: 767px) {
      :host-context(.chain-theme-root) .main-nav .logo {
        max-width: calc(100vw - 100px);
      }
      :host-context(.chain-theme-root) .main-nav .logo img {
        max-height: 44px;
        transform: none;
      }
    }
    /* Mobile menu only – header styled by Chain theme */
    .chain-mobile-nav {
      display: none;
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      z-index: 9999;
      opacity: 0;
      visibility: hidden;
      transition: opacity 0.3s, visibility 0.3s;
    }
    .chain-mobile-nav.open {
      display: block;
      opacity: 1;
      visibility: visible;
    }
    .chain-mobile-nav .mobile-menu-overlay {
      position: absolute;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
    }
    .chain-mobile-nav .mobile-menu-content {
      position: absolute;
      top: 0;
      right: 0;
      bottom: 0;
      width: 280px;
      max-width: 100%;
      background: #fff;
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      transform: translateX(100%);
      transition: transform 0.3s;
      box-shadow: -4px 0 20px rgba(0,0,0,0.1);
    }
    .chain-mobile-nav.open .mobile-menu-content {
      transform: translateX(0);
    }
    .chain-mobile-nav .mobile-nav-link {
      display: block;
      padding: 0.75rem 1rem;
      color: #334155;
      text-decoration: none;
      border-radius: 0.375rem;
    }
    .chain-mobile-nav .mobile-nav-link:hover {
      background: #f1f5f9;
    }
    .chain-mobile-nav .mobile-nav-link.primary {
      background: #2563eb;
      color: #fff;
    }
    .chain-mobile-nav .mobile-nav-link.primary:hover {
      background: #1d4ed8;
    }
    @media (min-width: 992px) {
      .chain-mobile-nav { display: none !important; }
    }
    .main-nav .nav a.nav-street-link {
      color: #2563eb;
      font-weight: 600;
    }
  `]
})
export class PublicHeaderComponent implements OnInit, OnDestroy {
  readonly storefrontEnabled = environment.storefrontEnabled;
  readonly brand = BRAND;

  private router = inject(Router);
  private destroy$ = new Subject<void>();
  
  mobileMenuOpen = signal(false);
  isScrolled = signal(false);
  currentRoute = signal<string>('');

  ngOnInit() {
    // Track route changes
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe((event: NavigationEnd) => {
        this.currentRoute.set(event.url);
        this.closeMobileMenu();
      });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    document.body.style.overflow = '';
  }

  @HostListener('window:scroll', ['$event'])
  onScroll() {
    this.isScrolled.set(window.scrollY > 10);
  }

  toggleMobileMenu() {
    this.mobileMenuOpen.update(open => !open);
    // Prevent body scroll when menu is open
    if (this.mobileMenuOpen()) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
  }

  closeMobileMenu() {
    this.mobileMenuOpen.set(false);
    document.body.style.overflow = '';
  }

  isHomePage(): boolean {
    return this.currentRoute() === '/' || this.currentRoute() === '';
  }

  scrollToSection(event: Event, sectionId: string) {
    event.preventDefault();
    const element = document.getElementById(sectionId);
    if (element) {
      const headerHeight = 80;
      const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
      const offsetPosition = Math.max(0, elementPosition - headerHeight);
      window.scrollTo({ top: offsetPosition, behavior: 'smooth' });
    }
  }
}
