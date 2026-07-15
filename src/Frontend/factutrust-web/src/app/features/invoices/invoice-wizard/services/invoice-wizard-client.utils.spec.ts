import { ClientTaxType } from '../models/invoice-wizard.models';
import {
  isClientReadyForSubmission,
  normalizeClientId,
  normalizeClientInfo,
  resolveClientRecordId
} from './invoice-wizard-client.utils';

const baseAddress = {
  street: '1 rue',
  streetLine2: null as string | null,
  postalCode: null as string | null,
  city: 'Tunis',
  governorate: 'Tunis',
  country: 'Tunisie'
};

function makeClient(partial: Partial<import('../models/invoice-wizard.models').ClientInfo>) {
  return {
    id: null as string | null,
    isNewClient: false,
    name: 'ACME',
    taxType: ClientTaxType.NonTaxSubject,
    address: { ...baseAddress },
    nif: null as string | null,
    email: 'a@b.c',
    phone: null as string | null,
    contactPerson: null as string | null,
    ...partial
  };
}

describe('invoice-wizard-client.utils', () => {
  describe('normalizeClientId', () => {
    it('returns null for empty or whitespace string', () => {
      expect(normalizeClientId('')).toBeNull();
      expect(normalizeClientId('  ')).toBeNull();
      expect(normalizeClientId(null)).toBeNull();
      expect(normalizeClientId(undefined)).toBeNull();
    });

    it('trims non-empty strings', () => {
      expect(normalizeClientId('  abc  ')).toBe('abc');
    });
  });

  describe('resolveClientRecordId', () => {
    it('reads id or Id', () => {
      expect(resolveClientRecordId({ id: 'x' })).toBe('x');
      expect(resolveClientRecordId({ Id: 'y' })).toBe('y');
      expect(resolveClientRecordId({ id: '', Id: 'z' })).toBe('z');
    });
  });

  describe('normalizeClientInfo', () => {
    it('forces isNewClient false when id is present', () => {
      const c = makeClient({ id: 'guid-1', isNewClient: true, name: 'X' });
      const n = normalizeClientInfo(c);
      expect(n.id).toBe('guid-1');
      expect(n.isNewClient).toBe(false);
    });

    it('resolves Id (PascalCase) and sets isNewClient false', () => {
      const raw = {
        ...makeClient({ id: null, isNewClient: false, name: 'BelHedi' }),
        Id: '550e8400-e29b-41d4-a716-446655440000'
      } as unknown as import('../models/invoice-wizard.models').ClientInfo;
      const n = normalizeClientInfo(raw);
      expect(n.id).toBe('550e8400-e29b-41d4-a716-446655440000');
      expect(n.isNewClient).toBe(false);
    });

    it('keeps isNewClient true only when strictly true and no id', () => {
      const n = normalizeClientInfo(makeClient({ id: null, isNewClient: true }));
      expect(n.isNewClient).toBe(true);
    });

    it('treats missing isNewClient as false when no id', () => {
      const c = makeClient({ id: null, isNewClient: false });
      delete (c as { isNewClient?: boolean }).isNewClient;
      const n = normalizeClientInfo(c as import('../models/invoice-wizard.models').ClientInfo);
      expect(n.isNewClient).toBe(false);
    });
  });

  describe('isClientReadyForSubmission', () => {
    it('accepts existing client with id', () => {
      expect(isClientReadyForSubmission(makeClient({ id: 'g1', isNewClient: false }))).toBe(true);
    });

    it('accepts new client with isNewClient true', () => {
      expect(isClientReadyForSubmission(makeClient({ id: null, isNewClient: true }))).toBe(true);
    });

    it('rejects client without id and without isNewClient', () => {
      expect(isClientReadyForSubmission(makeClient({ id: null, isNewClient: false }))).toBe(false);
    });

    it('rejects null', () => {
      expect(isClientReadyForSubmission(null)).toBe(false);
    });

    it('accepts when only Id is present on raw object (PascalCase)', () => {
      const raw = {
        ...makeClient({ id: null, isNewClient: false }),
        Id: '550e8400-e29b-41d4-a716-446655440001'
      } as unknown as import('../models/invoice-wizard.models').ClientInfo;
      expect(isClientReadyForSubmission(raw)).toBe(true);
    });
  });
});
