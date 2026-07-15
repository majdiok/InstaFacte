import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { normalizeHttpErrorResponse } from './http-error-normalize';

describe('normalizeHttpErrorResponse', () => {
  const errorHandler = new ErrorHandlerService();

  it('parses JSON from Blob error body (blob responseType)', async () => {
    const json = JSON.stringify({ error: 'Aucune facture fournisseur soldée avec retenue à la source pour cette période' });
    const blob = new Blob([json], { type: 'application/json' });
    const err = new HttpErrorResponse({
      error: blob,
      status: 400,
      statusText: 'Bad Request',
      url: 'https://localhost:7001/api/withholding-tax/tej-export/generate'
    });

    const normalized = await firstValueFrom(normalizeHttpErrorResponse(err));
    const message = errorHandler.extractErrorMessage(normalized);

    expect(message).toBe('Aucune facture fournisseur soldée avec retenue à la source pour cette période');
  });

  it('passes through object error unchanged', async () => {
    const err = new HttpErrorResponse({
      error: { error: 'Simple domain error' },
      status: 400,
      statusText: 'Bad Request'
    });

    const normalized = await firstValueFrom(normalizeHttpErrorResponse(err));
    expect(normalized.error).toEqual({ error: 'Simple domain error' });
    expect(errorHandler.extractErrorMessage(normalized)).toBe('Simple domain error');
  });

  it('parses string body like before', async () => {
    const err = new HttpErrorResponse({
      error: '{"error":"From string"}',
      status: 400,
      statusText: 'Bad Request'
    });

    const normalized = await firstValueFrom(normalizeHttpErrorResponse(err));
    expect(errorHandler.extractErrorMessage(normalized)).toBe('From string');
  });
});
