import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';
import { StudioRecordViewDesignerComponent, slugifyViewKey } from './studio-record-view-designer.component';
import { CustomRecordViewDto, RECORD_VIEW_LIMITS } from './studio-record-views.models';
import { STUDIO_RUNTIME_LABELS } from '../shared/studio-runtime-labels';

const API = `${environment.apiUrl}/studio/records/interventions`;

const fields: CustomField[] = [
  { id: 'f1', key: 'nom', label: 'Nom', fieldType: 0, isRequired: true, isActive: true } as CustomField,
  { id: 'f2', key: 'statut', label: 'Statut', fieldType: 7, isRequired: false, isActive: true, options: [{ value: 'a', label: 'A' }] } as unknown as CustomField,
  { id: 'f3', key: 'debut', label: 'Début', fieldType: 5, isRequired: false, isActive: true } as CustomField,
  { id: 'f4', key: 'ancien', label: 'Ancien', fieldType: 0, isRequired: false, isActive: false } as CustomField
];

const schema = { entity: { id: 'e1', key: 'interventions', displayName: 'Interventions' }, fields, form: { sections: [] }, relations: [], views: [] };

const existingView: CustomRecordViewDto = {
  id: 'v1',
  key: 'actives',
  displayName: 'Actives',
  mode: 'List',
  definition: { columns: [{ fieldKey: 'nom', hidden: false }], filters: [], sort: [{ fieldKey: 'nom', descending: false }], searchEnabled: true, pageSize: 50 },
  isDefault: false,
  isActive: true,
  rowVersion: 'AAA=',
  updatedAt: '2026-01-01T00:00:00Z'
};

describe('StudioRecordViewDesignerComponent', () => {
  let fixture: ComponentFixture<StudioRecordViewDesignerComponent>;
  let component: StudioRecordViewDesignerComponent;
  let httpMock: HttpTestingController;
  let router: Router;
  let confirmSpy: jasmine.Spy;

  function setup(viewId: string | null, canDesign = true): void {
    confirmSpy = jasmine.createSpy('confirm');
    TestBed.configureTestingModule({
      imports: [StudioRecordViewDesignerComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ConfirmationService, useValue: { confirm: confirmSpy } },
        { provide: AuthService, useValue: { hasPermission: () => canDesign } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(viewId ? { key: 'interventions', viewId } : { key: 'interventions' }) } }
        }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(StudioRecordViewDesignerComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(`${API}/schema`).flush({ success: true, data: schema, message: null, errors: [] });
    if (viewId) {
      httpMock.expectOne(`${API}/views/${viewId}`).flush({ success: true, data: existingView, message: null, errors: [] });
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('création : charge le schéma, propose les champs actifs et désactive Enregistrer tant que nom/clé sont invalides', () => {
    setup(null);
    expect(component.entityName()).toBe('Interventions');
    expect(component.activeFields().map(f => f.key)).toEqual(['nom', 'statut', 'debut']);
    expect(component.availableColumnOptions().map(o => o.key)).toEqual(['nom', 'statut', 'debut', 'createdAt', 'updatedAt']);
    expect(component.canSave()).toBeFalse();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-delete"]'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain(STUDIO_RUNTIME_LABELS.designer.previewHint);

    component.onNameChange('Vue à planifier 2026');
    expect(component.key()).toBe('vue_a_planifier_2026');
    expect(component.canSave()).toBeTrue();

    component.onKeyChange('1bad');
    expect(component.keyValid()).toBeFalse();
    expect(component.canSave()).toBeFalse();
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-key-invalid"]'))).not.toBeNull();

    component.onNameChange('Autre nom');
    expect(component.key()).toBe('1bad');
    expect(slugifyViewKey('2 roues')).toBe('v_2_roues');
  });

  it('borne les colonnes à RECORD_VIEW_LIMITS.maxColumns et les tris à maxSorts', () => {
    setup(null);
    component.fields.set(Array.from({ length: 30 }, (_, i) =>
      ({ id: `f${i}`, key: `c${i}`, label: `C${i}`, fieldType: 0, isRequired: false, isActive: true } as CustomField)));
    for (let i = 0; i < 30; i++) component.addColumn(`c${i}`);
    expect(component.columns().length).toBe(RECORD_VIEW_LIMITS.maxColumns);
    component.addColumn('c0');
    expect(component.columns().length).toBe(RECORD_VIEW_LIMITS.maxColumns);

    for (let i = 0; i < 5; i++) component.addSort(`c${i}`);
    expect(component.sort().length).toBe(RECORD_VIEW_LIMITS.maxSorts);
    expect(component.availableSortOptions().map(o => o.key)).not.toContain('c0');

    component.moveColumn(0, 1);
    expect(component.columns()[0].fieldKey).toBe('c1');
    component.toggleColumnHidden(0);
    expect(component.columns()[0].hidden).toBeTrue();
    component.toggleSortDirection(0);
    expect(component.sort()[0].descending).toBeTrue();
    component.removeColumn(0);
    component.removeSort(0);
    expect(component.columns().length).toBe(RECORD_VIEW_LIMITS.maxColumns - 1);
    expect(component.sort().length).toBe(RECORD_VIEW_LIMITS.maxSorts - 1);

    component.pageSize.set(RECORD_VIEW_LIMITS.maxPageSize + 1);
    expect(component.pageSizeValid()).toBeFalse();
  });

  it('POST /views en création puis navigue vers la liste avec ?view=<id>', () => {
    setup(null);
    component.onNameChange('Actives');
    component.addColumn('nom');
    component.addSort('nom');
    component.pageSize.set(50);
    component.save();

    const req = httpMock.expectOne(`${API}/views`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      key: 'actives',
      displayName: 'Actives',
      mode: 'List',
      definition: { columns: [{ fieldKey: 'nom', hidden: false }], filters: [], sort: [{ fieldKey: 'nom', descending: false }], kanban: null, calendar: null, searchEnabled: true, pageSize: 50 },
      isDefault: false,
      rowVersion: undefined
    });
    req.flush({ success: true, data: { ...existingView, id: 'new-id' }, message: null, errors: [] }, { status: 201, statusText: 'Created' });
    expect(router.navigate).toHaveBeenCalledWith(['/studio/d', 'interventions'], { queryParams: { view: 'new-id' } });
    expect(component.saving()).toBeFalse();
  });

  it('409 en création ⇒ message duplicateKey ; 400 « Limite du plan… » ⇒ message planLimit ; autre 400 ⇒ message serveur', () => {
    setup(null);
    component.onNameChange('Actives');

    component.save();
    httpMock.expectOne(`${API}/views`).flush({ success: false, data: null, message: 'Conflict', errors: [] }, { status: 409, statusText: 'Conflict' });
    expect(component.error()?.message).toBe(STUDIO_RUNTIME_LABELS.designer.duplicateKey);
    expect(component.error()?.stale).toBeFalse();
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-error"]')).nativeElement.textContent).toContain(STUDIO_RUNTIME_LABELS.designer.duplicateKey);
    expect(fixture.debugElement.query(By.css('[data-testid="designer-reload"]'))).toBeNull();

    component.save();
    httpMock.expectOne(`${API}/views`).flush(
      { success: false, data: null, message: 'Limite du plan atteinte : 20 vues par table maximum.', errors: [] }, { status: 400, statusText: 'Bad Request' });
    expect(component.error()?.message).toBe(STUDIO_RUNTIME_LABELS.designer.planLimit);

    component.save();
    httpMock.expectOne(`${API}/views`).flush(
      { success: false, data: null, message: 'Une vue accepte au plus 25 colonnes.', errors: [] }, { status: 400, statusText: 'Bad Request' });
    expect(component.error()?.message).toBe('Une vue accepte au plus 25 colonnes.');

    component.save();
    httpMock.expectOne(`${API}/views`).flush({ success: false, data: null, message: null, errors: [] }, { status: 404, statusText: 'Not Found' });
    expect(router.navigate).toHaveBeenCalledWith(['/studio/d', 'interventions']);
  });

  it('édition : clé désactivée, PUT avec rowVersion ; 409 ⇒ staleConflict + Recharger', async () => {
    setup('v1');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.editing).toBeTrue();
    expect(component.displayName()).toBe('Actives');
    expect(component.pageSize()).toBe(50);
    const keyInput: HTMLInputElement = fixture.debugElement.query(By.css('[data-testid="designer-key"]')).nativeElement;
    expect(keyInput.disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain(STUDIO_RUNTIME_LABELS.designer.keyImmutable);
    expect(fixture.debugElement.query(By.css('[data-testid="designer-delete"]'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-set-default"]'))).not.toBeNull();

    component.onNameChange('Actives (modifiée)');
    expect(component.key()).toBe('actives');
    component.save();
    const req = httpMock.expectOne(`${API}/views/v1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.rowVersion).toBe('AAA=');
    expect(req.request.body.key).toBe('actives');
    req.flush({ success: false, data: null, message: 'Conflict', errors: [] }, { status: 409, statusText: 'Conflict' });
    expect(component.error()?.message).toBe(STUDIO_RUNTIME_LABELS.designer.staleConflict);
    expect(component.error()?.stale).toBeTrue();
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-reload"]'))).not.toBeNull();

    component.reload();
    httpMock.expectOne(`${API}/schema`).flush({ success: true, data: schema, message: null, errors: [] });
    httpMock.expectOne(`${API}/views/v1`).flush({ success: true, data: { ...existingView, rowVersion: 'BBB=' }, message: null, errors: [] });
    expect(component.error()).toBeNull();
    expect(component.view()?.rowVersion).toBe('BBB=');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('supprimer ⇒ confirmation puis DELETE /views/{id} et retour liste ; vue par défaut ⇒ POST /default', () => {
    setup('v1');
    component.delete();
    expect(confirmSpy).toHaveBeenCalled();
    const options = confirmSpy.calls.mostRecent().args[0];
    expect(options.acceptLabel).toBe(STUDIO_RUNTIME_LABELS.designer.delete);
    options.accept();
    const del = httpMock.expectOne(`${API}/views/v1`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null, { status: 204, statusText: 'No Content' });
    expect(router.navigate).toHaveBeenCalledWith(['/studio/d', 'interventions']);

    component.setDefault();
    const def = httpMock.expectOne(`${API}/views/v1/default`);
    expect(def.request.method).toBe('POST');
    def.flush(null, { status: 204, statusText: 'No Content' });
    httpMock.expectOne(`${API}/views/v1`).flush({ success: true, data: { ...existingView, isDefault: true }, message: null, errors: [] });
    expect(component.isDefault()).toBeTrue();
  });

  it('lecture seule : sans studio.designForms, Enregistrer est désactivé et les actions de tête absentes', () => {
    setup('v1', false);
    expect(component.canDesign()).toBeFalse();
    expect(component.canSave()).toBeFalse();
    expect(fixture.debugElement.query(By.css('[data-testid="designer-delete"]'))).toBeNull();
    component.delete();
    expect(confirmSpy).not.toHaveBeenCalled();
  });
});
