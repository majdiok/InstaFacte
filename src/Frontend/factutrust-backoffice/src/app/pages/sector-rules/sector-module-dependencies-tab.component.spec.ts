import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';

import { SectorModuleDependenciesTabComponent } from './sector-module-dependencies-tab.component';
import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';

describe('SectorModuleDependenciesTabComponent', () => {
  let fixture: ComponentFixture<SectorModuleDependenciesTabComponent>;
  let component: SectorModuleDependenciesTabComponent;
  let api: jasmine.SpyObj<PlatformSectorRulesService>;
  let toast: jasmine.SpyObj<MessageService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj('PlatformSectorRulesService', ['createModuleDependency', 'deactivateModuleDependency']);
    toast = jasmine.createSpyObj('MessageService', ['add']);
    const permissions = jasmine.createSpyObj('PlatformPermissionsService', ['has']);
    permissions.has.and.returnValue(true);

    await TestBed.configureTestingModule({
      imports: [SectorModuleDependenciesTabComponent],
      providers: [
        { provide: PlatformSectorRulesService, useValue: api },
        { provide: PlatformPermissionsService, useValue: permissions },
        { provide: MessageService, useValue: toast }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SectorModuleDependenciesTabComponent);
    component = fixture.componentInstance;
    component.dependencies = [];
    fixture.detectChanges();
  });

  it('surfaces the backend French message via MessageService when the API rejects with a 400 cycle error', () => {
    api.createModuleDependency.and.returnValue(
      of({ success: false, data: null, message: 'Dépendance circulaire détectée entre modules.', errors: [] }) as never
    );
    component.newModuleId = 6;
    component.newRequiresModuleId = 7;

    component.add();

    expect(toast.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: 'Dépendance circulaire détectée entre modules.' })
    );
  });

  it('surfaces the backend message from an HTTP error response body', () => {
    api.createModuleDependency.and.returnValue(
      throwError(() => ({ error: { message: 'Dépendance circulaire détectée entre modules.' } }))
    );
    component.newModuleId = 6;
    component.newRequiresModuleId = 7;

    component.add();

    expect(toast.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: 'Dépendance circulaire détectée entre modules.' })
    );
  });

  it('canAdd() is false when module and requiresModule are equal or unset', () => {
    component.newModuleId = null;
    component.newRequiresModuleId = null;
    expect(component.canAdd()).toBeFalse();

    component.newModuleId = 6;
    component.newRequiresModuleId = 6;
    expect(component.canAdd()).toBeFalse();

    component.newModuleId = 6;
    component.newRequiresModuleId = 7;
    expect(component.canAdd()).toBeTrue();
  });
});
