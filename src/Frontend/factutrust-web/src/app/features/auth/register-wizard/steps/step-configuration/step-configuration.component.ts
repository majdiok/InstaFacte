import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InputSwitchModule } from 'primeng/inputswitch';
import { FormsModule } from '@angular/forms';
import { AppModule } from '@core/models/app-module';
import { ModuleCatalogEntry, RegistrationCatalogService } from '../../registration-catalog';

@Component({
  selector: 'app-step-configuration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, InputSwitchModule],
  templateUrl: './step-configuration.component.html',
  styleUrl: './step-configuration.component.scss'
})
export class StepConfigurationComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() segment: string | null = '';
  @Input() domain: string | null = '';

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

  get profileLabel(): string {
    const segmentLabel = this.catalog.segmentLabel(this.segment);
    const domainLabel = this.catalog.domainLabel(this.domain);
    return [segmentLabel, domainLabel].filter(Boolean).join(' · ');
  }

  enabledModuleIds(): AppModule[] {
    return this.form.get('enabledModules')?.value ?? [];
  }

  isModuleEnabled(id: AppModule): boolean {
    return this.enabledModuleIds().includes(id);
  }

  toggleModule(id: AppModule): void {
    this.moduleToggled.emit(id);
  }

  onReset(): void {
    this.resetToRecommendations.emit();
  }
}
