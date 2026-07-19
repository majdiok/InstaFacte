/** Format d'export d'un état comptable (aligné sur l'enum backend AccountingExportFormat). */
export type AccountingExportFormat = 'csv' | 'excel' | 'pdf';

/** Extension de fichier associée à un format d'export. */
export function exportExtension(format: AccountingExportFormat): string {
  switch (format) {
    case 'excel':
      return 'xlsx';
    case 'pdf':
      return 'pdf';
    default:
      return 'csv';
  }
}

/**
 * Déclenche le téléchargement d'un blob dans le navigateur.
 * Centralise le pattern (createObjectURL + <a download> + revokeObjectURL) auparavant dupliqué
 * dans plusieurs composants comptables.
 */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
