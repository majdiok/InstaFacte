import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { ProjectListComponent } from './project-list.component';
import { ProjectApiService } from './project-api.service';
import { ClientService } from '@core/services/client.service';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ProjectFavoritesService } from './project-favorites.service';

describe('ProjectListComponent', () => {
  let fixture: ComponentFixture<ProjectListComponent>;
  let component: ProjectListComponent;

  const api = {
    list: jasmine.createSpy('list').and.returnValue(of({
      success: true,
      data: { items: [], page: 1, pageSize: 20, totalCount: 0 }
    })),
    dashboardExtended: jasmine.createSpy('dashboardExtended').and.returnValue(of({ success: true, data: {} })),
    dashboard: jasmine.createSpy('dashboard').and.returnValue(of({ success: true, data: {} })),
    users: jasmine.createSpy('users').and.returnValue(of({ success: true, data: [] })),
    exportCsv: jasmine.createSpy('exportCsv').and.returnValue(of(new Blob()))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectListComponent],
      providers: [
        { provide: ProjectApiService, useValue: api },
        { provide: ClientService, useValue: { getClients: () => of({ success: true, data: { items: [] } }) } },
        { provide: AuthService, useValue: { hasPermission: () => false } },
        { provide: ToastService, useValue: { add: () => undefined } },
        { provide: ErrorHandlerService, useValue: { extractErrorMessage: () => 'err' } },
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({ status: 'Active' })) } },
        ProjectFavoritesService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('applies status query param on init', () => {
    expect(component.statusFilter).toBe('Active');
  });

  it('counts extra filters separately from main filters', () => {
    component.overdueOnly = true;
    component.ownerFilter = 'u1';
    expect(component.extraFiltersCount()).toBe(2);
    expect(component.hasActiveFilters()).toBe(true);
  });
});
