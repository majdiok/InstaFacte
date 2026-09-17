import { BUSINESS_GLB_LIMITS, preflightBusinessGlb } from './business-asset-preflight';

type Json = Record<string, unknown>;
const record = (value: unknown): Json => value as Json;
const entries = (value: unknown): Json[] => value as Json[];

function triangle(): Json {
  return {
    asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }],
    buffers: [{ byteLength: 42 }], bufferViews: [{ buffer: 0, byteOffset: 0, byteLength: 36 }, { buffer: 0, byteOffset: 36, byteLength: 6 }],
    accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3' }, { bufferView: 1, componentType: 5123, count: 3, type: 'SCALAR' }],
    meshes: [{ primitives: [{ attributes: { POSITION: 0 }, indices: 1 }] }]
  };
}
function triangleBytes(): Uint8Array {
  const bytes = new Uint8Array(44);
  const view = new DataView(bytes.buffer);
  [0, 0, 0, 1, 0, 0, 0, 1, 0].forEach((value, index) => view.setFloat32(index * 4, value, true));
  [0, 1, 2].forEach((value, index) => view.setUint16(36 + index * 2, value, true));
  return bytes;
}
function encode(root: unknown, binary?: Uint8Array): Uint8Array {
  return encodeText(JSON.stringify(root), binary);
}
function encodeText(text: string, binary?: Uint8Array): Uint8Array {
  const json = new TextEncoder().encode(text);
  const padded = Math.ceil(json.length / 4) * 4;
  const bytes = new Uint8Array(20 + padded + (binary ? 8 + binary.length : 0));
  const view = new DataView(bytes.buffer);
  view.setUint32(0, 0x46546c67, true); view.setUint32(4, 2, true); view.setUint32(8, bytes.length, true);
  view.setUint32(12, padded, true); view.setUint32(16, 0x4e4f534a, true);
  bytes.fill(32, 20, 20 + padded); bytes.set(json, 20);
  if (binary) { view.setUint32(20 + padded, binary.length, true); view.setUint32(24 + padded, 0x004e4942, true); bytes.set(binary, 28 + padded); }
  return bytes;
}
function fnv1a(bytes: Uint8Array): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < bytes.length; i++) hash = Math.imul(hash ^ bytes[i], 0x01000193);
  return hash >>> 0;
}
function expectCode(bytes: Uint8Array, code: string): void {
  // Full equality diff for small inputs; cheap fingerprint for multi-MB fixtures.
  const before: Uint8Array | number = bytes.length > 1_000_000 ? fnv1a(bytes) : bytes.slice();
  const result = preflightBusinessGlb(bytes);
  expect(result.ok).withContext(JSON.stringify(result)).toBeFalse();
  if (!result.ok) expect(result.code).toBe(code);
  if (typeof before === 'number') expect(fnv1a(bytes)).withContext('preflight must not modify input').toBe(before);
  else expect(bytes).withContext('preflight must not modify input').toEqual(before);
}

describe('bounded static GLB preflight', () => {
  it('accepts an empty low-level GLB without claiming a release', () => {
    const result = preflightBusinessGlb(encode({ asset: { version: '2.0' } }));
    expect(result.ok).toBeTrue();
    if (result.ok) { expect(result.summary.nodeTriangles).toBe(0); expect(Object.isFrozen(result.summary)).toBeTrue(); }
  });
  it('measures indexed geometry from actual bytes and counts shared mesh instances', () => {
    const root = triangle(); root['nodes'] = [{ mesh: 0 }, { mesh: 0 }];
    const bytes = encode(root, triangleBytes()); const before = bytes.slice();
    const result = preflightBusinessGlb(bytes);
    expect(result.ok).withContext(JSON.stringify(result)).toBeTrue();
    if (result.ok) {
      expect(result.summary.bufferBytes).toBe(42); expect(result.summary.accessorBytes).toBe(42);
      expect(result.summary.meshTriangles).toBe(1); expect(result.summary.nodeTriangles).toBe(2);
    }
    expect(bytes).toEqual(before);
  });
  it('accepts non-indexed triangle geometry', () => {
    const root = triangle(); delete entries(entries(root['meshes'])[0]['primitives'])[0]['indices'];
    expect(preflightBusinessGlb(encode(root, triangleBytes())).ok).toBeTrue();
  });
  it('honors Uint8Array byteOffset without reading adjacent bytes', () => {
    const encoded = encode(triangle(), triangleBytes()); const envelope = new Uint8Array(encoded.length + 32);
    envelope.set(encoded, 16); expect(preflightBusinessGlb(envelope.subarray(16, 16 + encoded.length)).ok).toBeTrue();
  });
  const changes: readonly [string, (root: Json) => void, string][] = [
    ['wrong asset version', r => record(r['asset'])['version'] = '1.0', 'glb.asset-version'],
    ['unsupported minimum version', r => record(r['asset'])['minVersion'] = '2.1', 'glb.asset-version'],
    ['required Draco', r => r['extensionsRequired'] = ['KHR_draco_mesh_compression'], 'glb.extension'],
    ['optional Meshopt', r => r['extensionsUsed'] = ['EXT_meshopt_compression'], 'glb.extension'],
    ['optional KTX2', r => r['extensionsUsed'] = ['KHR_texture_basisu'], 'glb.extension'],
    ['undeclared nested extension', r => entries(r['bufferViews'])[0]['extensions'] = { EXT_meshopt_compression: {} }, 'glb.extension'],
    ['external buffer', r => entries(r['buffers'])[0]['uri'] = 'https://example.test/a.bin', 'glb.uri'],
    ['relative buffer', r => entries(r['buffers'])[0]['uri'] = 'a.bin', 'glb.uri'],
    ['data buffer', r => entries(r['buffers'])[0]['uri'] = 'data:application/octet-stream;base64,AA==', 'glb.uri'],
    ['blob buffer', r => entries(r['buffers'])[0]['uri'] = 'blob:untrusted', 'glb.uri'],
    ['image URI', r => r['images'] = [{ uri: 'data:image/png;base64,AA==' }], 'glb.uri'],
    ['embedded uninspected PNG', r => r['images'] = [{ bufferView: 0, mimeType: 'image/png' }], 'glb.unsupported-feature'],
    ['animations', r => r['animations'] = [{}], 'glb.unsupported-feature'],
    ['skins', r => r['skins'] = [{}], 'glb.unsupported-feature'],
    ['camera', r => r['cameras'] = [{}], 'glb.unsupported-feature'],
    ['sparse accessor', r => entries(r['accessors'])[0]['sparse'] = {}, 'glb.unsupported-feature'],
    ['matrix accessor', r => entries(r['accessors'])[0]['type'] = 'MAT3', 'glb.accessor-format'],
    ['morph targets', r => entries(entries(r['meshes'])[0]['primitives'])[0]['targets'] = [], 'glb.unsupported-feature'],
    ['bad buffer reference', r => entries(r['bufferViews'])[0]['buffer'] = 1, 'glb.integer'],
    ['view beyond buffer', r => entries(r['bufferViews'])[0]['byteLength'] = 43, 'glb.integer'],
    ['negative offset', r => entries(r['bufferViews'])[0]['byteOffset'] = -1, 'glb.integer'],
    ['fractional offset', r => entries(r['bufferViews'])[0]['byteOffset'] = 0.5, 'glb.integer'],
    ['unsafe arithmetic', r => entries(r['accessors'])[0]['count'] = Number.MAX_SAFE_INTEGER + 1, 'glb.integer'],
    ['huge accessor count', r => entries(r['accessors'])[0]['count'] = 1_000_001, 'glb.integer'],
    ['invalid stride', r => entries(r['bufferViews'])[0]['byteStride'] = 6, 'glb.stride'],
    ['undersized stride', r => entries(r['bufferViews'])[0]['byteStride'] = 4, 'glb.accessor-alignment'],
    ['unaligned accessor', r => entries(r['accessors'])[0]['byteOffset'] = 1, 'glb.accessor-alignment'],
    ['accessor past view', r => entries(r['accessors'])[0]['count'] = 4, 'glb.accessor-range'],
    ['missing accessor view', r => entries(r['accessors'])[0]['bufferView'] = 2, 'glb.integer'],
    ['bad POSITION reference', r => record(entries(entries(r['meshes'])[0]['primitives'])[0]['attributes'])['POSITION'] = 2, 'glb.integer'],
    ['bad material reference', r => entries(entries(r['meshes'])[0]['primitives'])[0]['material'] = 0, 'glb.integer'],
    ['unsupported lines', r => entries(entries(r['meshes'])[0]['primitives'])[0]['mode'] = 1, 'glb.primitive-mode'],
    ['bad mesh reference', r => entries(r['nodes'])[0]['mesh'] = 1, 'glb.integer'],
    ['bad scene reference', r => r['scene'] = 1, 'glb.integer'],
    ['missing child', r => entries(r['nodes'])[0]['children'] = [1], 'glb.integer'],
    ['self cycle', r => entries(r['nodes'])[0]['children'] = [0], 'glb.node-cycle'],
    ['duplicate child', r => { r['nodes'] = [{ children: [1, 1] }, {}]; }, 'glb.multiple-parents'],
    ['cycle disconnected from root', r => r['nodes'] = [{}, { children: [2] }, { children: [1] }], 'glb.node-cycle'],
    ['scene refers to child', r => { r['nodes'] = [{ children: [1] }, {}]; r['scenes'] = [{ nodes: [1] }]; }, 'glb.scene-root'],
    ['node collection bound', r => r['nodes'] = Array(4097).fill({}), 'glb.collection-limit'],
    ['JSON nesting bound', r => { let extra: unknown = {}; for (let i = 0; i < 33; i++) extra = { nested: extra }; r['extras'] = extra; }, 'glb.json-depth'],
    ['JSON string bound', r => r['extras'] = 'x'.repeat(4097), 'glb.json-string'],
    ['JSON token bound', r => r['extras'] = Array(125001).fill('x'), 'glb.json-tokens'],
    ['JSON array bound', r => r['extras'] = Array(16385).fill(0), 'glb.collection-limit'],
    ['non-empty textures', r => r['textures'] = [{}], 'glb.unsupported-feature'],
    ['mesh morph weights', r => entries(r['meshes'])[0]['weights'] = [], 'glb.unsupported-feature'],
    ['node skin', r => entries(r['nodes'])[0]['skin'] = 0, 'glb.unsupported-feature'],
    ['node camera', r => entries(r['nodes'])[0]['camera'] = 0, 'glb.unsupported-feature'],
    ['node weights', r => entries(r['nodes'])[0]['weights'] = [], 'glb.unsupported-feature'],
    ['unsupported vertex semantic', r => record(entries(entries(r['meshes'])[0]['primitives'])[0]['attributes'])['TEXCOORD_2'] = 0, 'glb.unsupported-feature'],
    ['integer position component', r => entries(r['accessors'])[0]['componentType'] = 5123, 'glb.position-format'],
    ['normalized position', r => entries(r['accessors'])[0]['normalized'] = true, 'glb.position-format'],
    ['VEC2 position', r => entries(r['accessors'])[0]['type'] = 'VEC2', 'glb.position-format'],
    ['normalized indices', r => entries(r['accessors'])[1]['normalized'] = true, 'glb.index-format'],
    ['primitive budget across meshes', r => { const mesh = { primitives: Array(2050).fill({ attributes: { POSITION: 0 } }) }; r['meshes'] = [mesh, mesh]; }, 'glb.primitive-limit']
  ];
  for (const [name, change, code] of changes) it(`rejects ${name}`, () => {
    const root = triangle(); change(root); expectCode(encode(root, triangleBytes()), code);
  });
  for (const length of [64, 65]) for (const reverse of [false, true]) it(`checks ${length} node depth with reversed order=${reverse}`, () => {
    const nodes = Array.from({ length }, (_, i) => ({ children: i + 1 < length ? [reverse ? length - i - 2 : i + 1] : [] }));
    if (reverse) nodes.reverse();
    const bytes = encode({ asset: { version: '2.0' }, nodes });
    if (length === 64) expect(preflightBusinessGlb(bytes).ok).toBeTrue(); else expectCode(bytes, 'glb.node-depth');
  });
  for (const [at, value, code] of [[0, 0, 'glb.magic'], [4, 1, 'glb.version'], [8, 20, 'glb.length'], [12, 0xffffffff, 'glb.chunk'], [16, 0x004e4942, 'glb.chunk-order']] as const) {
    it(`rejects malformed header/chunk ${at}`, () => { const bytes = encode(triangle(), triangleBytes()); new DataView(bytes.buffer).setUint32(at, value, true); expectCode(bytes, code); });
  }
  it('rejects invalid UTF8 before JSON parsing', () => { const bytes = encode({ asset: { version: '2.0' } }); bytes[20] = 0xff; expectCode(bytes, 'glb.utf8'); });
  it('rejects malformed JSON', () => expectCode(encodeText('{invalid}'), 'glb.json'));
  it('rejects nonfinite JSON numbers', () => expectCode(encodeText('{"asset":{"version":"2.0"},"extras":1e999}'), 'glb.number'));
  it('rejects invalid BIN padding', () => { const bin = triangleBytes(); bin[43] = 1; expectCode(encode(triangle(), bin), 'glb.bin-padding'); });
  it('rejects nonfinite actual vertex data', () => { const bin = triangleBytes(); new DataView(bin.buffer).setFloat32(0, Infinity, true); expectCode(encode(triangle(), bin), 'glb.nonfinite-component'); });
  it('rejects indices outside actual vertex count', () => { const bin = triangleBytes(); new DataView(bin.buffer).setUint16(36, 3, true); expectCode(encode(triangle(), bin), 'glb.index-range'); });
  it('rejects truncated files and over-ceiling JSON chunks', () => {
    expectCode(new Uint8Array(19), 'glb.header');
    const bytes = encodeText(' '.repeat(BUSINESS_GLB_LIMITS.jsonBytes + 4)); expectCode(bytes, 'glb.json-limit');
  });
  it('rejects over-ceiling total bytes before copying the input', () => {
    expectCode(new Uint8Array(BUSINESS_GLB_LIMITS.bytes + 4), 'glb.byte-limit');
  });
  it('rejects an empty JSON chunk and inconsistent BIN/buffer combinations', () => {
    expectCode(encodeText(''), 'glb.json-limit');
    expectCode(encode(triangle(), new Uint8Array(0)), 'glb.integer');
    const noBuffers = triangle(); delete noBuffers['buffers'];
    expectCode(encode(noBuffers, triangleBytes()), 'glb.buffer-length');
    expectCode(encode(triangle(), new Uint8Array(48)), 'glb.buffer-length');
  });
  for (const [name, change] of [
    ['float component', (accessor: Json) => { accessor['componentType'] = 5126; }],
    ['non-scalar shape', (accessor: Json) => { accessor['type'] = 'VEC2'; }]
  ] as const) {
    it(`rejects ${name} indices`, () => {
      const root = triangle();
      change(entries(root['accessors'])[1]);
      entries(root['bufferViews'])[1]['byteLength'] = 12;
      entries(root['buffers'])[0]['byteLength'] = 48;
      expectCode(encode(root, new Uint8Array(48)), 'glb.index-format');
    });
  }
  it('rejects a non-indexed triangle count that is not a multiple of three', () => {
    const root = triangle();
    delete entries(entries(root['meshes'])[0]['primitives'])[0]['indices'];
    entries(root['accessors'])[0]['count'] = 4;
    entries(root['bufferViews'])[0]['byteLength'] = 48;
    entries(root['buffers'])[0]['byteLength'] = 48;
    expectCode(encode(root, new Uint8Array(48)), 'glb.triangle-count');
  });
  it('rejects expanded accessor work over the byte budget with overlapping views', () => {
    const view = { buffer: 0, byteOffset: 0, byteLength: 16_000_000 };
    const accessor = { componentType: 5126, count: 1_000_000, type: 'VEC4' };
    const root: Json = { asset: { version: '2.0' }, buffers: [{ byteLength: 16_000_000 }],
      bufferViews: [0, 1, 2, 3, 4].map(bufferView => ({ ...view, bufferView })),
      accessors: [0, 1, 2, 3, 4].map(bufferView => ({ ...accessor, bufferView })) };
    expectCode(encode(root, new Uint8Array(16_000_000)), 'glb.accessor-budget');
  });
  const triangleCeiling = (primitives: number, instances: number): Uint8Array => {
    const root: Json = { asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }],
      nodes: Array.from({ length: instances }, () => ({ mesh: 0 })),
      buffers: [{ byteLength: 2_000_034 }],
      bufferViews: [{ buffer: 0, byteOffset: 0, byteLength: 36 }, { buffer: 0, byteOffset: 36, byteLength: 1_999_998 }],
      accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3' }, { bufferView: 1, componentType: 5123, count: 999_999, type: 'SCALAR' }],
      meshes: [{ primitives: Array.from({ length: primitives }, () => ({ attributes: { POSITION: 0 }, indices: 1 })) }] };
    return encode(root, new Uint8Array(2_000_036));
  };
  it('rejects declared mesh triangles over the ceiling', () => {
    expectCode(triangleCeiling(7, 1), 'glb.triangle-limit');
  });
  it('rejects instantiated triangles over the ceiling when the mesh alone fits', () => {
    expectCode(triangleCeiling(4, 2), 'glb.triangle-limit');
  });
});
