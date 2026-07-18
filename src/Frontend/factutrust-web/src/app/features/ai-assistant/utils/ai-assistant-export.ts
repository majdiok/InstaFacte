import ExcelJS from 'exceljs';
import type { DashboardConfig } from '../models/ai-chat.models';

function sanitizeSheetName(title: string): string {
  const s = title.replace(/[:\\/?*[\]]/g, ' ').trim().slice(0, 31);
  return s.length > 0 ? s : 'Données';
}

function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** One sheet per table section (Excel .xlsx via ExcelJS). */
export async function exportDashboardToXlsx(config: DashboardConfig, baseFileName: string): Promise<void> {
  const workbook = new ExcelJS.Workbook();
  let sheetIdx = 0;
  for (const sec of config.sections) {
    if (sec.type !== 'table') {
      continue;
    }
    const cols = (sec.data?.columns ?? []) as { key: string; label: string }[];
    const rows = (sec.data?.rows ?? []) as Record<string, unknown>[];
    if (cols.length === 0) {
      continue;
    }
    const worksheet = workbook.addWorksheet(sanitizeSheetName(sec.title || `Table${sheetIdx + 1}`));
    worksheet.addRow(cols.map(c => c.label));
    for (const row of rows) {
      worksheet.addRow(cols.map(c => row[c.key]));
    }
    sheetIdx++;
  }
  if (sheetIdx === 0) {
    return;
  }
  const fname = baseFileName.endsWith('.xlsx') ? baseFileName : `${baseFileName}.xlsx`;
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  });
  triggerDownload(blob, fname);
}

/** Rasterizes a DOM subtree into a single A4 PDF page (scaled to fit; long content may shrink). */
export async function exportElementToPdf(host: HTMLElement, baseFileName: string): Promise<void> {
  const html2canvas = (await import('html2canvas')).default;
  const { jsPDF } = await import('jspdf');

  const canvas = await html2canvas(host, {
    scale: 2,
    useCORS: true,
    logging: false,
    backgroundColor: '#ffffff'
  });

  const imgData = canvas.toDataURL('image/png');
  const pdf = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();
  const imgW = pageWidth;
  const imgH = (canvas.height * imgW) / canvas.width;
  const scale = Math.min(1, pageHeight / imgH);
  pdf.addImage(imgData, 'PNG', 0, 0, imgW * scale, imgH * scale);
  pdf.save(baseFileName.endsWith('.pdf') ? baseFileName : `${baseFileName}.pdf`);
}
