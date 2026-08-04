import { BillableDossier } from '../services/honoraires.service';
import { resolveDossier, toClientSnapshot } from './honoraires-dossier.util';

describe('resolveDossier', () => {
  const catalogue: BillableDossier[] = [
    {
      assignmentId: 'a1',
      companyTenantId: 't1',
      companyName: 'Ste Bouzgarou',
      nif: '4555985/P/P/L/000',
      address: 'Tunis'
    },
    {
      assignmentId: 'a2',
      companyTenantId: 't2',
      companyName: 'Société Hadad',
      nif: '6666665/P/O/M/000'
    }
  ];

  it('returns catalogue entry when assignmentId matches', () => {
    const result = resolveDossier(catalogue, 'a1');
    expect(result?.companyName).toBe('Ste Bouzgarou');
    expect(result?.address).toBe('Tunis');
  });

  it('returns fallback snapshot when dossier absent from catalogue', () => {
    const snapshot = toClientSnapshot({
      assignmentId: 'archived',
      companyName: 'Client archivé',
      nif: '111'
    });
    const result = resolveDossier(catalogue, 'archived', snapshot);
    expect(result?.companyName).toBe('Client archivé');
    expect(result?.nif).toBe('111');
  });

  it('returns null when assignmentId is null and no fallback', () => {
    expect(resolveDossier(catalogue, null)).toBeNull();
    expect(resolveDossier(catalogue, undefined)).toBeNull();
  });

  it('returns fallback when assignmentId is null but fallback provided', () => {
    const snapshot = toClientSnapshot({ assignmentId: 'x', companyName: 'Legacy' });
    expect(resolveDossier(catalogue, null, snapshot)?.companyName).toBe('Legacy');
  });

  it('prefers catalogue over fallback when both exist', () => {
    const snapshot = toClientSnapshot({
      assignmentId: 'a1',
      companyName: 'Nom snapshot obsolète'
    });
    const result = resolveDossier(catalogue, 'a1', snapshot);
    expect(result?.companyName).toBe('Ste Bouzgarou');
  });
});

describe('toClientSnapshot', () => {
  it('maps document fields into BillableDossier shape', () => {
    const snap = toClientSnapshot({
      assignmentId: 'id-1',
      companyName: 'ACME',
      nif: 'NIF',
      address: 'Adresse',
      contactEmail: 'a@b.c'
    });
    expect(snap).toEqual({
      assignmentId: 'id-1',
      companyTenantId: '',
      companyName: 'ACME',
      nif: 'NIF',
      address: 'Adresse',
      contactEmail: 'a@b.c',
      contactPhone: undefined
    });
  });
});
