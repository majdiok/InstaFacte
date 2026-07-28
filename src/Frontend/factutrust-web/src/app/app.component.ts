import { Component, OnInit, OnDestroy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';
import { ToastComponent } from '@shared/components/toast/toast.component';

/**
 * Feature flag CSS Superieur Admin.
 * Classe `theme-superieur` sur <html> — rollback = retirer la classe.
 * Activé par défaut après QA Phase 7 (voir docs/design/superieur-baseline/).
 */
export const SUPERIEUR_THEME_ENABLED = true;

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NgbModalModule, ToastComponent],
  template: `
    <router-outlet></router-outlet>
    <app-toast-container></app-toast-container>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
    }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'InstaFact';

  ngOnInit(): void {
    if (SUPERIEUR_THEME_ENABLED) {
      document.documentElement.classList.add('theme-superieur');
    } else {
      document.documentElement.classList.remove('theme-superieur');
    }
  }

  ngOnDestroy(): void {
    document.documentElement.classList.remove('theme-superieur');
  }
}
