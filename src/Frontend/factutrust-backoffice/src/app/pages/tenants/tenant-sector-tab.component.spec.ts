import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { MessageService } from 'primeng/api';

import { TenantSectorTabComponent } from './tenant-sector-tab.component';
import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformTenantSectorService } from '@core/services/platform-tenant-sector.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type { SectorReconfigurationPreviewDto } from '@core/models/sector-rules.models';

describe('TenantSectorTabComponent', () => {
  let fixture: ComponentFixture<TenantSectorTabComponent>;
  let component: TenantSectorTabComponent;
  let rulesApi: jasmine.SpyObj<PlatformSectorRulesService>;
  let sectorApi: jasmine.SpyObj<PlatformTenantSectorService>;
  let permissions: jasmine.SpyObj<PlatformPermissionsService>;

  const catalog = {
    version: 12,
    updatedAtUtc: null,
    updatedBy: null,
    useDbRules: true,
    segments: [
      { id: 's1', code: 'commerce', label: 'Commerce & négoce', isActive: true, sortOrder: 1 },
      { id: 's2', code: 'btp', label: 'BTP & construction', isActive: true, sortOrder: 3 }
    ],
    domains: [
      { id: 'd1', code: 'vente-detail', label: 'Vente au détail', isActive: true, sortOrder: 1 },
      { id: 'd2', code: 'construction', label: 'Construction & gros œuvre', isActive: true, sortOrder: 5 }
    ],
    segmentDomains: [
      { segmentCode: 'commerce', domainCodes: ['vente-detail', 'autre'] },
      { segmentCode: 'btp', domainCodes: ['construction', 'autre'] }
    ],
    moduleRules: [],
    dependencies: [],
    defaultSettings: [],
    dataTemplates: []
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
    component.businessDomain = 'vente-detail';
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

  it('resolves segment/domain labels from the loaded catalog', () => {
    setup(true);
    expect(component.segmentLabel('commerce')).toBe('Commerce & négoce');
    expect(component.domainLabel('vente-detail')).toBe('Vente au détail');
    expect(component.segmentLabel('unknown-code')).toBe('unknown-code');
  });

  it('filters domain options by the segment associations', () => {
    setup(true);
    component.pickedSegment = 'btp';
    const options = component.domainOptionsForSegment();
    expect(options.map(o => o.value)).toEqual(['construction', 'autre'].filter(c => catalog.domains.some(d => d.code === c)));
  });

  it('runPreview() populates preview() from the service response and renders module diff labels', () => {
    setup(true);
    const previewDto: SectorReconfigurationPreviewDto = {
      currentSegment: 'commerce',
      currentDomain: 'vente-detail',
      targetSegment: 'btp',
      targetDomain: 'construction',
      users: [
        { userId: 'u1', currentEnabledModuleIds: [9], targetEnabledModuleIds: [16], modulesToEnable: [16], modulesToDisable: [9] }
      ],
      templates: [],
      settings: [],
      warnings: []
    };
    sectorApi.preview.and.returnValue(of({ success: true, data: previewDto, message: null, errors: [] }) as never);

    component.pickedSegment = 'btp';
    component.pickedDomain = 'construction';
    component.runPreview();

    expect(component.preview()).toEqual(previewDto);
    expect(component.enabledModuleIds(previewDto)).toEqual([16]);
    expect(component.disabledModuleIds(previewDto)).toEqual([9]);
  });

  it('Apply stays disabled until the confirmation checkbox is ticked, even after a preview', () => {
    setup(true);
    const previewDto: SectorReconfigurationPreviewDto = {
      currentSegment: 'commerce',
      currentDomain: 'vente-detail',
      targetSegment: 'btp',
      targetDomain: 'construction',
      users: [],
      templates: [],
      settings: [],
      warnings: []
    };
    sectorApi.preview.and.returnValue(of({ success: true, data: previewDto, message: null, errors: [] }) as never);
    component.pickedSegment = 'btp';
    component.runPreview();

    expect(component.confirmed).toBeFalse();
    component.openApplyConfirm();
    expect(component.applyConfirmVisible).toBeFalse();

    component.confirmed = true;
    component.openApplyConfirm();
    expect(component.applyConfirmVisible).toBeTrue();
  });
});
