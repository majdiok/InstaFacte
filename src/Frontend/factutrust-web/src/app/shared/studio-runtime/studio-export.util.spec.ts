import ExcelJS from 'exceljs';
import { exportRowsCsv, exportRowsXlsx } from './studio-export.util';

describe('studio-export.util', () => {
  beforeEach(() => {
    spyOn(URL, 'revokeObjectURL');
    const link = { click: jasmine.createSpy('click') } as unknown as HTMLAnchorElement;
    spyOn(document, 'createElement').and.returnValue(link);
  });

  it('uses semicolon separator and escapes embedded semicolons', async () => {
    let captured: Blob | undefined;
    spyOn(URL, 'createObjectURL').and.callFake((blob: Blob) => {
      captured = blob;
      return 'blob:test';
    });

    exportRowsCsv(
      'rapport',
      [{ key: 'a', label: 'Col A' }, { key: 'b', label: 'Col B' }],
      [{ a: '1;2', b: 'x' }]
    );

    const bytes = new Uint8Array(await captured!.arrayBuffer());
    expect(bytes[0]).toBe(0xef);
    expect(bytes[1]).toBe(0xbb);
    expect(bytes[2]).toBe(0xbf);

    const text = new TextDecoder('utf-8').decode(bytes);
    expect(text).toContain('Col A;Col B');
    expect(text).toContain('"1;2";x');
  });

  it('builds an xlsx workbook with headers and row values', async () => {
    let captured: Blob | undefined;
    spyOn(URL, 'createObjectURL').and.callFake((blob: Blob) => {
      captured = blob;
      return 'blob:test-xlsx';
    });

    await exportRowsXlsx(
      'export-test',
      [{ key: 'name', label: 'Nom' }, { key: 'qty', label: 'Qté' }],
      [{ name: 'Article', qty: 3 }]
    );

    expect(captured).toBeDefined();

    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.load(await captured!.arrayBuffer());
    const sheet = workbook.getWorksheet('Données');
    expect(sheet).toBeDefined();
    expect(sheet!.getRow(1).values).toEqual([, 'Nom', 'Qté']);
    expect(sheet!.getRow(2).values).toEqual([, 'Article', '3']);
  });
});