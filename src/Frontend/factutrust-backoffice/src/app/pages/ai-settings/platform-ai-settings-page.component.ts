import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { SelectButtonModule } from 'primeng/selectbutton';
import { MessageService } from 'primeng/api';

import { PlatformAiSettingsService } from '@core/services/platform-ai-settings.service';
import type { OllamaInferenceDevice, PlatformAiSettingsDto } from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';

interface ModelOption {
  label: string;
  value: string;
}

interface InferenceDeviceOption {
  label: string;
  value: OllamaInferenceDevice;
  description: string;
}

/**
 * Configuration du modèle IA global de la plateforme.
 *
 * Le modèle sélectionné est utilisé par l'Assistant IA de toutes les entreprises
 * (Ollama tourne sur le serveur partagé de la plateforme). Les utilisateurs des
 * entreprises ne peuvent pas le modifier.
 */
@Component({
  selector: 'app-platform-ai-settings-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DropdownModule,
    SelectButtonModule,
    FtPageHeaderComponent,
    FtSkeletonComponent
  ],
  template: `
    <ft-page-header
      title="Configuration IA"
      subtitle="Modèle de langage utilisé par l'Assistant IA de toutes les entreprises." />

    @if (loading()) {
      <ft-skeleton kind="line" count="5" />
    }
    @if (!loading() && data(); as d) {
      <section class="card">
        <h3>Moteur d'inférence</h3>
        <p class="hint">
          Choisit si Ollama utilise le GPU (accélération matérielle) ou le CPU uniquement pour
          l'assistant, l'Assistant Studio, l'import de factures et le modèle vision.
        </p>
        <div class="field">
          <label for="inference-device">Exécution</label>
          <p-selectButton
            inputId="inference-device"
            [options]="inferenceDeviceOptions"
            [(ngModel)]="selectedInferenceDevice"
            optionLabel="label"
            optionValue="value"
            [allowEmpty]="false" />
        </div>
        <p class="hint device-hint">
          {{ inferenceDeviceHint() }}
        </p>
        @if (!d.isOllamaAssistantConfigured) {
          <p class="warn">
            Le modèle assistant configuré est cloud (OpenRouter) : ce réglage s'applique aux modèles Ollama locaux uniquement.
          </p>
        }
        @if (selectedInferenceDevice === 'CpuOnly' && isLargeAssistantModel()) {
          <p class="warn">
            Modèle assistant volumineux détecté : privilégiez un modèle ≤ 4B (ex. qwen2.5:3b-instruct) pour des réponses rapides sur CPU.
          </p>
        }
        @if (selectedInferenceDevice === 'CpuOnly') {
          <p class="hint device-hint">
            Réglages serveur CPU (appsettings Ollama) : CpuFixedChatNumCtx, CpuFixedChatNumCtxCeiling, CpuNumBatch,
            UseCompactChatPromptOnCpu. Redémarrer l'API après modification.
          </p>
        }
        @if (selectedInferenceDevice === 'CpuOnly' && d.serverInvoiceImportVisionModel) {
          <p class="warn">
            Import photo (vision) activé : l'analyse d'images sera significativement plus lente en mode CPU uniquement.
          </p>
        }
      </section>

      <section class="card">
        <h3>Modèle IA — Assistant</h3>
        <p class="hint">
          Modèle utilisé par l'Assistant IA de toutes les entreprises (conversation).
        </p>

        @if (d.availableModels.length) {
          <div class="field">
            <label for="ai-model">Modèle utilisé</label>
            <p-dropdown
              inputId="ai-model"
              [options]="modelOptions()"
              [(ngModel)]="selectedModelRef"
              optionLabel="label"
              optionValue="value"
              appendTo="body"
              styleClass="w-full" />
          </div>
        } @else {
          <p class="empty">
            Aucun modèle détecté. Vérifiez qu'Ollama est démarré sur le serveur de la plateforme.
          </p>
        }
      </section>

      <section class="card">
        <h3>Modèle IA — Import de factures</h3>
        <p class="hint">
          Modèle dédié à l'extraction des factures PDF (plus léger et rapide que le modèle assistant).
          Prioritaire sur la configuration serveur.
        </p>
        @if (d.availableModels.length) {
          <div class="field">
            <label for="ai-import-model">Modèle d'import</label>
            <p-dropdown
              inputId="ai-import-model"
              [options]="modelOptions()"
              [(ngModel)]="selectedImportModelRef"
              optionLabel="label"
              optionValue="value"
              appendTo="body"
              styleClass="w-full" />
          </div>
        }
      </section>

      <section class="card">
        <h3>Modèle IA — Assistant Studio</h3>
        <p class="hint">
          Modèle utilisé par l'Assistant Studio (création de tables, systèmes et plans) pour toutes les entreprises.
          Prioritaire sur le modèle Assistant. « Aucun » = même modèle que l'Assistant / serveur.
        </p>
        @if (d.availableModels.length) {
          <div class="field">
            <label for="ai-studio-model">Modèle Studio</label>
            <p-dropdown
              inputId="ai-studio-model"
              [options]="modelOptions()"
              [(ngModel)]="selectedStudioModelRef"
              optionLabel="label"
              optionValue="value"
              appendTo="body"
              styleClass="w-full" />
          </div>
        }
      </section>

      <section class="card">
        <h3>Modèle IA — Import photos (vision)</h3>
        <p class="hint">
          Utilisé en secours lorsque l'OCR ne suffit pas (bons de livraison photographiés, manuscrit).
          Configuré dans appsettings (<code>Ollama:InvoiceImportVisionModel</code>), ex. llava.
        </p>
        <p class="reco-model">
          @if (d.serverInvoiceImportVisionModel) {
            {{ d.serverInvoiceImportVisionModel }}
          } @else {
            <span class="empty">Non configuré (fallback vision désactivé)</span>
          }
        </p>
      </section>

      @if (d.recommendation; as reco) {
        <section class="card reco">
          <h3><i class="pi pi-star-fill" aria-hidden="true"></i> Modèle recommandé pour ce serveur</h3>
          <strong class="reco-model">{{ reco.displayLabel }}</strong>
          <p class="reco-reason">{{ reco.reason }}</p>
          <ul class="reco-specs">
            <li>RAM totale : {{ formatGb(reco.hardwareProfile.totalRamBytes) }} Go</li>
            <li>RAM disponible : {{ formatGb(reco.hardwareProfile.availableRamBytes) }} Go</li>
            <li>Cœurs CPU : {{ reco.hardwareProfile.cpuCores }}</li>
            @if (reco.hardwareProfile.gpu; as gpu) {
              <li>
                GPU : {{ gpu.name || 'détecté' }}@if (gpu.vramBytes) { · {{ formatGb(gpu.vramBytes) }} Go VRAM }
              </li>
            }
          </ul>
          <p-button
            label="Appliquer le modèle recommandé"
            icon="pi pi-check"
            [outlined]="true"
            severity="secondary"
            [disabled]="selectedModelRef === reco.recommendedModelRef"
            (onClick)="applyRecommendation(reco.recommendedModelRef)" />
        </section>
      }

      <div class="footer-actions">
        <p-button
          label="Enregistrer le modèle"
          icon="pi pi-check"
          severity="primary"
          [disabled]="!hasChanges() || busy()"
          [loading]="busy()"
          (onClick)="submit()" />
      </div>
    }
  `,
  styles: [
    `
      .card {
        background: var(--ft-surface-2, var(--ft-surface));
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: 1.1rem;
        margin-top: 1rem;
        max-width: 620px;
      }
      .card h3 {
        font-size: 0.95rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        margin: 0 0 0.6rem;
      }
      .hint {
        margin: 0 0 0.9rem;
        font-size: 0.85rem;
        color: var(--ft-text-muted);
        line-height: 1.5;
      }
      .empty {
        margin: 0;
        font-size: 0.85rem;
        color: var(--ft-text-muted);
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
      }
      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }
      .reco {
        border-color: var(--ft-accent-border, var(--ft-border));
        background: var(--ft-accent-muted, var(--ft-surface-2));
      }
      .reco h3 {
        color: var(--ft-accent, var(--ft-text));
      }
      .reco-model {
        display: block;
        font-size: 0.95rem;
        color: var(--ft-text);
      }
      .reco-reason {
        margin: 0.3rem 0 0.6rem;
        font-size: 0.85rem;
        color: var(--ft-text-muted);
      }
      .reco-specs {
        margin: 0 0 0.9rem;
        padding-left: 1.1rem;
        font-size: 0.85rem;
        color: var(--ft-text);
        line-height: 1.6;
      }
      .device-hint {
        margin-top: 0.65rem;
      }
      .warn {
        margin: 0.65rem 0 0;
        font-size: 0.85rem;
        color: var(--ft-warning, #b45309);
        line-height: 1.5;
      }
      .footer-actions {
        margin-top: 1.25rem;
        display: flex;
        justify-content: flex-start;
        max-width: 620px;
      }
      :host ::ng-deep .w-full {
        width: 100%;
      }
    `
  ]
})
export class PlatformAiSettingsPageComponent implements OnInit {
  private readonly api = inject(PlatformAiSettingsService);
  private readonly toast = inject(MessageService);

  protected readonly data = signal<PlatformAiSettingsDto | null>(null);
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);
  protected readonly savedModelRef = signal<string>('');
  protected readonly savedImportModelRef = signal<string>('');
  protected readonly savedStudioModelRef = signal<string>('');
  protected readonly savedInferenceDevice = signal<OllamaInferenceDevice>('Gpu');

  /** Liée par [(ngModel)] au sélecteur. */
  protected selectedModelRef = '';
  protected selectedImportModelRef = '';
  protected selectedStudioModelRef = '';
  protected selectedInferenceDevice: OllamaInferenceDevice = 'Gpu';

  protected readonly inferenceDeviceOptions: InferenceDeviceOption[] = [
    {
      label: 'GPU',
      value: 'Gpu',
      description: 'Ollama utilise le GPU si disponible (comportement par défaut).'
    },
    {
      label: 'CPU uniquement',
      value: 'CpuOnly',
      description: 'Force num_gpu=0 : le GPU n\'est pas sollicité pour l\'IA.'
    }
  ];

  protected readonly modelOptions = computed<ModelOption[]>(() => {
    const d = this.data();
    const options: ModelOption[] = [{ label: 'Aucun — modèle serveur par défaut', value: '' }];
    if (!d) {
      return options;
    }
    for (const m of d.availableModels) {
      const src = m.providerKey === 'openrouter' ? 'OpenRouter' : 'Ollama';
      options.push({ label: `${src} · ${m.displayLabel}`, value: m.modelRef });
    }
    // Modèle configuré mais plus installé : on l'ajoute pour ne pas perdre la valeur.
    const configuredRefs = [
      d.configuredModelRef,
      d.invoiceImportModelRef,
      d.studioAiModelRef
    ];
    for (const configured of configuredRefs) {
      if (configured
        && !d.availableModels.some(m => m.modelRef === configured)
        && !options.some(o => o.value === configured)) {
        options.push({ label: `${configured} (non installé)`, value: configured });
      }
    }
    return options;
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.get().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.data.set(res.data);
          this.selectedModelRef = res.data.configuredModelRef ?? '';
          this.savedModelRef.set(this.selectedModelRef);
          this.selectedImportModelRef = res.data.invoiceImportModelRef ?? '';
          this.savedImportModelRef.set(this.selectedImportModelRef);
          this.selectedStudioModelRef = res.data.studioAiModelRef ?? '';
          this.savedStudioModelRef.set(this.selectedStudioModelRef);
          this.selectedInferenceDevice = res.data.inferenceDevice ?? 'Gpu';
          this.savedInferenceDevice.set(this.selectedInferenceDevice);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Chargement de la configuration IA impossible.'
        });
      }
    });
  }

  protected hasChanges(): boolean {
    return this.selectedModelRef !== this.savedModelRef()
      || this.selectedImportModelRef !== this.savedImportModelRef()
      || this.selectedStudioModelRef !== this.savedStudioModelRef()
      || this.selectedInferenceDevice !== this.savedInferenceDevice();
  }

  protected inferenceDeviceHint(): string {
    const option = this.inferenceDeviceOptions.find(o => o.value === this.selectedInferenceDevice);
    const base = option?.description ?? '';
    return `${base} Le changement peut provoquer un rechargement du modèle au prochain message.`;
  }

  protected isLargeAssistantModel(): boolean {
    const ref = (this.selectedModelRef || this.data()?.configuredModelRef || '').toLowerCase();
    return /\b(7b|8b|13b|14b|32b|70b)\b/.test(ref);
  }

  protected applyRecommendation(modelRef: string): void {
    this.selectedModelRef = modelRef;
  }

  protected formatGb(bytes: number): string {
    return (bytes / 1024 ** 3).toFixed(1);
  }

  submit(): void {
    this.busy.set(true);
    const modelRef = this.selectedModelRef ? this.selectedModelRef : null;
    const invoiceImportModelRef = this.selectedImportModelRef ? this.selectedImportModelRef : null;
    const studioAiModelRef = this.selectedStudioModelRef ? this.selectedStudioModelRef : null;
    this.api.update({
      modelRef,
      invoiceImportModelRef,
      studioAiModelRef,
      inferenceDevice: this.selectedInferenceDevice
    }).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.data.set(res.data);
          this.selectedModelRef = res.data.configuredModelRef ?? '';
          this.savedModelRef.set(this.selectedModelRef);
          this.selectedImportModelRef = res.data.invoiceImportModelRef ?? '';
          this.savedImportModelRef.set(this.selectedImportModelRef);
          this.selectedStudioModelRef = res.data.studioAiModelRef ?? '';
          this.savedStudioModelRef.set(this.selectedStudioModelRef);
          this.selectedInferenceDevice = res.data.inferenceDevice ?? 'Gpu';
          this.savedInferenceDevice.set(this.selectedInferenceDevice);
          this.toast.add({
            severity: 'success',
            summary: 'Configuration IA enregistrée',
            detail: 'Le modèle sera rechargé avec le nouveau moteur d\'inférence au prochain message.'
          });
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.message ?? '' });
        }
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Enregistrement impossible.' });
      }
    });
  }
}
