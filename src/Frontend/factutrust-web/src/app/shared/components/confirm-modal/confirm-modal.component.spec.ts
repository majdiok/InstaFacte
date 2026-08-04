import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmModalComponent } from './confirm-modal.component';

describe('ConfirmModalComponent', () => {
  let fixture: ComponentFixture<ConfirmModalComponent>;
  let component: ConfirmModalComponent;
  let activeModal: jasmine.SpyObj<NgbActiveModal>;

  beforeEach(async () => {
    activeModal = jasmine.createSpyObj('NgbActiveModal', ['close', 'dismiss']);

    await TestBed.configureTestingModule({
      imports: [ConfirmModalComponent],
      providers: [{ provide: NgbActiveModal, useValue: activeModal }]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmModalComponent);
    component = fixture.componentInstance;
    component.header = 'Annuler l\'inventaire';
    component.message = 'Les comptages non validés seront perdus.';
    component.acceptLabel = 'Annuler l\'inventaire';
    component.rejectLabel = 'Continuer le comptage';
    component.acceptButtonStyleClass = 'btn-danger';
    fixture.detectChanges();
  });

  it('renders both action labels in full in the footer', () => {
    const footer = fixture.nativeElement.querySelector('.confirm-modal-footer') as HTMLElement;
    expect(footer.textContent).toContain('Continuer le comptage');
    expect(footer.textContent).toContain('Annuler l\'inventaire');
  });

  it('applies btn-danger on the accept button', () => {
    const accept = fixture.nativeElement.querySelector(
      '.confirm-modal-btn-accept'
    ) as HTMLButtonElement;
    expect(accept.classList.contains('btn-danger')).toBeTrue();
  });

  it('closes on accept and dismisses on reject', () => {
    const reject = fixture.nativeElement.querySelector(
      '.confirm-modal-btn-reject'
    ) as HTMLButtonElement;
    const accept = fixture.nativeElement.querySelector(
      '.confirm-modal-btn-accept'
    ) as HTMLButtonElement;

    reject.click();
    expect(activeModal.dismiss).toHaveBeenCalled();

    accept.click();
    expect(activeModal.close).toHaveBeenCalled();
  });
});
