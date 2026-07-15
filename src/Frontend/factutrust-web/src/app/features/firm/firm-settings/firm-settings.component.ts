import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-firm-settings',
  standalone: true,
  imports: [CommonModule, RouterModule, CardModule],
  template: `
    <div class="page">
      <h1>Paramètres cabinet</h1>
      <a routerLink="/firm/settings/users" class="card-link">
        <p-card header="Utilisateurs du cabinet">
          <p>Gérer les comptables et responsables du cabinet.</p>
        </p-card>
      </a>
    </div>
  `,
  styles: [`
    .page { padding: 1.5rem; }
    .card-link { text-decoration: none; color: inherit; display: block; max-width: 400px; margin-top: 1rem; }
  `]
})
export class FirmSettingsComponent {}
