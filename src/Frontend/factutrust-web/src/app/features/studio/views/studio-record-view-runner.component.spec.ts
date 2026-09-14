import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { environment } from '@environments/environment';
import { MessageService } from 'primeng/api';
import { StudioRecordViewRunnerComponent } from './studio-record-view-runner.component';
import { CustomRecordViewDto, RecordViewDefinition, RecordViewRunResultDto } from './studio-record-views.models';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';

const definition: RecordViewDefinition = {
  columns: [{ fieldKey: 'nom', hidden: false }],
  filters: [],
  sort: [],
  searchEnabled: true,
  pageSize: 25
};

const view: CustomRecordViewDto = {
  id: 'v1',
  key: 'v1',
  displayName: 'Actifs',
  mode: 'List',
  definition,
  isDefault: false,
  isActive: true,
  rowVersion: 'AAA',
  updatedAt: '2026-01-01T00:00:00Z'
};

const fields: CustomField[] = [
  { id: 'f1', key: 'nom', label: 'Nom', fieldType: 0, isRequired: false, isActive: true } as CustomField
];

describe('StudioRecordViewRunnerComponent', () => {
  let fixture: ComponentFixture<StudioRecordViewRunnerComponent>;
  let component: StudioRecordViewRunnerComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StudioRecordViewRunnerComponent, HttpClientTestingModule],
      providers: [MessageService]
    }).compileComponents();
    fixture = TestBed.createComponent(StudioRecordViewRunnerComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function setInputs(v: CustomRecordViewDto | null = view): void {
    fixture.componentRef.setInput('entityKey', 'interventions');
    fixture.componentRef.setInput('allFields', fields);
    fixture.componentRef.setInput('view', v);
    fixture.detectChanges();
  }

  it('exécute la vue au montage (POST /run) et affiche les lignes reçues', () => {
    setInputs();
    const req = httpMock.expectOne(`${environment.apiUrl}/studio/records/interventions/views/v1/run`);
    expect(req.request.method).toBe('POST');
    const result: RecordViewRunResultDto = {
      mode: 'List', items: [{ id: 'r1', data: { nom: 'X' }, createdAt: '', updatedAt: '' }],
      total: 1, page: 1, pageSize: 25, truncated: false
    };
    req.flush({ success: true, data: result, message: null, errors: [] });
    fixture.detectChanges();

    expect(component['result']()?.total).toBe(1);
    expect(fixture.debugElement.query(By.css('app-dynamic-table'))).not.toBeNull();
  });

  it('affiche le bandeau de troncature quand truncated=true', () => {
    setInputs();
    const req = httpMock.expectOne(`${environment.apiUrl}/studio/records/interventions/views/v1/run`);
    const result: RecordViewRunResultDto = { mode: 'List', items: [], total: 0, page: 1, pageSize: 25, truncated: true };
    req.flush({ success: true, data: result, message: null, errors: [] });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.runner-banner'))).not.toBeNull();
  });

  it('affiche un état d’erreur si la requête échoue', () => {
    setInputs();
    const req = httpMock.expectOne(`${environment.apiUrl}/studio/records/interventions/views/v1/run`);
    req.flush({ message: 'erreur' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('app-empty-state'))).not.toBeNull();
  });

  it('affiche un panneau « Bientôt » pour les modes Kanban et Calendrier, sans appel réseau', () => {
    setInputs({ ...view, mode: 'Kanban' });
    httpMock.expectNone(`${environment.apiUrl}/studio/records/interventions/views/v1/run`);

    expect(fixture.debugElement.query(By.css('.runner-soon'))?.nativeElement.textContent).toContain('Kanban');
  });
});
