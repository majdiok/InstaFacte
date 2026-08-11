import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  AccountingAnomalyDetailDto,
  AccountingAnomalyListItemDto,
  AccountingAuditCorrectionLinkDto
} from './accounting-audit.service';

export type AuditAnomalyNavItem = Pick<
  AccountingAnomalyListItemDto,
  'id' | 'ruleCode' | 'deepLinkRoute' | 'correctionLink' | 'accountRef' | 'periodFrom' | 'periodTo'
> &
  Partial<Pick<AccountingAnomalyDetailDto, 'lines'>>;

@Injectable({ providedIn: 'root' })
export class AuditCorrectionNavigator {
  private readonly router = inject(Router);

  navigate(item: AuditAnomalyNavItem, options?: { closeDialog?: () => void; fiscalYear?: number }): void {
    const link = this.resolveLink(item, options?.fiscalYear);
    if (!link) return;

    options?.closeDialog?.();

    const returnUrl = options?.fiscalYear
      ? `/accounting/health?fiscalYear=${options.fiscalYear}`
      : '/accounting/health';

    const queryParams: Record<string, string> = {
      ...link.queryParams,
      returnUrl
    };

    void this.router.navigate([link.route], { queryParams });
  }

  navigateToEntry(
    journalEntryId: string | null | undefined,
    entryDate?: string | null,
    journalCode?: string | null
  ): void {
    if (!journalEntryId && !entryDate) return;
    const date = entryDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10);
    const params: Record<string, string> = { from: date, to: date, autoLoad: '1' };
    if (journalCode) params['journalCode'] = journalCode;
    void this.router.navigate(['/accounting/journal'], { queryParams: params });
  }

  resolveLink(item: AuditAnomalyNavItem, fiscalYear?: number): AccountingAuditCorrectionLinkDto | null {
    if (item.correctionLink?.route) return item.correctionLink;
    return buildFallbackCorrectionLink(item, fiscalYear ?? new Date().getFullYear());
  }
}

/** Fallback si l'API ne renvoie pas encore correctionLink. */
export function buildFallbackCorrectionLink(
  item: AuditAnomalyNavItem,
  fiscalYear: number
): AccountingAuditCorrectionLinkDto | null {
  const route = item.deepLinkRoute;
  if (!route) return null;

  const from = item.periodFrom?.slice(0, 10) ?? `${fiscalYear}-01-01`;
  const to = item.periodTo?.slice(0, 10) ?? `${fiscalYear}-12-31`;
  const firstLine = item.lines?.[0];
  const queryParams: Record<string, string> = { from, to };

  switch (item.ruleCode) {
    case 'drafts':
      return {
        route: '/accounting/entry-search',
        queryParams: { ...queryParams, status: '0', autoSearch: '1' },
        label: 'Voir les brouillards'
      };
    case 'unlettered': {
      const account = item.accountRef?.includes('4011') && !item.accountRef.includes('4111') ? '4011' : '4111';
      return {
        route: '/accounting/lettering',
        queryParams: { account, from, to, unletteredOnly: '1', autoLoad: '1' },
        label: 'Ouvrir le lettrage'
      };
    }
    case 'entry-missing-attachment':
      return {
        route: '/accounting/journal',
        queryParams: { from, to, missingAttachment: '1', autoLoad: '1' },
        label: 'Voir les écritures sans justificatif'
      };
    case 'health-piece-duplicates':
    case 'sequence-gaps':
    case 'unbalanced':
    case 'health-thirdparty-mislink':
      return {
        route: '/accounting/entry-search',
        queryParams: { ...queryParams, autoSearch: '1' },
        label: 'Rechercher les écritures'
      };
    case 'vat':
    case 'vat-deductible-no-proof':
      return {
        route: '/accounting/vat-declaration',
        queryParams: { fiscalYear: String(fiscalYear) },
        label: 'Ouvrir la TVA'
      };
    case 'open-periods':
    case 'health-out-of-period':
      return {
        route: '/accounting/closing',
        queryParams: { fiscalYear: String(fiscalYear) },
        label: 'Gérer les périodes'
      };
    case 'depreciation':
      return {
        route: '/accounting/fixed-assets/depreciation-run',
        queryParams: { fiscalYear: String(fiscalYear) },
        label: 'Dotations'
      };
    case 'recon-bank-incomplete':
      return {
        route: '/accounting/bank-reconciliation',
        queryParams: { fiscalYear: String(fiscalYear), unmatchedOnly: '1' },
        label: 'Rapprochement bancaire'
      };
    case 'suspense':
      return {
        route: '/accounting/balance',
        queryParams: { account: item.accountRef ?? firstLine?.accountNumber ?? '471', from, to },
        label: 'Consulter la balance'
      };
    default:
      return { route, queryParams, label: 'Corriger' };
  }
}
