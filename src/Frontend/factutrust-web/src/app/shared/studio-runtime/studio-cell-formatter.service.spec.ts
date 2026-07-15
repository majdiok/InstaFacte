import { TestBed } from '@angular/core/testing';
import { StudioCellFormatterService } from './studio-cell-formatter.service';

describe('StudioCellFormatterService', () => {
  let service: StudioCellFormatterService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(StudioCellFormatterService);
  });

  it('formats ISO datetime for fr-FR', () => {
    const result = service.formatCell('2026-03-22T00:00:00', { key: 'd', label: 'Date', format: 'datetime' });
    expect(result).toContain('2026');
  });

  it('maps status via statusMap', () => {
    const result = service.formatCell('3', {
      key: 'status', label: 'Statut', format: 'status',
      formatOptions: { statusMap: { '3': 'Validé' } }
    });
    expect(result).toBe('Validé');
  });

  it('returns dash for empty values', () => {
    expect(service.formatCell(null, null)).toBe('-');
  });
});