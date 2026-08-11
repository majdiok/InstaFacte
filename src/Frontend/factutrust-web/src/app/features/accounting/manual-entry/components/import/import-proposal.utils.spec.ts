import { createEmptyLine } from '../../models/entry-form.model';
import {
  buildStoreSnapshot,
  entryLinesToProposedLines,
  hasMeaningfulEdits,
  hasMeaningfulHeaderEdits,
  hasMeaningfulLineEdits,
  isImportProposalBalanced,
  mergeProposalWithStore,
  recomputeImportDiagnostics
} from './import-proposal.utils';
import { JournalEntryProposal } from '../../models/accounting-document-import.models';

const accounts = [
  { id: '1', accountNumber: '607', label: 'Achats', accountClass: 6, isActive: true, isSystem: false, natureType: 0, level: 1 },
  { id: '2', accountNumber: '4011', label: 'Fournisseurs', accountClass: 4, isActive: true, isSystem: false, natureType: 0, level: 1 },
  { id: '3', accountNumber: '9999', label: 'Inactif', accountClass: 6, isActive: false, isSystem: false, natureType: 0, level: 1 }
];

function baseProposal(): JournalEntryProposal {
  return {
    direction: 'PURCHASE',
    directionReason: null,
    journalCode: 'JA',
    entryDate: '2026-08-01',
    label: 'Facture fournisseur',
    pieceRef: 'FAC-001',
    pieceDate: '2026-08-01',
    thirdParty: {
      kind: 2,
      matchedId: null,
      matchedName: null,
      matchedBy: null,
      collectiveAccountNumber: '4011',
      name: 'Supplier',
      nif: null,
      email: null,
      phone: null,
      street: null,
      city: null,
      postalCode: null,
      governorate: null
    },
    lines: [
      {
        accountNumber: '607',
        accountLabel: 'Achats',
        accountStatus: 'found',
        role: 'expense',
        label: 'HT',
        debit: 100,
        credit: 0,
        vatRatePercent: null,
        thirdPartyId: null,
        thirdPartyKind: null
      },
      {
        accountNumber: '4011',
        accountLabel: 'Fournisseurs',
        accountStatus: 'found',
        role: 'thirdparty',
        label: 'Fournisseur',
        debit: 0,
        credit: 100,
        vatRatePercent: null,
        thirdPartyId: null,
        thirdPartyKind: null
      }
    ],
    totalDebit: 100,
    totalCredit: 100,
    documentTotalTtc: 100,
    extraction: {
      documentType: 'INVOICE',
      documentNumber: 'FAC-001',
      issueDate: '2026-08-01',
      dueDate: null,
      documentStatus: null,
      currency: 'TND',
      seller: null,
      buyer: null,
      lines: [],
      vatBreakdown: [],
      totalHt: 100,
      totalVat: 0,
      fodecAmount: null,
      fiscalStampAmount: null,
      withholdingAmount: null,
      totalTtc: 100,
      confidence: 'high',
      warnings: [],
      extractionMethod: 'native-pdf',
      ocrApplied: false,
      vatBreakdownRecomputed: false
    },
    diagnostics: [
      {
        severity: 'warning',
        code: 'ThirdPartyNotMatched',
        message: 'Tiers non rapproché',
        relatedLineIndex: 1
      }
    ],
    hasBlockingDiagnostic: false
  };
}

describe('import-proposal.utils', () => {
  it('detects header edits', () => {
    const p = baseProposal();
    expect(
      hasMeaningfulHeaderEdits(p, {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Facture fournisseur',
        pieceRef: 'FAC-001',
        pieceDate: '2026-08-01'
      })
    ).toBe(false);
    expect(
      hasMeaningfulHeaderEdits(p, {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Autre libellé',
        pieceRef: 'FAC-001',
        pieceDate: '2026-08-01'
      })
    ).toBe(true);
  });

  it('detects line amount edits', () => {
    const p = baseProposal();
    const lines = [
      { ...createEmptyLine(), accountNumber: '607', lineLabel: 'HT', debit: 120, credit: null },
      { ...createEmptyLine(), accountNumber: '4011', lineLabel: 'Fournisseur', debit: null, credit: 100 }
    ];
    expect(hasMeaningfulLineEdits(p.lines, lines)).toBe(true);
  });

  it('maps entry lines to proposed lines with account status', () => {
    const lines = [
      { ...createEmptyLine(), accountNumber: '607', lineLabel: 'HT', debit: 50, credit: null },
      { ...createEmptyLine(), accountNumber: '9999', lineLabel: 'Bad', debit: 50, credit: null }
    ];
    const proposed = entryLinesToProposedLines(lines, baseProposal().lines, accounts, 'PURCHASE');
    expect(proposed[0].accountStatus).toBe('found');
    expect(proposed[1].accountStatus).toBe('inactive');
  });

  it('recomputes blocking account missing diagnostic', () => {
    const p = baseProposal();
    const lines = [
      { ...createEmptyLine(), accountNumber: '8888', lineLabel: 'HT', debit: 100, credit: null },
      { ...createEmptyLine(), accountNumber: '4011', lineLabel: 'Fournisseur', debit: null, credit: 100 }
    ];
    const snapshot = buildStoreSnapshot(
      {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Facture',
        pieceRef: 'FAC-001',
        pieceDate: '2026-08-01'
      },
      lines,
      false,
      true
    );
    const proposed = entryLinesToProposedLines(lines, p.lines, accounts, p.direction);
    const diagnostics = recomputeImportDiagnostics(p, proposed, snapshot, accounts);
    expect(diagnostics.some(d => d.code === 'AccountMissing' && d.severity === 'blocking')).toBe(true);
  });

  it('mergeProposalWithStore updates totals and label', () => {
    const p = baseProposal();
    const lines = [
      { ...createEmptyLine(), accountNumber: '607', lineLabel: 'HT', debit: 80, credit: null },
      { ...createEmptyLine(), accountNumber: '4011', lineLabel: 'Fournisseur', debit: null, credit: 80 }
    ];
    const snapshot = buildStoreSnapshot(
      {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Libellé modifié',
        pieceRef: 'FAC-002',
        pieceDate: '2026-08-01'
      },
      lines,
      false,
      true
    );
    const merged = mergeProposalWithStore(p, snapshot, accounts);
    expect(merged.label).toBe('Libellé modifié');
    expect(merged.pieceRef).toBe('FAC-002');
    expect(merged.totalDebit).toBe(80);
    expect(merged.totalCredit).toBe(80);
    expect(merged.lines.length).toBe(2);
  });

  it('preserves server-only diagnostics like DuplicatePieceRef', () => {
    const p: JournalEntryProposal = {
      ...baseProposal(),
      diagnostics: [
        {
          severity: 'blocking',
          code: 'DuplicatePieceRef',
          message: 'Doublon',
          relatedLineIndex: null
        }
      ],
      hasBlockingDiagnostic: true
    };
    const snapshot = buildStoreSnapshot(
      {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Facture',
        pieceRef: 'FAC-001',
        pieceDate: '2026-08-01'
      },
      [
        { ...createEmptyLine(), accountNumber: '607', lineLabel: 'HT', debit: 100, credit: null },
        { ...createEmptyLine(), accountNumber: '4011', lineLabel: 'Fournisseur', debit: null, credit: 100 }
      ],
      false,
      true
    );
    const merged = mergeProposalWithStore(p, snapshot, accounts);
    expect(merged.diagnostics.some(d => d.code === 'DuplicatePieceRef')).toBe(true);
    expect(merged.hasBlockingDiagnostic).toBe(true);
  });

  it('isImportProposalBalanced ignores empty lines', () => {
    const lines = [
      { ...createEmptyLine(), accountNumber: '607', debit: 100, credit: null },
      { ...createEmptyLine(), accountNumber: '4011', debit: null, credit: 100 },
      createEmptyLine()
    ];
    expect(isImportProposalBalanced(lines)).toBe(true);
    lines[1].credit = 90;
    expect(isImportProposalBalanced(lines)).toBe(false);
  });

  it('hasMeaningfulEdits combines header and lines', () => {
    const p = baseProposal();
    const snapshot = buildStoreSnapshot(
      {
        journalCode: 'JA',
        entryDate: '2026-08-01',
        entryLabel: 'Facture fournisseur',
        pieceRef: 'FAC-001',
        pieceDate: '2026-08-01'
      },
      [
        { ...createEmptyLine(), accountNumber: '607', lineLabel: 'HT', debit: 100, credit: null },
        { ...createEmptyLine(), accountNumber: '4011', lineLabel: 'Fournisseur', debit: null, credit: 100 }
      ],
      false,
      true
    );
    expect(hasMeaningfulEdits(p, snapshot)).toBe(false);
  });
});
