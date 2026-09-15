import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import { AuthService } from '@core/services/auth.service';
import { StudioRecordFormComponent } from './studio-record-form.component';
import { EntityRelationDto } from './relations/studio-relations.models';

const API = `${environment.apiUrl}/studio/records/interventions`;

const m2m: EntityRelationDto = {
  kind: 'many_to_many',
  sourceEntityId: 'e1', sourceEntityKey: 'interventions', sourceLabel: 'Interventions',
  targetEntityId: 'e2', targetEntityKey: 'techniciens', targetLabel: 'Techniciens',
  fieldId: 'f1', fieldKey: 'intervention_id', isRequired: true, isUnique: false,
  junctionEntityId: 'j1', junctionEntityKey: 'intervention_technicien', junctionTargetFieldKey: 'technicien_id'
};
const m2o: EntityRelationDto = { ...m2m, kind: 'many_to_one', junctionEntityKey: null, junctionTargetFieldKey: null };

const schema = (relations: EntityRelationDto[] | null) => ({
  entity: { id: 'e1', key: 'interventions', displayName: 'Interventions' },
  fields: [{ id: 'f1', key: 'nom', label: 'Nom', fieldType: 0, isRequired: false, isActive: true }],
  form: { sections: [] },
  relations,
  views: []
});

describe('StudioRecordFormComponent — onglets Fiche / Liés (2.5e)', () => {
  let fixture: ComponentFixture<StudioRecordFormComponent>;
  let component: StudioRecordFormComponent;
  let httpMock: HttpTestingController;

  function setup(recordId: string | null, relations: EntityRelationDto[] | null, canWrite = true): void {
    TestBed.configureTestingModule({
      imports: [StudioRecordFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AuthService, useValue: { hasPermission: () => canWrite } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(recordId ? { key: 'interventions', id: recordId } : { key: 'interventions' }) } } }
      ]
    });
    fixture = TestBed.createComponent(StudioRecordFormComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne(`${API}/schema`).flush({ success: true, data: schema(relations), message: null, errors: [] });
    if (recordId) {
      httpMock.expectOne(`${API}/${recordId}`).flush({ success: true, data: { id: recordId, data: { nom: 'X' }, createdAt: '', updatedAt: '' }, message: null, errors: [] });
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('sans relation N-N ⇒ pas d\'onglets, formulaire seul', () => {
    setup('r1', []);
    expect(component.showTabs()).toBeFalse();
    expect(fixture.debugElement.query(By.css('app-studio-record-tabs'))).toBeNull();
    expect(fixture.debugElement.query(By.css('app-dynamic-form'))).not.toBeNull();
  });

  it('sans relations dans le schéma (null) ⇒ pas d\'onglets', () => {
    setup('r1', null);
    expect(component.showTabs()).toBeFalse();
  });

  it('en création (recordId null) ⇒ pas d\'onglets même avec N-N', () => {
    setup(null, [m2m]);
    expect(component.recordId).toBeNull();
    expect(component.showTabs()).toBeFalse();
    expect(fixture.debugElement.query(By.css('app-studio-record-tabs'))).toBeNull();
  });

  it('en édition avec N-N ⇒ onglets Fiche puis Liés — <cible>, formulaire actif par défaut', () => {
    setup('r1', [m2o, m2m]);
    expect(component.showTabs()).toBeTrue();
    expect(component.tabs().map(t => t.key)).toEqual(['form', 'linked:intervention_technicien']);
    expect(component.tabs()[1].label).toBe('Liés — Techniciens');
    expect(component.activeTab()).toBe('form');
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('app-studio-record-tabs'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('app-dynamic-form'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('app-studio-linked-records-tab'))).toBeNull();

    component.activeTab.set('linked:intervention_technicien');
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('app-dynamic-form'))).toBeNull();
    expect(fixture.debugElement.query(By.css('app-studio-linked-records-tab'))).not.toBeNull();
    expect(component.activeRelation()?.targetEntityKey).toBe('techniciens');
    // L'onglet Liés monte ses requêtes : on les vide (jonction + cibles ×2).
    httpMock.expectOne(`${API.replace('/interventions','')}/intervention_technicien?page=1&pageSize=50&filterField=intervention_id&filterValue=r1`)
      .flush({ success: true, data: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 }, message: null, errors: [] });
    httpMock.match(r => r.urlWithParams.startsWith(`${API.replace('/interventions','')}/techniciens?page=1`)).forEach(req => {
      if (!req.cancelled) req.flush({ success: true, data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }, message: null, errors: [] });
    });
  });

});
