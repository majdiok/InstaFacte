import { exportRowsCsv } from './studio-export.util';

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
});