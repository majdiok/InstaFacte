import * as XLSX from 'xlsx';

export interface ExportColumn {
  key: string;
  label: string;
}

function cell(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (Array.isArray(v)) return v.join(', ');
  if (typeof v === 'boolean') return v ? 'Oui' : 'Non';
  return String(v);
}

function csvEscape(v: unknown): string {
  const s = cell(v);
  return /[";\r\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
}

function ensureExt(name: string, ext: string): string {
  const safe = (name || 'export').replace(/[^\p{L}\p{N}_-]+/gu, '_');
  return safe.toLowerCase().endsWith('.' + ext) ? safe : `${safe}.${ext}`;
}

function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** Exports rows (keyed by column.key) to a semicolon-separated CSV (Excel-FR friendly, BOM). */
export function exportRowsCsv(
  filename: string,
  columns: ExportColumn[],
  rows: Record<string, unknown>[],
  formatCell?: (row: Record<string, unknown>, col: ExportColumn) => string
): void {
  const header = columns.map(c => csvEscape(c.label)).join(';');
  const lines = rows.map(r => columns.map(c => csvEscape(formatCell ? formatCell(r, c) : r[c.key])).join(';'));
  const csv = [header, ...lines].join('\r\n');
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' });
  triggerDownload(blob, ensureExt(filename, 'csv'));
}

/** Exports rows to a real .xlsx workbook (reuses the xlsx dependency already in the app). */
export function exportRowsXlsx(
  filename: string,
  columns: ExportColumn[],
  rows: Record<string, unknown>[],
  formatCell?: (row: Record<string, unknown>, col: ExportColumn) => string
): void {
  const data = rows.map(r => {
    const o: Record<string, string> = {};
    for (const c of columns) o[c.label] = formatCell ? formatCell(r, c) : cell(r[c.key]);
    return o;
  });
  const ws = XLSX.utils.json_to_sheet(data, { header: columns.map(c => c.label) });
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Données');
  XLSX.writeFile(wb, ensureExt(filename, 'xlsx'));
}
