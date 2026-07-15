/**
 * Best-effort export of HTML tables rendered inside assistant markdown (e.g. ngx-markdown).
 * Merged cells and complex layouts may not round-trip perfectly.
 */
export function htmlTablesToCsv(host: HTMLElement): string | null {
  const tables = host.querySelectorAll<HTMLTableElement>('table');
  if (tables.length === 0) {
    return null;
  }

  const blocks: string[] = [];
  tables.forEach((table, index) => {
    const rows = table.querySelectorAll('tr');
    if (rows.length === 0) {
      return;
    }
    const lines: string[] = [];
    rows.forEach(tr => {
      const cells = tr.querySelectorAll<HTMLTableCellElement>('th, td');
      const values = Array.from(cells).map(c => normalizeCellText(c.textContent));
      if (values.length > 0) {
        lines.push(values.map(csvEscapeCell).join(';'));
      }
    });
    if (lines.length > 0) {
      blocks.push(`# Table ${index + 1}\r\n${lines.join('\r\n')}`);
    }
  });

  return blocks.length > 0 ? blocks.join('\r\n\r\n') : null;
}

function normalizeCellText(raw: string | null): string {
  if (!raw) {
    return '';
  }
  return raw.replace(/\s+/g, ' ').trim();
}

function csvEscapeCell(value: string): string {
  if (/[",\n\r]/.test(value)) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}
