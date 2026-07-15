import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { FirmAssignmentService, FirmClientAssignment } from '@core/services/firm-assignment.service';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-firm-invitations',
  standalone: true,
  providers: [MessageService],
  imports: [CommonModule, TableModule, ButtonModule, ToastModule],
  template: `
    <p-toast />
    <div class="page">
      <h1>Invitations</h1>
      <p-table [value]="items()" [loading]="loading()">
        <ng-template pTemplate="header">
          <tr>
            <th>Société</th>
            <th>Demandée le</th>
            <th>Notes</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>{{ row.companyName }}</td>
            <td>{{ row.requestedAt | date:'short' }}</td>
            <td>{{ row.notes || '—' }}</td>
            <td class="actions">
              @if (auth.isFirmManager()) {
                <button pButton label="Accepter" class="p-button-success" (click)="accept(row)"></button>
                <button pButton label="Refuser" class="p-button-danger p-button-outlined" (click)="reject(row)"></button>
              }
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`
    .page { padding: 1.5rem; }
    .actions { display: flex; gap: 0.5rem; }
  `]
})
export class FirmInvitationsComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly messages = inject(MessageService);

  readonly items = signal<FirmClientAssignment[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.assignments.getIncomingInvitations().subscribe({
      next: r => {
        if (r.success) this.items.set(r.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  accept(row: FirmClientAssignment): void {
    this.assignments.acceptInvitation(row.id).subscribe({
      next: r => {
        if (r.success) {
          this.messages.add({ severity: 'success', summary: 'Acceptée' });
          this.load();
        }
      }
    });
  }

  reject(row: FirmClientAssignment): void {
    this.assignments.rejectInvitation(row.id).subscribe({
      next: r => {
        if (r.success) {
          this.messages.add({ severity: 'info', summary: 'Refusée' });
          this.load();
        }
      }
    });
  }
}
