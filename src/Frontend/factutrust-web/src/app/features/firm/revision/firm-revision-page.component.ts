import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AuthService } from '@core/services/auth.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  FirmRevisionService,
  FirmRevisionDossierDetail,
  FirmRevisionDossierRow,
  FirmRevisionOverview,
  FirmRevisionWorkQueue
} from './firm-revision.service';
import {
  fanOutWarning,
  formatImpact,
  neverScannedHint,
  riskLabelOf,
  riskLevelOf,
  severityClass,
  severityLabel,
  sortByPriority,
  sortNoteItems
} from './firm-revision.view-model';

type RevisionTab = 'portfolio' | 'queue';

@Component({
  selector: 'app-firm-revision-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    DialogModule,
    TagModule,
    PageHeaderComponent,
    StatCardComponent,
    ButtonComponent
  ],
  templateUrl: './firm-revision-page.component.html',
  styleUrl: './firm-revision-page.component.scss'
})
export class FirmRevisionPageComponent implements OnInit {
  private readonly api = inject(FirmRevisionService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  /**
   * Le balayage mobilise toutes les bases dossiers : réservé au responsable de cabinet. Le
   * collaborateur consulte mais ne déclenche pas. `runSweep()` conserve la traduction du 403 en
   * défense en profondeur, si le JWT et l'API divergeaient.
   */
  readonly canSweep = computed(() => this.auth.hasPermission(PERMISSIONS.firmRevision.manage));

  readonly loading = signal(false);
  readonly sweeping = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly overview = signal<FirmRevisionOverview | null>(null);
  readonly workQueue = signal<FirmRevisionWorkQueue | null>(null);
  readonly selectedDossier = signal<FirmRevisionDossierDetail | null>(null);
  readonly detailVisible = signal(false);

  fiscalYear = new Date().getFullYear();
  activeTab: RevisionTab = 'portfolio';

  readonly dossiers = computed(() => sortByPriority(this.overview()?.dossiers ?? []));
  readonly families = computed(() => this.overview()?.byFamily ?? []);
  readonly partialWarning = computed(() => fanOutWarning(this.overview()));
  readonly neverScannedNotice = computed(() => neverScannedHint(this.overview()));

  readonly noteItems = computed(() => {
    const note = this.selectedDossier()?.note;
    return note ? sortNoteItems(note.items) : [];
  });

  readonly tabs: { key: RevisionTab; label: string }[] = [
    { key: 'portfolio', label: 'Portefeuille' },
    { key: 'queue', label: 'File de travail' }
  ];

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.error.set(null);
    this.loading.set(true);

    this.api.getOverview(this.fiscalYear).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.overview.set(res.data);
        } else {
          this.error.set(res.error ?? 'Chargement impossible.');
        }
      },
      error: err => {
        this.loading.set(false);
        // 503 = module éteint côté API. Le dire explicitement plutôt qu'« erreur réseau ».
        if (err?.status === 503) {
          this.error.set("Le réviseur de portefeuille n'est pas activé sur cette instance.");
          return;
        }
        // 403 = fenêtre de jeton périmé : `/auth/me` rafraîchit les permissions du front sans
        // faire tourner le JWT, donc le menu peut s'ouvrir avant que les revendications du jeton
        // ne portent `firm:revision:view`. Une reconnexion referme l'écart.
        if (err?.status === 403) {
          this.error.set(
            'Vos droits ont changé depuis votre connexion. Reconnectez-vous pour accéder au réviseur.'
          );
          return;
        }
        this.error.set('Erreur réseau lors du chargement du portefeuille.');
      }
    });

    this.loadWorkQueue();
  }

  loadWorkQueue(): void {
    this.api.getWorkQueue(this.fiscalYear).subscribe({
      next: res => {
        if (res.success && res.data) this.workQueue.set(res.data);
      },
      error: () => {
        // La file est secondaire : son échec ne doit pas masquer le portefeuille déjà chargé.
      }
    });
  }

  setTab(tab: RevisionTab): void {
    this.activeTab = tab;
  }

  runSweep(): void {
    this.sweeping.set(true);
    this.error.set(null);
    this.notice.set(null);

    this.api.sweep(this.fiscalYear).subscribe({
      next: res => {
        this.sweeping.set(false);
        if (res.success && res.data) {
          const r = res.data;
          this.notice.set(
            `${r.dossiersScanned} dossier(s) contrôlé(s), ${r.totalAnomalies} anomalie(s)` +
            (r.dossiersFailed > 0 ? ` — ${r.dossiersFailed} dossier(s) en échec.` : '.'));
          this.refresh();
        } else {
          this.error.set(res.error ?? 'Le balayage a échoué.');
        }
      },
      error: err => {
        this.sweeping.set(false);
        this.error.set(err?.status === 403
          ? 'Seul le responsable de cabinet peut lancer un balayage.'
          : 'Le balayage a échoué.');
      }
    });
  }

  openDossier(rowData: FirmRevisionDossierRow): void {
    if (rowData.readFailed) return;

    this.api.getDossier(rowData.companyTenantId, this.fiscalYear).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.selectedDossier.set(res.data);
          this.detailVisible.set(true);
        } else {
          this.error.set(res.error ?? 'Dossier illisible.');
        }
      },
      error: () => this.error.set('Dossier illisible.')
    });
  }

  /** Ouvre le dossier client dans son propre contexte, pour y corriger l'anomalie. */
  openClientDossier(rowData: FirmRevisionDossierRow): void {
    this.router.navigate(['/firm/open', rowData.companyTenantId]);
  }

  // Passerelles vers le view-model — la logique reste testable hors composant.
  readonly riskLevelOf = riskLevelOf;
  readonly riskLabelOf = riskLabelOf;
  readonly severityLabel = severityLabel;
  readonly severityClass = severityClass;
  readonly formatImpact = formatImpact;
}
