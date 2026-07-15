import { DashboardConfig, DashboardSection } from '../models/ai-chat.models';

function csvEscapeCell(value: unknown): string {
  const s = value === undefined || value === null ? '' : String(value);
  if (/[",\n\r]/.test(s)) {
    return `"${s.replace(/"/g, '""')}"`;
  }
  return s;
}

/** Builds CSV text for a dashboard table section (UTF-8, Excel-friendly separator). */
export function dashboardTableSectionToCsv(section: DashboardSection): string {
  if (section.type !== 'table') {
    return '';
  }
  const cols = section.data?.columns ?? [];
  const rows = section.data?.rows ?? [];
  if (cols.length === 0) {
    return '';
  }
  const header = cols.map((c: { label: string; key: string }) => csvEscapeCell(c.label)).join(';');
  const lines = rows.map((row: Record<string, unknown>) =>
    cols.map((c: { label: string; key: string }) => csvEscapeCell(row[c.key])).join(';')
  );
  return [header, ...lines].join('\r\n');
}

export function downloadCsv(filename: string, csvBody: string): void {
  const blob = new Blob(['\ufeff' + csvBody], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename.endsWith('.csv') ? filename : `${filename}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

/** Concatenates all table sections with section titles as separators (Excel-friendly). */
export function dashboardFullConfigToCsv(config: DashboardConfig): string {
  const parts: string[] = [];
  for (const sec of config.sections) {
    if (sec.type !== 'table') {
      continue;
    }
    const csv = dashboardTableSectionToCsv(sec);
    if (csv) {
      parts.push(`# ${sec.title}\r\n${csv}`);
    }
  }
  return parts.join('\r\n\r\n');
}
