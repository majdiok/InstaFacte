import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StreetThemeService } from '../../services/street-theme.service';

@Component({
  selector: 'app-checkout-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="wrap">
      <h1>Panier public</h1>
      <p>
        Le parcours de commande invité complet sera branché sur l’API
        <code>POST /api/public/street/orders</code>. Pour l’instant, retournez à la
        <a routerLink="/visite-virtuelle">rue</a>.
      </p>
    </div>
  `,
  styles: [
    `
      .wrap {
        max-width: 640px;
        margin: 2rem auto;
        padding: 0 1rem;
      }
    `
  ]
})
export class CheckoutPageComponent implements OnInit, OnDestroy {
  private readonly theme = inject(StreetThemeService);

  ngOnInit(): void {
    this.theme.activate();
  }

  ngOnDestroy(): void {
    this.theme.deactivate();
  }
}
