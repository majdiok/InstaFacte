import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '@environments/environment';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';
import { StudioLinkedRecordsService, primaryLabel } from './studio-linked-records.service';
import { EntityRelationDto } from './studio-relations.models';
import { CustomRecord } from '../studio.models';

const rel: EntityRelationDto = {
  kind: 'many_to_many',
  sourceEntityId: 'e1', sourceEntityKey: 'interventions', sourceLabel: 'Interventions',
  targetEntityId: 'e2', targetEntityKey: 'techniciens', targetLabel: 'Techniciens',
  fieldId: 'f1', fieldKey: 'intervention_id', isRequired: true, isUnique: false,
  junctionEntityId: 'j1', junctionEntityKey: 'intervention_technicien',
  junctionTargetFieldId: 'f2', junctionTargetFieldKey: 'technicien_id'
};

const fields: CustomField[] = [
  { id: 'f1', key: 'code', label: 'Code', fieldType: 0, isRequired: false, isActive: true } as CustomField,
  { id: 'f2', key: 'nom', label: 'Nom', fieldType: 0, isRequired: false, isActive: true } as CustomField
];

const record: CustomRecord = { id: 'r123456789', data: { code: '', nom: '  Ben Ali  ' }, createdAt: '', updatedAt: '' };

describe('StudioLinkedRecordsService', () => {
  let service: StudioLinkedRecordsService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/studio/records`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(StudioLinkedRecordsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('liste via GET records/{jonction} avec filterField=fieldKey et filterValue=recordId', () => {
    service.listLinks(rel, 'rec-1', 2, 50).subscribe(res => expect(res.success).toBeTrue());
    const req = httpMock.expectOne(`${base}/intervention_technicien?page=2&pageSize=50&filterField=intervention_id&filterValue=rec-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: { items: [], page: 2, pageSize: 50, totalCount: 0, totalPages: 0 }, message: null, errors: [] });
  });

  it('borne pageSize à 200 et exige junctionEntityKey', () => {
    service.listLinks(rel, 'rec-1', 1, 999).subscribe();
    httpMock.expectOne(`${base}/intervention_technicien?page=1&pageSize=200&filterField=intervention_id&filterValue=rec-1`)
      .flush({ success: true, data: { items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 0 }, message: null, errors: [] });
    expect(() => service.listLinks({ ...rel, junctionEntityKey: null }, 'rec-1')).toThrow();
  });

  it('link ⇒ POST records/{jonction} avec les deux clés de la jonction', () => {
    service.link(rel, 'rec-1', 'rec-9').subscribe();
    const req = httpMock.expectOne(`${base}/intervention_technicien`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ data: { intervention_id: 'rec-1', technicien_id: 'rec-9' } });
    req.flush({ success: true, data: { id: 'j9', data: {}, createdAt: '', updatedAt: '' }, message: null, errors: [] });
  });

  it('unlink ⇒ DELETE records/{jonction}/{id}', () => {
    service.unlink(rel, 'j9').subscribe();
    const req = httpMock.expectOne(`${base}/intervention_technicien/j9`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('searchTargets ⇒ GET records/{cible} avec search, page=1, pageSize=20', () => {
    service.searchTargets(rel, 'ben').subscribe();
    const req = httpMock.expectOne(`${base}/techniciens?page=1&pageSize=20&search=ben`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }, message: null, errors: [] });
  });

  it('primaryLabel : première valeur texte du data selon les champs, sinon id tronqué', () => {
    expect(primaryLabel(record, fields)).toBe('Ben Ali');
    expect(primaryLabel({ ...record, data: { nom: 'Nom sans champ' } }, fields)).toBe('Nom sans champ');
    expect(primaryLabel({ ...record, data: null }, fields)).toBe('r1234567');
    expect(primaryLabel({ ...record, data: { code: '  ', nom: 42 } }, fields)).toBe('r1234567');
  });
});
