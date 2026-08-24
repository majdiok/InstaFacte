import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { PortalService } from './portal.service';

@Component({
  selector: 'app-portal-profile',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h1>Profil</h1>
    @if (me(); as m) {
      <dl>
        <dt>Raison sociale</dt><dd>{{ m.clientName }}</dd>
        <dt>NIF</dt><dd>{{ m.clientNif || '—' }}</dd>
        <dt>Email</dt><dd>{{ m.clientEmail || m.contactEmail }}</dd>
        <dt>Contact</dt><dd>{{ m.contactName }}</dd>
      </dl>
      <p class="hint">Les informations fiscales se mettent à jour auprès de votre fournisseur.</p>
    }
  `,
  styles: [`dl { display: grid; grid-template-columns: 160px 1fr; gap: .5rem 1rem; background: white; padding: 1rem; border-radius: 8px; } .hint { color: #6b7280; }`]
})
export class PortalProfileComponent {
  readonly me = toSignal(inject(PortalService).getMe());
}
