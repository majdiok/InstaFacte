import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { formatLocalDate } from '@core/utils/date.util';
import { createClientUuid } from '@core/utils/safe-random-uuid.util';
import { ApiResponse } from '@core/services/auth.service';
import { InvoiceService } from '@core/services/invoice.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { environment } from '@environments/environment';
import { InvoiceWizardService } from '../../invoices/invoice-wizard/services/invoice-wizard.service';
import { PaymentMethod } from '../../invoices/invoice-wizard/models/invoice-wizard.models';
import { PaymentSplit, PosStateService } from './pos-state.service';
import { toApiPaymentMethod } from './pos-payment.mapper';
import { PosRegisterSessionService } from './pos-register-session.service';
import {
  resolveHeaderPaymentMethod,
  shouldRecordPaymentAfterSubmit,
  toDaysUntilDue,
  toDueDate,
  toNamedWizardClient,
  toPassengerWizardClient,
  toPaymentTerms,
  toWizardLines,
  toWizardMetadata
} from './pos-checkout.mapper';

export interface PosSubmitResult {
  invoiceId: string;
  invoiceNumber: string;
}

@Injectable({ providedIn: 'root' })
export class PosCheckoutService {
  private readonly wizardService = inject(InvoiceWizardService);
  private readonly posState = inject(PosStateService);
  private readonly warehouseContext = inject(WarehouseContextService);
  private readonly registerSession = inject(PosRegisterSessionService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly http = inject(HttpClient);
  private readonly wizardApiUrl = `${environment.apiUrl}/invoices/wizard`;

  applySaleToWizard(): void {
    const seller = this.wizardService.seller();
    this.wizardService.reset();
    if (seller) {
      this.wizardService.selectSeller(seller);
    }
    this.applyCartToWizard(false);
  }

  applyCreditNoteToWizard(): void {
    const linked = this.posState.linkedInvoice();
    if (!linked?.id) {
      return;
    }
    for (const line of [...this.wizardService.lines()]) {
      this.wizardService.removeLine(line.id);
    }
    this.applyCartToWizard(true, linked.id);
  }

  submitCurrentDraft(): Observable<PosSubmitResult> {
    return this.wizardService.saveDraft().pipe(
      switchMap(draftId => {
        const idempotencyKey = `pos-${Date.now()}-${createClientUuid()}`.slice(0, 64);
        return this.http.post<ApiResponse<{ invoiceId: string; invoiceNumber: string }>>(
          `${this.wizardApiUrl}/drafts/${draftId}/submit`,
          { idempotencyKey }
        );
      }),
      map(res => {
        const data = res?.data as Record<string, unknown> | undefined;
        const rawId = data?.['invoiceId'] ?? data?.['InvoiceId'];
        const rawNum = data?.['invoiceNumber'] ?? data?.['InvoiceNumber'];
        return {
          invoiceId: typeof rawId === 'string' ? rawId : String(rawId ?? ''),
          invoiceNumber: typeof rawNum === 'string' ? rawNum : String(rawNum ?? '')
        };
      })
    );
  }

  shouldRecordPayment(): boolean {
    return shouldRecordPaymentAfterSubmit(
      this.posState.paymentSchedule(),
      this.posState.isSplitPayment() && this.posState.paymentSplits().length > 0
    );
  }

  recordImmediatePayment(invoiceId: string, method: PaymentMethod): Observable<unknown> {
    return this.invoiceService.recordPayment(invoiceId, {
      paymentDate: formatLocalDate(new Date()),
      method: toApiPaymentMethod(method),
      cashRegisterSessionId: this.sessionId()
    });
  }

  recordCashPayment(invoiceId: string): Observable<unknown> {
    return this.invoiceService.recordPayment(invoiceId, {
      paymentDate: formatLocalDate(new Date()),
      method: toApiPaymentMethod(PaymentMethod.Cash),
      cashRegisterSessionId: this.sessionId()
    });
  }

  recordSplitPayments(invoiceId: string, splits: PaymentSplit[]): Observable<unknown> {
    const paymentDate = formatLocalDate(new Date());
    return this.invoiceService.recordPayments(
      invoiceId,
      splits.map(split => ({
        paymentDate,
        method: toApiPaymentMethod(split.method),
        amount: split.amount,
        cashRegisterSessionId: this.sessionId()
      }))
    );
  }

  sendInvoiceEmail(invoiceId: string): Observable<unknown> {
    return this.invoiceService.sendByEmail(invoiceId);
  }

  private applyCartToWizard(isCreditNote: boolean, linkedInvoiceId?: string): void {
    const lines = this.posState.lines();
    const client = this.posState.client();
    const isSplit = this.posState.isSplitPayment() && this.posState.paymentSplits().length > 0;
    const settlement = this.posState.paymentSchedule();
    const dueDate = isCreditNote
      ? new Date()
      : toDueDate(settlement, client?.defaultPaymentTermDays);
    const daysUntilDue = isCreditNote ? 0 : toDaysUntilDue(settlement, client?.defaultPaymentTermDays);
    const paymentMethod = resolveHeaderPaymentMethod(
      isSplit,
      this.posState.paymentSplits(),
      this.posState.paymentMethod()
    );

    this.wizardService.updateMetadata(
      toWizardMetadata({
        isCreditNote,
        ticketId: this.posState.sessionId(),
        warehouseId: this.warehouseContext.selectedWarehouseId(),
        cashRegisterSessionId: this.registerSession.currentSession()?.id ?? null,
        dueDate,
        linkedInvoiceId: linkedInvoiceId ?? null
      })
    );

    if (client && !client.isWalkIn) {
      this.wizardService.selectClient(toNamedWizardClient(client));
    } else {
      this.wizardService.selectClient(toPassengerWizardClient());
    }

    const totals = this.posState.totals();
    toWizardLines(lines, totals.subTotalHT, totals.totalDiscount).forEach(line => {
      this.wizardService.addLine(line);
    });

    this.wizardService.updatePayment({
      method: paymentMethod,
      terms: toPaymentTerms({
        isSplit,
        orderNotes: this.posState.orderNotes() ?? '',
        settlement,
        dueDate,
        isCreditNote
      }),
      daysUntilDue
    });
  }

  private sessionId(): string | undefined {
    return this.registerSession.currentSession()?.id ?? undefined;
  }
}
