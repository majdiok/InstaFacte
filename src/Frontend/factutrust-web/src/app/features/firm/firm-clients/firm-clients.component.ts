import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FirmAssignmentService, FirmClientDossier } from '@core/services/firm-assignment.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-firm-clients',
  standalone: true,
  providers: [MessageService],
  imports: [CommonModule, TableModule, ButtonModule, ToastModule],
  template: `
    <p-toast />
    <div class="page">
      <h1>Dossiers clients</h1>
      <p-table [value]="clients()" [loading]="loading()">
        <ng-template pTemplate="header">
          <tr>
            <th>Société</th>
            <th>Active depuis</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.companyName }}</td>
            <td>{{ row.activeSince | date:'short' }}</td>
            <td>
              <button pButton label="Ouvrir la comptabilité" (click)="openDossier(row)"></button>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`.page { padding: 1.5rem; }`]
})
export class FirmClientsComponent implements OnInit {
  private readonly assignments = inject(FirmAssignmentService);
  private readonly firmContext = inject(FirmContextService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  readonly clients = signal<FirmClientDossier[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.assignments.getActiveClients().subscribe({
      next: r => {
        if (r.success) this.clients.set(r.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  async openDossier(row: FirmClientDossier): Promise<void> {
    try {
      await this.firmContext.switchClient(row.companyTenantId);
      await this.router.navigate(['/accounting/chart']);
    } catch {
      this.messages.add({ severity: 'error', summary: 'Erreur', detail: 'Impossible d\'ouvrir le dossier' });
    }
  }
}
