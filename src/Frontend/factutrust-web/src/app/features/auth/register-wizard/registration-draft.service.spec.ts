import { TestBed } from '@angular/core/testing';
import {
  FORBIDDEN_DRAFT_FIELDS,
  PERSISTED_FIELDS,
  RegistrationDraftService
} from './registration-draft.service';

const STORAGE_KEY = 'ft_register_draft';

/** Charge de formulaire complète, mots de passe compris — ce que le composant passe réellement. */
function fullFormValue(): Record<string, unknown> {
  return {
    companySegment: 'commerce',
    businessDomain: 'artisanat',
    firstName: 'Sana',
    lastName: 'Ben Ali',
    email: 'sana@example.com',
    password: 'SuperSecret123!',
    confirmPassword: 'SuperSecret123!',
    companyName: 'Atelier Sana',
    nif: '1234567/A/B/C/000',
    taxRegime: 0,
    companyEmail: 'contact@example.com',
    phone: '71123456',
    website: '',
    warehouseName: 'Magasin principal',
    enabledModules: [0, 1, 2],
    hasPhysicalStock: true,
    sellsToConsumers: null,
    headcountBand: '2-9',
    accountingDelegatedToFirm: false,
    street: 'Rue Rmada',
    streetLine2: '',
    city: 'Monastir',
    postalCode: '5000',
    governorate: 'Bizerte',
    acceptTerms: true
  };
}

describe('RegistrationDraftService', () => {
  let service: RegistrationDraftService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RegistrationDraftService);
    sessionStorage.clear();
  });

  afterEach(() => sessionStorage.clear());

  describe('sécurité de la charge persistée', () => {
    it('n’écrit JAMAIS un mot de passe (ni acceptTerms) dans le stockage', () => {
      service.save(fullFormValue(), 3);

      const raw = sessionStorage.getItem(STORAGE_KEY) ?? '';
      expect(raw).not.toBe('');

      // Sur la chaîne brute : aucun nom de champ interdit, et surtout aucune valeur de secret.
      for (const forbidden of FORBIDDEN_DRAFT_FIELDS) {
        expect(raw).withContext(`champ interdit « ${forbidden} » présent`).not.toContain(forbidden);
      }
      expect(raw).not.toContain('SuperSecret123!');
    });

    it('n’expose que des champs de la liste blanche', () => {
      service.save(fullFormValue(), 1);

      const stored = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? '{}');
      const allowed = new Set<string>(PERSISTED_FIELDS);
      for (const key of Object.keys(stored.values)) {
        expect(allowed.has(key)).withContext(`champ inattendu « ${key} »`).toBeTrue();
      }
    });

    it('ignore un champ interdit déjà présent dans un brouillon forgé (défense en profondeur)', () => {
      sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          version: 1,
          savedAt: Date.now(),
          step: 2,
          values: { firstName: 'Sana', password: 'injecté', acceptTerms: true }
        })
      );

      const draft = service.load();

      expect(draft).not.toBeNull();
      expect(draft!.values['firstName']).toBe('Sana');
      expect(Object.keys(draft!.values)).not.toContain('password');
      expect(Object.keys(draft!.values)).not.toContain('acceptTerms');
    });
  });

  describe('cycle de vie', () => {
    it('restitue les valeurs et l’étape enregistrées', () => {
      service.save(fullFormValue(), 2);

      const draft = service.load();

      expect(draft).not.toBeNull();
      expect(draft!.step).toBe(2);
      expect(draft!.values['companyName']).toBe('Atelier Sana');
      expect(draft!.values['headcountBand']).toBe('2-9');
      // `false` est une réponse, pas une absence : elle doit survivre.
      expect(draft!.values['accountingDelegatedToFirm']).toBeFalse();
    });

    it('omet les valeurs vides pour ne pas écraser un formulaire vierge', () => {
      service.save(fullFormValue(), 0);
      const draft = service.load();
      expect(Object.keys(draft!.values)).not.toContain('website');
      expect(Object.keys(draft!.values)).not.toContain('streetLine2');
    });

    it('purge un brouillon expiré (au-delà de 24 h)', () => {
      sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          version: 1,
          savedAt: Date.now() - 25 * 60 * 60 * 1000,
          step: 1,
          values: { firstName: 'Sana' }
        })
      );

      expect(service.load()).toBeNull();
      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('purge un brouillon de version obsolète', () => {
      sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ version: 0, savedAt: Date.now(), step: 1, values: { firstName: 'Sana' } })
      );

      expect(service.load()).toBeNull();
    });

    it('renvoie null sur un contenu illisible et nettoie derrière lui', () => {
      sessionStorage.setItem(STORAGE_KEY, 'pas du json');

      expect(service.load()).toBeNull();
      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('clear() supprime le brouillon', () => {
      service.save(fullFormValue(), 1);
      service.clear();
      expect(service.load()).toBeNull();
    });

    it('n’enregistre rien quand le formulaire est vierge', () => {
      service.save({ firstName: '', email: null, password: 'Secret123!' }, 0);
      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });
  });
});
