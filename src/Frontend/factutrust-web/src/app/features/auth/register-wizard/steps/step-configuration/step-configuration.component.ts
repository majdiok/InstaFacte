import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InputSwitchModule } from 'primeng/inputswitch';
import { FormsModule } from '@angular/forms';
import { TooltipModule } from 'primeng/tooltip';
import { AppModule } from '@core/models/app-module';
import { ModuleCatalogEntry, RegistrationCatalogService } from '../../registration-catalog';

@Component({
  selector: 'app-step-configuration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, InputSwitchModule, TooltipModule],
  templateUrl: './step-configuration.component.html',
  styleUrl: './step-configuration.component.scss'
})
export class StepConfigurationComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() segment: string | null = '';
  @Input() domain: string | null = '';
  /** Modules just auto-enabled as a hard dependency of the last toggle (plan WP-F3, wizard's `lastAutoEnabled`). */
  @Input() autoEnabledIds: AppModule[] = [];

  @Output() moduleToggled = new EventEmitter<AppModule>();
  @Output() resetToRecommendations = new EventEmitter<void>();

  readonly catalog = inject(RegistrationCatalogService);

  get coreModules(): ModuleCatalogEntry[] {
    return this.catalog.modules.filter(m => this.catalog.isCoreModule(m.id));
  }

  get recommendedModules(): ModuleCatalogEntry[] {
    const recommended = new Set(this.catalog.recommendedModules(this.segment, this.domain));
    return this.catalog.modules.filter(m => !this.catalog.isCoreModule(m.id) && recommended.has(m.id));
  }

  get optionalModules(): ModuleCatalogEntry[] {
    const optional = new Set(this.catalog.optionalModules(this.segment, this.domain));
    return this.catalog.modules.filter(m => optional.has(m.id));
  }

  /** Premium modules locked on the Free plan — rendered as a non-toggleable "Plan supérieur" group. */
  get premiumModules(): ModuleCatalogEntry[] {
    const premium = new Set(this.catalog.premiumModules(this.segment, this.domain));
    return this.catalog.modules.filter(m => premium.has(m.id));
  }

  get profileLabel(): string {
    const segmentLabel = this.catalog.segmentLabel(this.segment);
    const domainLabel = this.catalog.domainLabel(this.domain);
    return [segmentLabel, domainLabel].filter(Boolean).join(' · ');
  }

  /** Discreet indicator (plan 2.1): the remote catalog was unreachable after retries. */
  get usedStaticFallback(): boolean {
    return this.catalog.usedStaticFallback();
  }

  enabledModuleIds(): AppModule[] {
    return this.form.get('enabledModules')?.value ?? [];
  }

  isModuleEnabled(id: AppModule): boolean {
    return this.enabledModuleIds().includes(id);
  }

  /** A module required (transitively) by another currently-enabled module cannot be turned off (plan WP-F3). */
  isLockedByDependency(id: AppModule): boolean {
    return this.catalog.dependentsOf(id, this.enabledModuleIds()).length > 0;
  }

  dependencyLockLabel(id: AppModule): string {
    const dependents = this.catalog.dependentsOf(id, this.enabledModuleIds());
    const labels = dependents.map(d => this.catalog.moduleLabel(d)).join(', ');
    return `Requis par ${labels}`;
  }

  wasAutoEnabled(id: AppModule): boolean {
    return this.autoEnabledIds.includes(id);
  }

  autoEnabledHintLabel(id: AppModule): string {
    const dependents = this.catalog.dependentsOf(id, this.enabledModuleIds());
    const labels = dependents.map(d => this.catalog.moduleLabel(d)).join(', ');
    return labels ? `Activé automatiquement (requis par ${labels})` : 'Activé automatiquement';
  }

  toggleModule(id: AppModule): void {
    this.moduleToggled.emit(id);
  }

  onReset(): void {
    this.resetToRecommendations.emit();
  }
}

