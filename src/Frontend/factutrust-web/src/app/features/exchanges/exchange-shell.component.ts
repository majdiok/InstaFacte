import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin, of, timer } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';
import { ExchangeBadgeService } from '@core/services/exchange-badge.service';
import {
  ExchangeAuditEvent,
  ExchangeDocument,
  ExchangeMessage,
  ExchangeMessageVisibility,
  ExchangeRequest,
  ExchangeRequestCategory,
  ExchangeRequestStatus,
  ExchangeService,
  ExchangeTask,
  ExchangeTaskStatus,
  ExchangeThreadDetail,
  ExchangeThreadListItem
} from '@core/services/exchange.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';

type TabKey = 'conversation' | 'documents' | 'demandes' | 'taches' | 'historique';

@Component({
  selector: 'app-exchange-shell',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './exchange-shell.component.html',
  styleUrl: './exchange-shell.component.scss'
})
export class ExchangeShellComponent implements OnInit, OnDestroy {
  private readonly exchange = inject(ExchangeService);
  private readonly badge = inject(ExchangeBadgeService);
  readonly auth = inject(AuthService);
  private readonly assignments = inject(FirmAssignmentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly isFirm = computed(() => this.auth.isAccountingFirm());
  readonly canClose = computed(() => {
    const role = this.auth.user()?.role;
    return role === 'Administrator' || role === 'FirmManager';
  });
  readonly canInternalNote = computed(() => this.isFirm());

  readonly threads = signal<ExchangeThreadListItem[]>([]);
  readonly activeThread = signal<ExchangeThreadDetail | null>(null);
  readonly messages = signal<ExchangeMessage[]>([]);
  readonly requests = signal<ExchangeRequest[]>([]);
  readonly tasks = signal<ExchangeTask[]>([]);
  readonly documents = signal<ExchangeDocument[]>([]);
  readonly history = signal<ExchangeAuditEvent[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly emptyHint = signal<string | null>(null);
  readonly activeTab = signal<TabKey>('conversation');
  readonly composerMode = signal<'message' | 'note'>('message');
  readonly draft = signal('');
  readonly search = signal('');
  readonly showNewRequest = signal(false);
  readonly showNewTask = signal(false);
  readonly newRequestTitle = signal('');
  readonly newRequestDescription = signal('');
  readonly newRequestCategory = signal<ExchangeRequestCategory>(0);
  readonly newTaskTitle = signal('');
  readonly newTaskDue = signal('');

  readonly filteredThreads = computed(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.threads();
    if (!q) return list;
    return list.filter(t => t.counterpartName.toLowerCase().includes(q));
  });

  private pollSub: Subscription | null = null;
  private routeSub: Subscription | null = null;

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'documents' || tab === 'demandes' || tab === 'taches' || tab === 'historique') {
      this.activeTab.set(tab);
    } else if (tab === 'partage') {
      this.activeTab.set('documents');
    }

    this.routeSub = this.route.paramMap.subscribe(params => {
      const id = params.get('threadId');
      this.bootstrap(id);
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
    this.routeSub?.unsubscribe();
  }

  selectThread(threadId: string): void {
    const base = this.isFirm() ? '/firm/exchanges' : '/exchanges';
    void this.router.navigate([base, threadId], {
      queryParamsHandling: 'preserve'
    });
  }

  setTab(tab: TabKey): void {
    this.activeTab.set(tab);
    const base = this.isFirm() ? '/firm/exchanges' : '/exchanges';
    const threadId = this.activeThread()?.id;
    void this.router.navigate(threadId ? [base, threadId] : [base], {
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
    if (threadId) this.loadTabData(threadId, tab);
  }

  send(): void {
    const thread = this.activeThread();
    const body = this.draft().trim();
    if (!thread || !body) return;
    const visibility: ExchangeMessageVisibility =
      this.composerMode() === 'note' && this.canInternalNote() ? 1 : 0;
    this.exchange.sendMessage(thread.id, body, visibility).subscribe({
      next: r => {
        if (r.success) {
          this.draft.set('');
          this.messages.update(m => [...m, r.data]);
          this.badge.invalidate();
        } else {
          this.error.set(r.message || 'Envoi impossible');
        }
      },
      error: () => this.error.set('Envoi impossible')
    });
  }

  onFileSelected(event: Event): void {
    const thread = this.activeThread();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!thread || !file) return;
    this.exchange.uploadDocument(thread.id, file).subscribe({
      next: r => {
        if (r.success) {
          this.documents.update(d => [r.data, ...d]);
          this.setTab('documents');
        } else {
          this.error.set(r.message || 'Upload impossible');
        }
        input.value = '';
      },
      error: () => {
        this.error.set('Upload impossible');
        input.value = '';
      }
    });
  }

  downloadDoc(doc: ExchangeDocument): void {
    const thread = this.activeThread();
    if (!thread) return;
    this.exchange.downloadDocument(thread.id, doc.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = doc.fileName;
        a.click();
        URL.revokeObjectURL(url);
      }
    });
  }

  createRequest(): void {
    const thread = this.activeThread();
    const title = this.newRequestTitle().trim();
    if (!thread || !title) return;
    this.exchange
      .createRequest(thread.id, {
        title,
        description: this.newRequestDescription().trim() || undefined,
        category: this.newRequestCategory(),
        priority: 1
      })
      .subscribe({
        next: r => {
          if (r.success) {
            this.requests.update(list => [r.data, ...list]);
            this.showNewRequest.set(false);
            this.newRequestTitle.set('');
            this.newRequestDescription.set('');
            this.setTab('demandes');
            this.badge.invalidate();
          }
        }
      });
  }

  changeRequestStatus(req: ExchangeRequest, status: ExchangeRequestStatus): void {
    const thread = this.activeThread();
    if (!thread) return;
    this.exchange.changeRequestStatus(thread.id, req.id, status).subscribe({
      next: r => {
        if (r.success) {
          this.requests.update(list => list.map(x => (x.id === req.id ? r.data : x)));
          this.badge.invalidate();
        }
      }
    });
  }

  createTask(asAppointment = false): void {
    const thread = this.activeThread();
    const title = this.newTaskTitle().trim() || (asAppointment ? 'Rendez-vous' : '');
    if (!thread || !title) return;
    this.exchange
      .createTask(thread.id, {
        title,
        dueDate: this.newTaskDue() || undefined
      })
      .subscribe({
        next: r => {
          if (r.success) {
            this.tasks.update(list => [r.data, ...list]);
            this.showNewTask.set(false);
            this.newTaskTitle.set('');
            this.newTaskDue.set('');
            this.setTab('taches');
          }
        }
      });
  }

  changeTaskStatus(task: ExchangeTask, status: ExchangeTaskStatus): void {
    const thread = this.activeThread();
    if (!thread) return;
    this.exchange.changeTaskStatus(thread.id, task.id, status).subscribe({
      next: r => {
        if (r.success) {
          this.tasks.update(list => list.map(x => (x.id === task.id ? r.data : x)));
        }
      }
    });
  }

  closeExchange(): void {
    const thread = this.activeThread();
    if (!thread || !this.canClose()) return;
    this.exchange.closeThread(thread.id).subscribe({
      next: r => {
        if (r.success) this.reloadActive(thread.id);
      }
    });
  }

  reopenExchange(): void {
    const thread = this.activeThread();
    if (!thread || !this.canClose()) return;
    this.exchange.reopenThread(thread.id).subscribe({
      next: r => {
        if (r.success) this.reloadActive(thread.id);
      }
    });
  }

  formatBytes(n: number): string {
    if (n < 1024) return `${n} o`;
    if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} Ko`;
    return `${(n / (1024 * 1024)).toFixed(1)} Mo`;
  }

  requestStatusLabel(s: ExchangeRequestStatus): string {
    return ['Ouverte', 'En cours', 'Attente client', 'Attente cabinet', 'Résolue', 'Close'][s] ?? '';
  }

  categoryLabel(c: ExchangeRequestCategory): string {
    return ['Réclamation', 'Information', 'Document manquant', 'Autre'][c] ?? '';
  }

  taskStatusLabel(s: ExchangeTaskStatus): string {
    return ['À faire', 'En cours', 'Terminée', 'Annulée'][s] ?? '';
  }

  auditLabel(type: number): string {
    const labels = [
      'Échange ouvert',
      'Message envoyé',
      'Note interne',
      'Demande créée',
      'Statut demande',
      'Tâche créée',
      'Tâche terminée',
      'Document partagé',
      'Échange clos',
      'Échange rouvert',
      'Rendez-vous suggéré',
      'Statut tâche'
    ];
    return labels[type] ?? `Événement ${type}`;
  }

  private bootstrap(threadId: string | null): void {
    this.loading.set(true);
    this.error.set(null);
    this.emptyHint.set(null);

    this.exchange.listThreads().subscribe({
      next: listRes => {
        const list = listRes.success ? listRes.data : [];
        this.threads.set(list);

        if (this.isFirm()) {
          if (list.length === 0) {
            this.assignments.getActiveClients().subscribe({
              next: clientsRes => {
                const clients = clientsRes.success ? clientsRes.data : [];
                if (clients.length === 0) {
                  this.loading.set(false);
                  this.emptyHint.set('Aucun dossier client actif pour démarrer un échange.');
                  return;
                }
                // Ensure a thread for the first accessible client, then reload list
                this.exchange.ensureThread(clients[0].assignmentId).subscribe({
                  next: eRes => {
                    if (eRes.success) {
                      this.selectThread(eRes.data.id);
                    } else {
                      this.emptyHint.set(eRes.message || 'Impossible de créer l’échange');
                    }
                    this.loading.set(false);
                  },
                  error: () => {
                    this.loading.set(false);
                    this.error.set('Impossible de créer l’échange');
                  }
                });
              },
              error: () => {
                this.loading.set(false);
                this.emptyHint.set('Aucun dossier client actif pour démarrer un échange.');
              }
            });
            return;
          }
          if (threadId) {
            this.reloadActive(threadId);
          } else {
            this.selectThread(list[0].id);
            this.loading.set(false);
          }
          return;
        }

        // Company: ensure thread from active assignment
        this.assignments.getCompanyCurrent().subscribe({
          next: aRes => {
            const assignment = aRes.success ? aRes.data : null;
            if (!assignment || assignment.status !== 1) {
              this.loading.set(false);
              this.emptyHint.set(
                'Liez un cabinet comptable dans Paramètres → Cabinet comptable pour ouvrir les échanges.'
              );
              return;
            }
            this.exchange.ensureThread().subscribe({
              next: eRes => {
                if (!eRes.success) {
                  this.loading.set(false);
                  this.error.set(eRes.message || 'Impossible d’ouvrir l’échange');
                  return;
                }
                if (!threadId || threadId !== eRes.data.id) {
                  this.selectThread(eRes.data.id);
                  this.loading.set(false);
                  return;
                }
                this.activeThread.set(eRes.data);
                this.loadAll(eRes.data.id);
              },
              error: () => {
                this.loading.set(false);
                this.error.set('Impossible d’ouvrir l’échange');
              }
            });
          },
          error: () => {
            this.loading.set(false);
            this.emptyHint.set('Impossible de vérifier la liaison cabinet.');
          }
        });
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Impossible de charger les échanges');
      }
    });
  }

  private reloadActive(threadId: string): void {
    this.loading.set(true);
    this.exchange.getThread(threadId).subscribe({
      next: r => {
        if (!r.success) {
          this.loading.set(false);
          this.error.set(r.message || 'Échange introuvable');
          return;
        }
        this.activeThread.set(r.data);
        this.loadAll(threadId);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Échange introuvable');
      }
    });
  }

  private loadAll(threadId: string): void {
    forkJoin({
      messages: this.exchange.getMessages(threadId).pipe(
        catchError(() => of({ success: true as const, data: [] as ExchangeMessage[], message: null, errors: [] as string[] }))
      ),
      requests: this.exchange.listRequests(threadId).pipe(
        catchError(() => of({ success: true as const, data: [] as ExchangeRequest[], message: null, errors: [] as string[] }))
      ),
      tasks: this.exchange.listTasks(threadId).pipe(
        catchError(() => of({ success: true as const, data: [] as ExchangeTask[], message: null, errors: [] as string[] }))
      ),
      documents: this.exchange.listDocuments(threadId).pipe(
        catchError(() => of({ success: true as const, data: [] as ExchangeDocument[], message: null, errors: [] as string[] }))
      ),
      history: this.exchange.getHistory(threadId).pipe(
        catchError(() => of({ success: true as const, data: [] as ExchangeAuditEvent[], message: null, errors: [] as string[] }))
      )
    }).subscribe({
      next: res => {
        if (res.messages.success) this.messages.set(res.messages.data);
        if (res.requests.success) this.requests.set(res.requests.data);
        if (res.tasks.success) this.tasks.set(res.tasks.data);
        if (res.documents.success) this.documents.set(res.documents.data);
        if (res.history.success) this.history.set(res.history.data);

        this.markUnread(threadId);
        this.startPolling(threadId);
        this.loading.set(false);
        this.badge.invalidate();
      },
      error: () => this.loading.set(false)
    });
  }

  private loadTabData(threadId: string, tab: TabKey): void {
    if (tab === 'demandes') {
      this.exchange.listRequests(threadId).subscribe(r => {
        if (r.success) this.requests.set(r.data);
      });
    } else if (tab === 'taches') {
      this.exchange.listTasks(threadId).subscribe(r => {
        if (r.success) this.tasks.set(r.data);
      });
    } else if (tab === 'documents') {
      this.exchange.listDocuments(threadId).subscribe(r => {
        if (r.success) this.documents.set(r.data);
      });
    } else if (tab === 'historique') {
      this.exchange.getHistory(threadId).subscribe(r => {
        if (r.success) this.history.set(r.data);
      });
    }
  }

  private markUnread(threadId: string): void {
    const me = this.auth.user()?.id;
    for (const m of this.messages()) {
      if (me && m.authorUserId !== me && !m.readReceipts.some(r => r.userId === me)) {
        this.exchange.markRead(threadId, m.id).subscribe();
      }
    }
  }

  private startPolling(threadId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(12000, 12000)
      .pipe(switchMap(() => this.exchange.getMessages(threadId)))
      .subscribe({
        next: r => {
          if (r.success) {
            this.messages.set(r.data);
            this.markUnread(threadId);
            this.badge.invalidate();
          }
        }
      });
  }
}
