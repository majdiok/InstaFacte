import { FormControl, Validators } from '@angular/forms';
import { ErrorMessageService } from './error-message.service';

describe('ErrorMessageService', () => {
  const service = new ErrorMessageService();

  it('affiche le message serveur string', () => {
    const control = new FormControl('dup@test.com', Validators.email);
    control.setErrors({ server: 'Un fournisseur existe déjà avec cet email.' });
    expect(service.getErrorMessage(control)).toBe('Un fournisseur existe déjà avec cet email.');
  });

  it('affiche le message serveur objet { message }', () => {
    const control = new FormControl('');
    control.setErrors({ server: { message: 'Un fournisseur existe déjà avec ce matricule fiscal.' } });
    expect(service.getErrorMessage(control)).toBe('Un fournisseur existe déjà avec ce matricule fiscal.');
  });

  it('garde le message required pour les erreurs natives', () => {
    const control = new FormControl('', Validators.required);
    control.markAsTouched();
    expect(service.getErrorMessage(control)).toBe('Ce champ est obligatoire');
  });
});
