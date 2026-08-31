import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { PromptModalComponent } from './prompt-modal.component';

describe('PromptModalComponent', () => {
  let fixture: ComponentFixture<PromptModalComponent>;
  let component: PromptModalComponent;
  let activeModal: jasmine.SpyObj<NgbActiveModal>;

  beforeEach(async () => {
    activeModal = jasmine.createSpyObj('NgbActiveModal', ['close', 'dismiss']);

    await TestBed.configureTestingModule({
      imports: [PromptModalComponent],
      providers: [{ provide: NgbActiveModal, useValue: activeModal }]
    }).compileComponents();

    fixture = TestBed.createComponent(PromptModalComponent);
    component = fixture.componentInstance;
    component.header = 'Annuler le paiement';
    component.message = 'Saisissez le motif d\'annulation :';
    component.placeholder = 'Motif';
    component.acceptLabel = 'Annuler le paiement';
    component.rejectLabel = 'Conserver';
    component.acceptButtonStyleClass = 'btn-danger';
    fixture.detectChanges();
  });

  it('closes with the trimmed value on accept', () => {
    component.value = '  motif rejet  ';
    fixture.detectChanges();
    const accept = fixture.nativeElement.querySelector('.btn-danger') as HTMLButtonElement;

    accept.click();

    expect(activeModal.close).toHaveBeenCalledWith('motif rejet');
  });

  it('disables the accept button when required and the value is empty', () => {
    component.value = '';
    fixture.detectChanges();
    const accept = fixture.nativeElement.querySelector('.btn-danger') as HTMLButtonElement;

    expect(accept.disabled).toBeTrue();
  });

  it('dismisses the modal on reject', () => {
    const reject = fixture.nativeElement.querySelector('.btn-secondary') as HTMLButtonElement;

    reject.click();

    expect(activeModal.dismiss).toHaveBeenCalled();
    expect(activeModal.close).not.toHaveBeenCalled();
  });
});
