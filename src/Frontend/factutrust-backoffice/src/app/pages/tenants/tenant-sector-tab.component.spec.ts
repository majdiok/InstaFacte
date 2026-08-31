import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { MessageService } from 'primeng/api';

import { TenantSectorTabComponent } from './tenant-sector-tab.component';
import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformTenantSectorService } from '@core/services/platform-tenant-sector.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type { SectorReconfigurationPreviewDto, SectorRuleSetAdminDto } from '@core/models/sector-rules.models';

describe('TenantSectorTabComponent', () => {
  let fixture: ComponentFixture<TenantSectorTabComponent>;
  let component: TenantSectorTabComponent;
  let rulesApi: jasmine.SpyObj<PlatformSectorRulesService>;
  let sectorApi: jasmine.SpyObj<PlatformTenantSectorService>;
  let permissions: jasmine.SpyObj<PlatformPermissionsService>;

  const catalog: SectorRuleSetAdminDto = {
    version: 12,
    segments: [
      { id: 'seg-commerce', code: 'commerce', labelFr: 'Commerce & négoce', descriptionFr: 'Commerce', iconKey: 'shopping-cart', sortOrder: 1, isActive: true },
      { id: 'seg-btp', code: 'btp-construction', labelFr: 'BTP & Construction', descriptionFr: 'BTP', iconKey: 'hard-hat', sortOrder: 3, isActive: true }
    ],
    domains: [
      { id: 'dom-artisanat', code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1, isActive: true },
      { id: 'dom-autre', code: 'autre', labelFr: 'Autre domaine', sortOrder: 9, isActive: true }
    ],
    segmentDomains: [
      { id: 'sd-1', segmentId: 'seg-commerce', domainId: 'dom-artisanat', sortOrder: 1, isActive: true },
      { id: 'sd-2', segmentId: 'seg-commerce', domainId: 'dom-autre', sortOrder: 2, isActive: true },
      { id: 'sd-3', segmentId: 'seg-btp', domainId: 'dom-artisanat', sortOrder: 1, isActive: true },
      { id: 'sd-4', segmentId: 'seg-btp', domainId: 'dom-autre', sortOrder: 2, isActive: true }
    ],
    moduleRules: [],
    moduleDependencies: [],
    settings: [],
    templates: []
  };

  function setup(hasApplyPermission: boolean): void {
    rulesApi = jasmine.createSpyObj('PlatformSectorRulesService', ['getAll']);
    rulesApi.getAll.and.returnValue(of({ success: true, data: catalog, message: null, errors: [] }) as never);

    sectorApi = jasmine.createSpyObj('PlatformTenantSectorService', ['preview', 'apply']);

    permissions = jasmine.createSpyObj('PlatformPermissionsService', ['has']);
    permissions.has.and.callFake((perm: string) => hasApplyPermission && perm === PlatformPermission.SectorRulesApply);

    TestBed.configureTestingModule({
      imports: [TenantSectorTabComponent],
      providers: [
        { provide: PlatformSectorRulesService, useValue: rulesApi },
        { provide: PlatformTenantSectorService, useValue: sectorApi },
        { provide: PlatformPermissionsService, useValue: permissions },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantSectorTabComponent);
    component = fixture.componentInstance;
    component.tenantId = 'tenant-1';
    component.companyName = 'Bâtiment Moderne du Sahel SARL';
    component.companySegment = 'commerce';
    component.businessDomain = 'artisanat';
    fixture.detectChanges();
  }

  it('renders read-only (no Modifier button) when the user lacks SectorRulesApply', () => {
    setup(false);
    expect(component.canApply()).toBeFalse();
  });

  it('exposes canApply=true and allows opening the picker when the user has SectorRulesApply', () => {
    setup(true);
    expect(component.canApply()).toBeTrue();
    component.openPicker();
    expect(component.pickerOpen()).toBeTrue();
  });

  it('resolves segment/domain labels from the loaded catalog (fallback to raw code)', () => {
    setup(true);
    expect(component.segmentLabel('commerce')).toBe('Commerce & négoce');
    expect(component.domainLabel('artisanat')).toBe('Artisanat');
    expect(component.segmentLabel('unknown-code')).toBe('unknown-code');
  });

  it('filters domain options by the GUID segment↔domain associations', () => {
    setup(true);
    component.pickedSegment = 'btp-construction';
    const options = component.domainOptionsForSegment();
    expect(options.map(o => o.value)).toEqual(['artisanat', 'autre']);
  });

  it('runPreview() populates preview() from the service response and renders module diff labels', () => {
    setup(true);
    const previewDto: SectorReconfigurationPreviewDto = {
      currentSegment: 'commerce',
      currentDomain: 'artisanat',
      targetSegment: 'btp-construction',
      targetDomain: 'autre',
      users: [
        { userId: 'u1', currentEnabledModuleIds: [9], targetEnabledModuleIds: [16], modulesToEnable: [16], modulesToDisable: [9] }
      ],
      templates: [],
      settings: [],
      warnings: []
    };
    sectorApi.preview.and.returnValue(of({ success: true, data: previewDto, message: null, errors: [] }) as never);

    component.pickedSegment = 'btp-construction';
    component.pickedDomain = 'autre';
    component.runPreview();

    expect(component.preview()).toEqual(previewDto);
    expect(component.enabledModuleIds(previewDto)).toEqual([16]);
    expect(component.disabledModuleIds(previewDto)).toEqual([9]);
  });

  it('Apply stays disabled until the confirmation checkbox is ticked, even after a preview', () => {
    setup(true);
    const previewDto: SectorReconfigurationPreviewDto = {
      currentSegment: 'commerce',
      currentDomain: 'artisanat',
      targetSegment: 'btp-construction',
      targetDomain: 'autre',
      users: [],
      templates: [],
      settings: [],
      warnings: []
    };
    sectorApi.preview.and.returnValue(of({ success: true, data: previewDto, message: null, errors: [] }) as never);
    component.pickedSegment = 'btp-construction';
    component.runPreview();

    expect(component.confirmed).toBeFalse();
    component.openApplyConfirm();
    expect(component.applyConfirmVisible).toBeFalse();

    component.confirmed = true;
    component.openApplyConfirm();
    expect(component.applyConfirmVisible).toBeTrue();
  });
});
