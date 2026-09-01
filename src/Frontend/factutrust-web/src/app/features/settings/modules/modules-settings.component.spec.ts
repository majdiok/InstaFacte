import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AppModule } from '@core/models/app-module';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { CompanyModuleDto, CompanyModulesService } from '@core/services/company-modules.service';
import { ModulesSettingsComponent } from './modules-settings.component';

function mod(partial: Partial<CompanyModuleDto> & { id: AppModule }): CompanyModuleDto {
  return {
    code: 'mod',
    labelFr: 'Module',
    isCore: false,
    isEnabled: false,
    allowedByPlan: true,
    requires: [],
    requiredBy: [],
    recommendedForSector: false,
    ...partial
  };
}

describe('ModulesSettingsComponent', () => {
  function setup(modules: CompanyModuleDto[], planCode = 'STANDARD') {
    const modulesService = {
      getModules: jasmine
        .createSpy('getModules')
        .and.returnValue(of({ success: true, data: { planCode, modules }, message: null, errors: [] })),
      updateModules: jasmine
        .createSpy('updateModules')
        .and.returnValue(
          of({ success: true, data: { enabledModuleIds: [], warnings: [] }, message: null, errors: [] })
        )
    };
    const authService = {
      user: () => ({ companySegment: 'commerce' }),
      refreshUserProfile: jasmine.createSpy('refreshUserProfile').and.returnValue(of({ success: true }))
    };
    const toastService = { add: jasmine.createSpy('add') };

    TestBed.configureTestingModule({
      imports: [ModulesSettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: CompanyModulesService, useValue: modulesService },
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toastService }
      ]
    });

    const fixture = TestBed.createComponent(ModulesSettingsComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, modulesService, authService, toastService };
  }

  it('classe les modules en Cœur / Recommandés / Autres', () => {
    const { component } = setup([
      mod({ id: AppModule.Administration, isCore: true, isEnabled: true }),
      mod({ id: AppModule.Stock, recommendedForSector: true }),
      mod({ id: AppModule.CRM })
    ]);

    expect(component.coreModules().map(m => m.id)).toEqual([AppModule.Administration]);
    expect(component.recommendedModules().map(m => m.id)).toEqual([AppModule.Stock]);
    expect(component.otherModules().map(m => m.id)).toEqual([AppModule.CRM]);
  });

  it('un module cœur ne peut pas être basculé', () => {
    const { component } = setup([mod({ id: AppModule.Administration, isCore: true, isEnabled: true })]);

    component.toggleModule(component.coreModules()[0]);

    expect(component.isSelected(AppModule.Administration)).toBe(true);
    expect(component.isDirty()).toBe(false);
  });

  it('un module verrouillé par le plan (allowedByPlan=false) ne peut pas être activé', () => {
    const { component } = setup([mod({ id: AppModule.AI, allowedByPlan: false })]);
    const aiModule = component.otherModules()[0];

    component.toggleModule(aiModule);

    expect(component.isSelected(AppModule.AI)).toBe(false);
    expect(component.isLockedByPlan(aiModule)).toBe(true);
  });

  it('activer un module active automatiquement ses dépendances (fermeture transitive) et affiche l’indice', () => {
    const { component } = setup([
      mod({ id: AppModule.Stock, requires: [] }),
      mod({ id: AppModule.Purchases, requires: [AppModule.Stock] }),
      mod({ id: AppModule.Accounting, requires: [AppModule.Purchases] })
    ]);

    const accounting = component.otherModules().find(m => m.id === AppModule.Accounting)!;
    component.toggleModule(accounting);

    expect(component.isSelected(AppModule.Accounting)).toBe(true);
    expect(component.isSelected(AppModule.Purchases)).toBe(true);
    expect(component.isSelected(AppModule.Stock)).toBe(true);
    expect(component.wasAutoEnabled(AppModule.Purchases)).toBe(true);
    expect(component.wasAutoEnabled(AppModule.Stock)).toBe(true);
    expect(component.wasAutoEnabled(AppModule.Accounting)).toBe(false);
  });

  it('désactiver un module requis par un autre module actif est bloqué', () => {
    const { component } = setup([
      mod({ id: AppModule.Stock, isEnabled: true, requiredBy: [AppModule.Purchases] }),
      mod({ id: AppModule.Purchases, isEnabled: true, requires: [AppModule.Stock] })
    ]);

    const stock = component.recommendedModules().concat(component.otherModules()).find(m => m.id === AppModule.Stock)!;
    expect(component.isLockedByDependency(AppModule.Stock)).toBe(true);

    component.toggleModule(stock);

    expect(component.isSelected(AppModule.Stock)).toBe(true);
    expect(component.dependencyLockLabel(AppModule.Stock)).toContain('Requis par');
  });

  it('save() appelle updateModules puis refreshUserProfile pour actualiser la navigation sans relogin', () => {
    const { component, modulesService, authService, toastService } = setup([
      mod({ id: AppModule.Stock, isEnabled: false })
    ]);

    component.toggleModule(component.otherModules()[0]);
    expect(component.isDirty()).toBe(true);

    component.save();

    expect(modulesService.updateModules).toHaveBeenCalledWith([AppModule.Stock]);
    expect(authService.refreshUserProfile).toHaveBeenCalled();
    expect(toastService.add).toHaveBeenCalled();
  });

  it("save() affiche les avertissements retournés par l'API en toast warn", () => {
    const { component, modulesService, toastService } = setup([mod({ id: AppModule.Stock })]);
    modulesService.updateModules.and.returnValue(
      of({
        success: true,
        data: { enabledModuleIds: [AppModule.Stock], warnings: ['Attention: dépendance recommandée manquante.'] },
        message: null,
        errors: []
      })
    );

    component.toggleModule(component.otherModules()[0]);
    component.save();

    expect(toastService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'warn' })
    );
  });
});
