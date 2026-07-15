import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { SubJournalsComponent } from './sub-journals.component';

describe('SubJournalsComponent', () => {
  let fixture: ComponentFixture<SubJournalsComponent>;
  let component: SubJournalsComponent;
  let httpMock: HttpTestingController;
  const base = `${environment.apiUrl}/accounting`;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubJournalsComponent, HttpClientTestingModule, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(SubJournalsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushJournal(journalCode: string): void {
    const req = httpMock.expectOne(r => r.url === `${base}/journal`);
    expect(req.request.params.get('journalCode')).toBe(journalCode);
    req.flush({ success: true, data: [] });
    fixture.detectChanges();
  }

  it('loads JV on init', () => {
    flushJournal('JV');
    expect(component.activeTabIndex).toBe(0);
    expect(component.activeJournalCode).toBe('JV');
  });

  it('loads JA when journal tab changes', () => {
    flushJournal('JV');

    component.onJournalTabChange({ index: 1, code: 'JA' });
    flushJournal('JA');

    expect(component.activeTabIndex).toBe(1);
    expect(component.activeJournalCode).toBe('JA');
  });

  it('reloads active journal when dates change', () => {
    flushJournal('JV');
    component.activeTabIndex = 5;

    component.onDatesChange();
    flushJournal('JIM');

    expect(component.activeJournalCode).toBe('JIM');
  });

  it('includes active JIM journal in AI payload', () => {
    flushJournal('JV');
    component.onJournalTabChange({ index: 5, code: 'JIM' });
    flushJournal('JIM');

    const payload = component.buildSubJournalsAnalyzePayload() as {
      filters: Record<string, unknown>;
    };

    expect(payload.filters['activeJournal']).toBe('JIM');
    expect(payload.filters['activeJournalLabel']).toBe('Immobilisations');
  });

  it('does not reload when selecting the same tab', () => {
    flushJournal('JV');

    component.onJournalTabChange({ index: 0, code: 'JV' });
    httpMock.expectNone(`${base}/journal`);
  });
});
