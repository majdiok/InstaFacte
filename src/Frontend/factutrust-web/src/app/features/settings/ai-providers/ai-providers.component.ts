import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputSwitchModule } from 'primeng/inputswitch';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ToastService } from '@core/services/toast.service';
import {
  AiChatService,
  OpenRouterProviderSettingsDto,
  UpdateOpenRouterProviderRequest
} from '../../ai-assistant/services/ai-chat.service';

@Component({
  selector: 'app-ai-providers',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputSwitchModule,
    PageHeaderComponent,
    BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Fournisseurs IA"
      subtitle="Connectez OpenRouter pour utiliser des modèles cloud (API compatible OpenAI)">
      <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" routerLink="/settings"></p-button>
    </app-page-header>

    @if (loading()) {
      <p role="status" aria-live="polite">Chargement…</p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="save()" class="ai-form">
        <div class="field">
          <label for="ft-or-enabled">Activer OpenRouter</label>
          <p-inputSwitch inputId="ft-or-enabled" formControlName="isEnabled"></p-inputSwitch>
        </div>

        <div class="field">
          <label for="ft-or-display">Nom affiché</label>
          <input id="ft-or-display" type="text" pInputText formControlName="displayName" class="w-full" />
        </div>

        <div class="field">
          <label for="ft-or-base">URL de base (optionnel)</label>
          <input
            id="ft-or-base"
            type="url"
            pInputText
            formControlName="baseUrl"
            class="w-full"
            [placeholder]="defaultBaseUrl()" />
          <small class="hint">Par défaut : {{ defaultBaseUrl() }}</small>
        </div>

        <div class="field">
          <label for="ft-or-key">Clé API</label>
          <input
            id="ft-or-key"
            type="password"
            pInputText
            formControlName="apiKey"
            class="w-full"
            autocomplete="off"
            [placeholder]="keyPlaceholder()" />
          @if (settings()?.isApiKeyConfigured) {
            <small class="hint">Clé configurée (se termine par …{{ settings()?.apiKeyLast4 }})</small>
          }
        </div>

        <p-button type="submit" label="Enregistrer" icon="pi pi-check" [loading]="saving()" [disabled]="form.invalid || saving()"></p-button>
      </form>
    }
  `,
  styles: [
    `
      .ai-form {
        max-width: 520px;
        display: flex;
        flex-direction: column;
        gap: var(--spacing-4);
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: var(--spacing-2);
      }
      .field label {
        font-weight: var(--font-weight-medium);
      }
      .hint {
        color: var(--color-neutral-500);
        font-size: var(--font-size-sm);
      }
      .w-full {
        width: 100%;
      }
    `
  ]
})
export class AiProvidersComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly aiChat = inject(AiChatService);
  private readonly toast = inject(ToastService);

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Fournisseurs IA' }
  ];

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly settings = signal<OpenRouterProviderSettingsDto | null>(null);
  readonly defaultBaseUrl = signal('');

  form = this.fb.nonNullable.group({
    isEnabled: [false],
    displayName: [''],
    baseUrl: [''],
    apiKey: ['']
  });

  ngOnInit(): void {
    this.aiChat.getOpenRouterSettings().subscribe({
      next: s => {
        this.settings.set(s);
        this.defaultBaseUrl.set(s.defaultBaseUrl);
        this.form.patchValue({
          isEnabled: s.isEnabled,
          displayName: s.displayName ?? '',
          baseUrl: s.baseUrl ?? '',
          apiKey: ''
        });
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Impossible de charger la configuration OpenRouter.'
        });
        this.loading.set(false);
      }
    });
  }

  keyPlaceholder(): string {
    return this.settings()?.isApiKeyConfigured
      ? 'Laisser vide pour conserver la clé actuelle'
      : 'sk-or-…';
  }

  save(): void {
    if (this.form.invalid) {
      return;
    }
    const v = this.form.getRawValue();
    const body: UpdateOpenRouterProviderRequest = {
      isEnabled: v.isEnabled,
      displayName: v.displayName || null,
      baseUrl: v.baseUrl || null,
      apiKey: v.apiKey?.trim() ? v.apiKey.trim() : null
    };
    this.saving.set(true);
    this.aiChat.updateOpenRouterSettings(body).subscribe({
      next: s => {
        this.settings.set(s);
        this.defaultBaseUrl.set(s.defaultBaseUrl);
        this.form.patchValue({ apiKey: '' });
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: 'Succès', detail: 'Configuration enregistrée.' });
      },
      error: () => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: 'Enregistrement impossible. Vérifiez les champs et la clé API.'
        });
      }
    });
  }
}
