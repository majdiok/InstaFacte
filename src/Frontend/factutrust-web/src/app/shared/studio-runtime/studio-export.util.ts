import ExcelJS from 'exceljs';

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

async function downloadWorkbook(workbook: ExcelJS.Workbook, filename: string): Promise<void> {
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  });
  triggerDownload(blob, ensureExt(filename, 'xlsx'));
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

/** Exports rows to a real .xlsx workbook via ExcelJS. */
export async function exportRowsXlsx(
  filename: string,
  columns: ExportColumn[],
  rows: Record<string, unknown>[],
  formatCell?: (row: Record<string, unknown>, col: ExportColumn) => string
): Promise<void> {
  const workbook = new ExcelJS.Workbook();
  const worksheet = workbook.addWorksheet('Données');
  worksheet.addRow(columns.map(c => c.label));
  for (const row of rows) {
    worksheet.addRow(columns.map(c => (formatCell ? formatCell(row, c) : cell(row[c.key]))));
  }
  await downloadWorkbook(workbook, filename);
}
