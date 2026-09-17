/** Bounded, synchronous GLB2 preflight. No fetch, renderer, decoder or release approval. */
export const BUSINESS_GLB_LIMITS = Object.freeze({
  bytes: 64 * 1024 * 1024, jsonBytes: 4 * 1024 * 1024, jsonDepth: 32,
  jsonTokens: 250_000, arrayEntries: 16_384, stringCharacters: 4096,
  nodes: 4096, edges: 16_384, nodeDepth: 64, records: 4096,
  accessorElements: 1_000_000, accessorBytes: 64 * 1024 * 1024,
  primitives: 4096, triangles: 2_000_000
});

export interface BusinessGlbSummary {
  readonly encodedBytes: number;
  readonly jsonBytes: number;
  readonly bufferBytes: number;
  readonly accessorBytes: number;
  readonly nodes: number;
  readonly edges: number;
  readonly depth: number;
  readonly meshes: number;
  readonly primitives: number;
  /** Sum over mesh definitions, including meshes not instantiated by a node. */
  readonly meshTriangles: number;
  /** Sum over all nodes, not measured draw calls or the selected scene's passes. */
  readonly nodeTriangles: number;
}
export type BusinessGlbPreflight =
  | { readonly ok: true; readonly summary: BusinessGlbSummary }
  | { readonly ok: false; readonly code: string; readonly path: string; readonly message: string };

class RejectedGlb extends Error {
  constructor(readonly code: string, readonly path: string, message: string) { super(message); }
}
function reject(code: string, path: string, message: string): never { throw new RejectedGlb(code, path, message); }
function object(value: unknown, path: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) reject('glb.object', path, 'Object required');
  return value as Record<string, unknown>;
}
function integer(value: unknown, max: number, path: string, min = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < min || value > max) {
    reject('glb.integer', path, `Safe integer in ${min}..${max} required`);
  }
  return value;
}
function array(value: unknown, path: string, max: number = BUSINESS_GLB_LIMITS.records): unknown[] {
  if (!Array.isArray(value)) reject('glb.array', path, 'Array required');
  if (value.length > max) reject('glb.collection-limit', path, 'Collection exceeds preflight ceiling');
  return value;
}
function reference(value: unknown, length: number, path: string): number {
  return integer(value, length - 1, path);
}

/** Linear scan bounds nesting/token work before JSON.parse allocates its object graph. */
function boundedJson(text: string): Record<string, unknown> {
  let depth = 0;
  let tokens = 0;
  let quoted = false;
  let escaped = false;
  let stringLength = 0;
  for (const char of text) {
    if (quoted) {
      if ((char !== '"' || escaped) && ++stringLength > BUSINESS_GLB_LIMITS.stringCharacters) reject('glb.json-string', 'json', 'String exceeds ceiling');
      if (escaped) escaped = false;
      else if (char === '\\') escaped = true;
      else if (char === '"') quoted = false;
    } else {
      if (char === '"') { quoted = true; stringLength = 0; tokens++; }
      if (char === '{' || char === '[') { depth++; tokens++; }
      if (char === '}' || char === ']') depth--;
      if (char === ':' || char === ',') tokens++;
      if (depth > BUSINESS_GLB_LIMITS.jsonDepth) reject('glb.json-depth', 'json', 'JSON nesting exceeds ceiling');
      if (tokens > BUSINESS_GLB_LIMITS.jsonTokens) reject('glb.json-tokens', 'json', 'JSON token count exceeds ceiling');
    }
  }
  let parsed: unknown;
  try { parsed = JSON.parse(text); } catch { reject('glb.json', 'json', 'Invalid JSON'); }
  const root = object(parsed, 'json');
  const pending: unknown[] = [root];
  while (pending.length) {
    const value = pending.pop();
    if (typeof value === 'number' && !Number.isFinite(value)) reject('glb.number', 'json', 'Non-finite number');
    if (!value || typeof value !== 'object') continue;
    if (Array.isArray(value)) {
      array(value, 'json', BUSINESS_GLB_LIMITS.arrayEntries);
      pending.push(...value);
    } else {
      for (const [key, child] of Object.entries(value)) {
        // Closed initial subset, including undeclared extension payloads in nested data.
        if (key === 'uri') reject('glb.uri', 'json', 'All URI fields are unsupported; no implicit resource fetch');
        if (key === 'extensions' && Object.keys(object(child, 'extensions')).length) {
          reject('glb.extension', 'extensions', 'Extension payloads are not inspected by this tranche');
        }
        pending.push(child);
      }
    }
  }
  return root;
}

interface View { offset: number; length: number; stride: number | undefined }
interface Accessor { offset: number; count: number; size: number; component: number; componentBytes: number; stride: number; normalized: boolean }

const COMPONENT_BYTES: Readonly<Record<number, number | undefined>> = Object.freeze({ 5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4 });
const TYPE_COMPONENTS: Readonly<Record<string, number | undefined>> = Object.freeze({ SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 });

/** Only static, uncompressed, image-free triangle meshes are admitted initially. */
export function preflightBusinessGlb(input: Uint8Array): BusinessGlbPreflight {
  try {
    if (!(input instanceof Uint8Array) || input.byteLength < 20) reject('glb.header', 'header', 'GLB header and JSON chunk required');
    if (input.byteLength > BUSINESS_GLB_LIMITS.bytes) reject('glb.byte-limit', 'header', 'GLB exceeds byte ceiling');
    // Snapshot input before inspection; no retained mutable buffers or parsed payload escape.
    const bytes = new Uint8Array(input);
    const data = new DataView(bytes.buffer);
    if (data.getUint32(0, true) !== 0x46546c67) reject('glb.magic', 'header', 'Expected glTF magic');
    if (data.getUint32(4, true) !== 2) reject('glb.version', 'header', 'Only GLB version 2 is supported');
    if (data.getUint32(8, true) !== bytes.length) reject('glb.length', 'header', 'Declared length must equal actual bytes');
    let json: Uint8Array | undefined;
    let binary = new Uint8Array(0);
    let offset = 12;
    let chunk = 0;
    while (offset < bytes.length) {
      if (bytes.length - offset < 8) reject('glb.chunk', 'chunks', 'Truncated chunk header');
      const length = data.getUint32(offset, true);
      const type = data.getUint32(offset + 4, true);
      if (length % 4 || length > bytes.length - offset - 8) reject('glb.chunk', 'chunks', 'Invalid chunk length/alignment');
      if (chunk === 0 && type === 0x4e4f534a) {
        if (!length || length > BUSINESS_GLB_LIMITS.jsonBytes) reject('glb.json-limit', 'json', 'JSON chunk exceeds ceiling or is empty');
        json = bytes.subarray(offset + 8, offset + 8 + length);
      } else if (chunk === 1 && type === 0x004e4942) binary = bytes.subarray(offset + 8, offset + 8 + length);
      else reject('glb.chunk-order', 'chunks', 'Only JSON followed by optional BIN is supported');
      offset += 8 + length;
      chunk++;
    }
    if (!json) reject('glb.chunk-order', 'chunks', 'First chunk must be JSON');
    let text: string;
    try { text = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(json); }
    catch { reject('glb.utf8', 'json', 'JSON must be valid UTF-8'); }
    const root = boundedJson(text);
    const asset = object(root['asset'], 'asset');
    if (asset['version'] !== '2.0' || (asset['minVersion'] !== undefined && asset['minVersion'] !== '2.0')) {
      reject('glb.asset-version', 'asset', 'Only glTF 2.0 is supported');
    }
    for (const key of ['extensionsUsed', 'extensionsRequired']) {
      if (array(root[key] ?? [], key).length) reject('glb.extension', key, 'No extensions/compression are inspected yet');
    }
    for (const key of ['images', 'textures', 'skins', 'animations', 'cameras']) {
      if (array(root[key] ?? [], key).length) reject('glb.unsupported-feature', key, `${key} inspection is not implemented`);
    }
    const buffers = array(root['buffers'] ?? [], 'buffers', 1);
    let bufferBytes = 0;
    if (buffers.length) {
      bufferBytes = integer(object(buffers[0], 'buffers[0]')['byteLength'], binary.length, 'buffers[0].byteLength', 1);
      if (binary.length - bufferBytes > 3) reject('glb.buffer-length', 'buffers', 'BIN may have at most three padding bytes');
      for (let index = bufferBytes; index < binary.length; index++) {
        if (binary[index] !== 0) reject('glb.bin-padding', 'buffers', 'BIN padding must be zero');
      }
    } else if (binary.length) reject('glb.buffer-length', 'buffers', 'BIN requires one declared internal buffer');
    const views: View[] = array(root['bufferViews'] ?? [], 'bufferViews').map((entry, index) => {
      const path = `bufferViews[${index}]`;
      const view = object(entry, path);
      reference(view['buffer'], buffers.length, `${path}.buffer`);
      const start = integer(view['byteOffset'] ?? 0, bufferBytes, `${path}.byteOffset`);
      const length = integer(view['byteLength'], bufferBytes - start, `${path}.byteLength`, 1);
      let stride: number | undefined;
      if (view['byteStride'] !== undefined) {
        stride = integer(view['byteStride'], 252, `${path}.byteStride`, 4);
        if (stride % 4) reject('glb.stride', path, 'Stride must be a multiple of four');
      }
      return { offset: start, length, stride };
    });
    let accessorBytes = 0;
    const binaryData = new DataView(binary.buffer, binary.byteOffset, binary.byteLength);
    const accessors: Accessor[] = array(root['accessors'] ?? [], 'accessors').map((entry, index) => {
      const path = `accessors[${index}]`;
      const accessor = object(entry, path);
      if ('sparse' in accessor) reject('glb.unsupported-feature', path, 'Sparse accessors are not inspected');
      const view = views[reference(accessor['bufferView'], views.length, `${path}.bufferView`)];
      const component = integer(accessor['componentType'], 5126, `${path}.componentType`, 5120);
      const componentBytes = COMPONENT_BYTES[component];
      const size = TYPE_COMPONENTS[String(accessor['type'])];
      if (!componentBytes || !size) reject('glb.accessor-format', path, 'Unsupported component or accessor shape');
      const count = integer(accessor['count'], BUSINESS_GLB_LIMITS.accessorElements, `${path}.count`, 1);
      const start = integer(accessor['byteOffset'] ?? 0, view.length, `${path}.byteOffset`);
      const elementBytes = size * componentBytes;
      const stride = view.stride ?? elementBytes;
      if (stride < elementBytes || start % componentBytes || (view.offset + start) % componentBytes) {
        reject('glb.accessor-alignment', path, 'Misaligned accessor or undersized stride');
      }
      // Each factor is bounded before multiplication; subtraction avoids unchecked end offsets.
      if ((count - 1) * stride + elementBytes > view.length - start) reject('glb.accessor-range', path, 'Accessor exceeds its bufferView');
      accessorBytes += count * elementBytes;
      if (accessorBytes > BUSINESS_GLB_LIMITS.accessorBytes) reject('glb.accessor-budget', path, 'Total expanded accessor work exceeds ceiling');
      if (accessor['normalized'] !== undefined && typeof accessor['normalized'] !== 'boolean') reject('glb.accessor-format', path, 'normalized must be boolean');
      if (component === 5126) {
        for (let item = 0; item < count; item++) for (let lane = 0; lane < size; lane++) {
          if (!Number.isFinite(binaryData.getFloat32(view.offset + start + item * stride + lane * 4, true))) reject('glb.nonfinite-component', path, 'Non-finite floating component');
        }
      }
      return { offset: view.offset + start, count, size, component, componentBytes, stride, normalized: accessor['normalized'] === true };
    });
    const materials = array(root['materials'] ?? [], 'materials');
    let primitives = 0;
    let meshTriangles = 0;
    const meshCounts = array(root['meshes'] ?? [], 'meshes').map((entry, index) => {
      const mesh = object(entry, `meshes[${index}]`);
      if ('weights' in mesh) reject('glb.unsupported-feature', 'meshes', 'Morph weights are not inspected');
      let triangles = 0;
      for (const entry of array(mesh['primitives'], `meshes[${index}].primitives`)) {
        if (++primitives > BUSINESS_GLB_LIMITS.primitives) reject('glb.primitive-limit', 'meshes', 'Primitive ceiling exceeded');
        const primitive = object(entry, 'primitive');
        if ((primitive['mode'] ?? 4) !== 4) reject('glb.primitive-mode', 'primitive', 'Only TRIANGLES are inspected');
        if ('targets' in primitive) reject('glb.unsupported-feature', 'primitive', 'Morph targets are not inspected');
        if (primitive['material'] !== undefined) reference(primitive['material'], materials.length, 'primitive.material');
        const attributes = object(primitive['attributes'], 'primitive.attributes');
        const position = accessors[reference(attributes['POSITION'], accessors.length, 'primitive.POSITION')];
        if (position.component !== 5126 || position.size !== 3 || position.normalized) reject('glb.position-format', 'primitive.POSITION', 'Unnormalized FLOAT VEC3 required');
        for (const [semantic, ref] of Object.entries(attributes)) {
          if (!/^(POSITION|NORMAL|TANGENT|TEXCOORD_[01]|COLOR_0)$/.test(semantic)) reject('glb.unsupported-feature', 'primitive.attributes', 'Unsupported vertex semantic');
          if (accessors[reference(ref, accessors.length, 'primitive.attributes')].count !== position.count) reject('glb.attribute-count', 'primitive.attributes', 'Vertex counts differ');
        }
        const indices = primitive['indices'] === undefined ? undefined : accessors[reference(primitive['indices'], accessors.length, 'primitive.indices')];
        const count = indices?.count ?? position.count;
        if (count % 3) reject('glb.triangle-count', 'primitive', 'TRIANGLES requires a multiple of three elements');
        triangles += count / 3;
        meshTriangles += count / 3;
        if (meshTriangles > BUSINESS_GLB_LIMITS.triangles) reject('glb.triangle-limit', 'meshes', 'Triangle work ceiling exceeded');
        if (indices) {
          if (indices.size !== 1 || indices.normalized || ![5121, 5123, 5125].includes(indices.component) || indices.stride !== indices.componentBytes) reject('glb.index-format', 'primitive.indices', 'Tightly packed unsigned scalar indices required');
          for (let item = 0; item < indices.count; item++) {
            const at = indices.offset + item * indices.stride;
            const value = indices.component === 5121 ? binaryData.getUint8(at) : indices.component === 5123 ? binaryData.getUint16(at, true) : binaryData.getUint32(at, true);
            if (value >= position.count) reject('glb.index-range', 'primitive.indices', 'Index exceeds vertex count');
          }
        }
      }
      return triangles;
    });
    const nodes = array(root['nodes'] ?? [], 'nodes', BUSINESS_GLB_LIMITS.nodes);
    const incoming = new Array<number>(nodes.length).fill(0);
    let edges = 0;
    let nodeTriangles = 0;
    const adjacency = nodes.map((entry, index) => {
      const node = object(entry, `nodes[${index}]`);
      for (const field of ['skin', 'camera', 'weights']) if (field in node) reject('glb.unsupported-feature', 'nodes', `${field} is not inspected`);
      if (node['mesh'] !== undefined) nodeTriangles += meshCounts[reference(node['mesh'], meshCounts.length, 'node.mesh')];
      if (nodeTriangles > BUSINESS_GLB_LIMITS.triangles) reject('glb.triangle-limit', 'nodes', 'Instantiated triangle ceiling exceeded');
      return array(node['children'] ?? [], 'node.children', BUSINESS_GLB_LIMITS.edges).map(child => {
        if (++edges > BUSINESS_GLB_LIMITS.edges) reject('glb.edge-limit', 'nodes', 'Node edge ceiling exceeded');
        const target = reference(child, nodes.length, 'node.children');
        if (++incoming[target] > 1) reject('glb.multiple-parents', 'nodes', 'Node has multiple parents or duplicate edges');
        return target;
      });
    });
    const roots = incoming.map((count, index) => count ? -1 : index).filter(index => index >= 0);
    const rootSet = new Set(roots);
    const queue = [...roots];
    const depths = new Array<number>(nodes.length).fill(1);
    let depth = nodes.length ? 1 : 0;
    for (let head = 0; head < queue.length; head++) for (const child of adjacency[queue[head]]) {
      depths[child] = depths[queue[head]] + 1;
      depth = Math.max(depth, depths[child]);
      if (depth > BUSINESS_GLB_LIMITS.nodeDepth) reject('glb.node-depth', 'nodes', 'Node depth ceiling exceeded');
      if (--incoming[child] === 0) queue.push(child);
    }
    if (queue.length !== nodes.length) reject('glb.node-cycle', 'nodes', 'Node graph contains a cycle');
    const scenes = array(root['scenes'] ?? [], 'scenes', 256);
    if (root['scene'] !== undefined) reference(root['scene'], scenes.length, 'scene');
    for (const entry of scenes) for (const node of array(object(entry, 'scene')['nodes'] ?? [], 'scene.nodes')) {
      if (!rootSet.has(reference(node, nodes.length, 'scene.nodes'))) reject('glb.scene-root', 'scene.nodes', 'Scene node must be a graph root');
    }
    return { ok: true, summary: Object.freeze({ encodedBytes: bytes.length, jsonBytes: json.length, bufferBytes, accessorBytes,
      nodes: nodes.length, edges, depth, meshes: meshCounts.length, primitives, meshTriangles, nodeTriangles }) };
  } catch (error) {
    if (error instanceof RejectedGlb) return { ok: false, code: error.code, path: error.path, message: error.message };
    // Handles detached input buffers/platform decoder errors without exposing raw payloads.
    return { ok: false, code: 'glb.invalid', path: 'glb', message: 'GLB inspection failed' };
  }
}
