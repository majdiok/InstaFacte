import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { FirmAssignmentService, FirmClientAssignment } from '@core/services/firm-assignment.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { AccountingFirmSettingsComponent } from './accounting-firm-settings.component';

function assignment(partial: Partial<FirmClientAssignment>): FirmClientAssignment {
  return {
    id: 'a1',
    companyTenantId: 'c1',
    companyName: 'Ma Société',
    firmTenantId: 'f1',
    firmDisplayName: 'Cabinet Test',
    status: 0,
    statusDisplay: '',
    requestedAt: '2026-07-18T09:00:00Z',
    ...partial
  };
}

describe('AccountingFirmSettingsComponent', () => {
  function setup(current: FirmClientAssignment | null, history: FirmClientAssignment[] = []) {
    const service = {
      getCompanyCurrent: () => of({ success: true, data: current }),
      getCompanyHistory: () => of({ success: true, data: history }),
      searchDirectory: () => of({ success: true, data: [] }),
      requestAssignment: jasmine.createSpy('requestAssignment').and.returnValue(of({ success: true, data: current })),
      cancelPendingRequest: jasmine.createSpy('cancelPendingRequest').and.returnValue(of({ success: true, data: null })),
      revokeByCompany: jasmine.createSpy('revokeByCompany').and.returnValue(of({ success: true, data: null }))
    };

    TestBed.configureTestingModule({
      imports: [AccountingFirmSettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FirmAssignmentService, useValue: service },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ConfirmationService, useValue: { confirm: (c: { accept?: () => void }) => c.accept?.() } }
      ]
    });

    const fixture = TestBed.createComponent(AccountingFirmSettingsComponent);
    fixture.detectChanges();
    return { fixture, service };
  }

  it('affiche la recherche quand aucune liaison ouverte', () => {
    const { fixture } = setup(null, []);
    const html = fixture.nativeElement.textContent as string;
    expect(html).toContain('Rechercher un cabinet');
    expect(html).toContain('Envoyer la demande');
  });

  it('affiche le bouton « Annuler la demande » pour une demande en attente', () => {
    const { fixture } = setup(assignment({ status: 0 }));
    const html = fixture.nativeElement.textContent as string;
    expect(html).toContain('Annuler la demande');
    expect(html).toContain("En attente d'acceptation");
    expect(html).not.toContain('Rechercher un cabinet');
  });

  it('affiche « Révoquer l\'affectation » pour une liaison active avec libellé corrigé', () => {
    const { fixture } = setup(assignment({ status: 1, respondedAt: '2026-07-18T10:00:00Z' }));
    const html = fixture.nativeElement.textContent as string;
    expect(html).toContain("Révoquer l'affectation");
    expect(html).toContain('Liaison active');
    expect(html).not.toContain('Active</'); // pas le libellé backend brut
  });

  it('débloque la recherche et affiche le motif après un refus', () => {
    const rejected = assignment({ status: 2, rejectionReason: 'Dossier incomplet', respondedAt: '2026-07-18T11:00:00Z' });
    const { fixture } = setup(null, [rejected]);
    const html = fixture.nativeElement.textContent as string;
    expect(html).toContain('Rechercher un cabinet');
    expect(html).toContain('Refusée');
    expect(html).toContain('Dossier incomplet');
  });

  it('annule la demande en attente via le service (confirmation auto-acceptée)', () => {
    const { fixture, service } = setup(assignment({ status: 0 }));
    fixture.componentInstance.cancelPending();
    expect(service.cancelPendingRequest).toHaveBeenCalled();
  });
});
