import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { ApiResponse } from '@core/services/client.service';
import { STUDIO_WORKFLOW_LABELS } from './studio-workflow-labels';
import { workflowErrorMessage } from './studio-workflow-http.util';
import { StudioWorkflowInstanceDetailComponent } from './studio-workflow-instance-detail.component';
import { StudioWorkflowStatusTagComponent } from './studio-workflow-status-tag.component';
import { RunnableWorkflowDto, WorkflowInstanceDto, isOpenInstance } from './studio-workflows.models';
import { StudioWorkflowsService } from './studio-workflows.service';

/**
 * Onglet « Workflows » de la fiche enregistrement (4.4h2 ; maquette
 * `d44-approvals-record-tab.html`) : les instances de l'enregistrement (sonde
 * `listRecordInstances` portée par la fiche — 403/404 ⇒ l'onglet n'est pas rendu du tout,
 * fail-closed silencieux D21), le lancement manuel (`listRunnableWorkflows` + `runWorkflow`
 * PAR CLÉ — D-44-84, 201 ; 409 ⇒ « déjà en cours », 400 quota ⇒ message serveur D-44-02) et,
 * sur les instances ouvertes (`isOpenInstance`, 4.4a1 H-11), « Annuler » / « Relancer les
 * approbateurs ». Actions d'écriture rendues seulement avec `custom_records:write` (R17) ;
 * « Détail » rendu pour tout lecteur (4.5d3, D-45-F05 — D-44-82 levé) — il ouvre le drawer
 * 4.4f en place (`[(instanceId)]` + `[entityKey]` + `[recordId]` ⇒ route runtime
 * `custom_records:read` 4.5b/4.5d2, appelée seulement à l'ouverture) ;
 * `(changed)` de l'onglet ou du drawer ⇒ la fiche relance la sonde.
 * Écart maquette (annexe fait foi) : le lancement passe par un `p-dialog` (select + confirmer)
 * au lieu de la carte inline de la maquette ; colonnes « Démarré le » + « Échéance » (la
 * maquette combinait « Démarré par / le »).
 */
@Component({
  selector: 'app-studio-record-workflows-tab',
  standalone: true,
  imports: [
    DatePipe, FormsModule,
    ButtonModule, DialogModule, SelectModule, TableModule, TooltipModule,
    StudioWorkflowStatusTagComponent, StudioWorkflowInstanceDetailComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="srw-head">
      <p class="studio-muted">{{ labels.hint }}</p>
      @if (canWrite()) {
        <p-button [label]="labels.run" icon="fa-solid fa-play" size="small" (onClick)="openRun()" data-testid="srw-run" />
      }
    </div>
    <div class="ft-table-card">
      <p-table [value]="instances()" dataKey="id" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ labels.colWorkflow }}</th><th>{{ labels.colStatus }}</th><th>{{ labels.colStep }}</th>
            <th>{{ labels.colStarted }}</th><th>{{ labels.colDue }}</th><th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr [attr.data-testid]="'srw-row-' + row.id">
            <td>{{ row.workflowName }}</td>
            <td><app-studio-workflow-status-tag [status]="row.status" /></td>
            <td>{{ row.currentStepKey ?? '—' }}</td>
            <td>{{ row.startedAt | date:'dd/MM/yyyy HH:mm' }}</td>
            <td>{{ row.dueAt ? (row.dueAt | date:'dd/MM/yyyy HH:mm') : '—' }}</td>
            <td class="srw-actions">
              <p-button icon="fa-solid fa-eye" [text]="true" size="small" [pTooltip]="labels.detail"
                (onClick)="openInstanceId.set(row.id)" [attr.data-testid]="'srw-detail-' + row.id" />
              @if (canWrite() && isOpen(row)) {
                <p-button icon="fa-solid fa-bell" [text]="true" size="small" [pTooltip]="labels.remind"
                  (onClick)="remind(row)" [attr.data-testid]="'srw-remind-' + row.id" />
                <p-button icon="fa-solid fa-ban" [text]="true" size="small" severity="danger" [pTooltip]="labels.cancel"
                  (onClick)="cancel(row)" [attr.data-testid]="'srw-cancel-' + row.id" />
              }
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="6" class="ft-empty">{{ labels.empty }}</td></tr>
        </ng-template>
      </p-table>
    </div>
    <p-dialog [visible]="runOpen()" (visibleChange)="$event || closeRun()" [modal]="true" appendTo="body"
      styleClass="studio-theme" [header]="labels.runTitle" [style]="{ width: '440px', maxWidth: '95vw' }">
      <p-select [options]="runnable()" optionLabel="name" optionValue="key" [ngModel]="runKey()"
        (ngModelChange)="runKey.set($event)" [placeholder]="labels.runPlaceholder" appendTo="body"
        panelStyleClass="studio-theme" [loading]="runnableLoading()" data-testid="srw-run-select" />
      @if (!runnableLoading() && runnable().length === 0) {
        <p class="studio-muted">{{ labels.runEmpty }}</p>
      }
      <div class="srw-dialog__actions">
        <p-button [label]="labels.close" severity="secondary" [text]="true" (onClick)="closeRun()" />
        <p-button [label]="labels.run" icon="fa-solid fa-play" [disabled]="!runKey() || busy()" [loading]="busy()"
          (onClick)="confirmRun()" data-testid="srw-run-confirm" />
      </div>
    </p-dialog>
    <!-- 4.4f (H-8) : model() two-way ; le drawer n'appelle la route qu'à l'ouverture explicite.
         4.5d3 : portée fiche (entityKey + recordId ⇒ route runtime custom_records:read, 4.5d2) -->
    <app-studio-workflow-instance-detail [(instanceId)]="openInstanceId" [entityKey]="entityKey()" [recordId]="recordId()"
      (changed)="changed.emit()" (closed)="openInstanceId.set(null)" />
  `,
  // studio-layout.scss fournit .studio-muted (encapsulation émulée — même motif que
  // l'onglet « Liés » 2.5e2) ; .ft-table-card vient de la couche design globale.
  styleUrl: '../shared/studio-layout.scss',
  styles: [`
    .srw-head { display: flex; align-items: center; justify-content: space-between; gap: .75rem; margin-bottom: .75rem; }
    .srw-head p { margin: 0; }
    .srw-actions { display: flex; gap: .25rem; justify-content: flex-end; }
    .srw-dialog__actions { display: flex; justify-content: flex-end; gap: .5rem; margin-top: 1rem; }
    .ft-empty { text-align: center; color: var(--text-color-secondary); padding: 2rem; }
  `]
})
export class StudioRecordWorkflowsTabComponent {
  readonly entityKey = input.required<string>();
  readonly recordId = input.required<string>();
  readonly instances = input<WorkflowInstanceDto[]>([]);
  readonly canWrite = input(false);
  /** La fiche recharge la sonde (une instance lancée, annulée ou relancée). */
  readonly changed = output<void>();

  private readonly workflows = inject(StudioWorkflowsService);
  /** D-44-89 : le `<p-toast>` est porté par la fiche hôte (`studio-record-form.component.ts:33`) ; `MessageService` est root. */
  private readonly toast = inject(MessageService);

  readonly labels = STUDIO_WORKFLOW_LABELS.recordTab;
  readonly runOpen = signal(false);
  readonly runnable = signal<RunnableWorkflowDto[]>([]);
  readonly runnableLoading = signal(false);
  readonly runKey = signal<string | null>(null);
  readonly busy = signal(false);
  readonly openInstanceId = signal<string | null>(null);

  isOpen(row: WorkflowInstanceDto): boolean { return isOpenInstance(row.status); }

  openRun(): void {
    this.runOpen.set(true);
    this.runnableLoading.set(true);
    this.runKey.set(null);
    this.workflows.listRunnableWorkflows(this.entityKey()).subscribe({
      next: res => { this.runnable.set(res.success ? (res.data ?? []) : []); this.runnableLoading.set(false); },
      error: () => { this.runnable.set([]); this.runnableLoading.set(false); }
    });
  }

  closeRun(): void { if (!this.busy()) this.runOpen.set(false); }

  confirmRun(): void {
    const key = this.runKey();
    if (!key) return;
    this.busy.set(true);
    // POST records/{entityKey}/{recordId}/workflows/{key}/run — 201 (adressage PAR CLÉ, D-44-84)
    this.workflows.runWorkflow(this.entityKey(), this.recordId(), key).subscribe({
      next: () => {
        this.busy.set(false);
        this.runOpen.set(false);
        this.toast.add({ severity: 'success', summary: this.labels.title, detail: this.labels.started });
        this.changed.emit();
      },
      // 400 quota 200 instances ⇒ message serveur (D-44-02) ; 409 ⇒ « déjà en cours »
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.toast.add({ severity: 'warn', summary: this.labels.title, detail: err.status === 409 ? this.labels.alreadyRunning : (workflowErrorMessage(err) || this.labels.runError) });
      }
    });
  }

  cancel(row: WorkflowInstanceDto): void { this.act(this.workflows.cancelInstance(row.id, null), this.labels.cancelled); } // corps { reason: null } posé par le service
  remind(row: WorkflowInstanceDto): void { this.act(this.workflows.remindApprovers(row.id), this.labels.reminded); }        // 409 « < 24 h » ⇒ message serveur

  private act(call: Observable<ApiResponse<WorkflowInstanceDto>>, okDetail: string): void {
    if (this.busy()) return;
    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'success', summary: this.labels.title, detail: okDetail });
        this.changed.emit();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.toast.add({ severity: 'warn', summary: this.labels.title, detail: workflowErrorMessage(err) || this.labels.actionError });
      }
    });
  }
}
