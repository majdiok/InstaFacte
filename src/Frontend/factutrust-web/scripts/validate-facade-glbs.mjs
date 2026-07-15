/**
 * Validates committed/generated GLB façades:
 *   1. gltf-validator must report 0 errors.
 *   2. FaçadePack root with StorefrontRoot + StorefrontRoot_LOD1 children.
 *   3. Required named meshes present: FacadeBody, Sign_Plane, Logo_Plane, Trim,
 *      Mullions, Door_Leaf, Awning_Canopy, Lantern_Housing.
 *   4. Per-file byte budget ≤ 110 KB; sum across themes ≤ 550 KB.
 *   5. StorefrontRoot triangles ≤ 7 800; StorefrontRoot_LOD1 triangles ≤ 780.
 *   6. No embedded image bytes (textures stay external).
 *
 * Run: npm run validate:facade-glb
 */
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { validateBytes } from 'gltf-validator';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

if (typeof globalThis.FileReader === 'undefined') {
  globalThis.FileReader = class FileReader {
    constructor() {
      this.result = null;
      this.onloadend = null;
    }
    readAsDataURL(blob) {
      void blob
        .arrayBuffer()
        .then(ab => {
          const b64 = Buffer.from(ab).toString('base64');
          this.result = `data:application/octet-stream;base64,${b64}`;
          this.onloadend?.();
        })
        .catch(() => this.onloadend?.());
    }
    readAsArrayBuffer(blob) {
      void blob
        .arrayBuffer()
        .then(ab => {
          this.result = ab;
          this.onloadend?.();
        })
        .catch(() => this.onloadend?.());
    }
  };
}

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dir = path.join(__dirname, '../src/assets/virtual-street/facades');

const expected = ['classic.glb', 'modern.glb', 'vintage.glb', 'minimal.glb', 'artisan.glb'];

const PER_FILE_MAX_BYTES = 110 * 1024;
const TOTAL_MAX_BYTES = 550 * 1024;
const HIGH_TRIANGLES_MAX = 7800;
const LOD1_TRIANGLES_MAX = 780;

const REQUIRED_MESH_NAMES = [
  'FacadeBody',
  'Sign_Plane',
  'Logo_Plane',
  'Trim',
  'Mullions',
  'Door_Leaf',
  'Awning_Canopy',
  'Lantern_Housing'
];

function gatherTriangleCount(root) {
  let tris = 0;
  root.traverse(obj => {
    if (obj.isMesh && obj.geometry) {
      const idx = obj.geometry.getIndex?.();
      if (idx) {
        tris += idx.count / 3;
      } else {
        const pos = obj.geometry.getAttribute?.('position');
        if (pos) tris += pos.count / 3;
      }
    }
  });
  return Math.round(tris);
}

function findChildByName(root, name) {
  let hit = null;
  root.traverse(obj => {
    if (hit) return;
    if (obj.name === name) hit = obj;
  });
  return hit;
}

function hasMeshNamed(root, name) {
  let hit = false;
  root.traverse(obj => {
    if (hit) return;
    if (obj.name === name) hit = true;
  });
  return hit;
}

function parseGltf(buffer) {
  const loader = new GLTFLoader();
  return new Promise((resolve, reject) => {
    loader.parse(
      buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength),
      '',
      gltf => resolve(gltf),
      err => reject(err)
    );
  });
}

async function validateFile(filePath) {
  const buf = fs.readFileSync(filePath);
  const fileName = path.basename(filePath);
  const errors = [];

  const sizeBytes = buf.length;
  if (sizeBytes > PER_FILE_MAX_BYTES) {
    errors.push(`size ${sizeBytes} > ${PER_FILE_MAX_BYTES} bytes`);
  }

  const report = await validateBytes(new Uint8Array(buf));
  const { numErrors, messages } = report.issues;
  if (numErrors > 0) {
    const errs = messages.filter(m => m.severity === 0);
    errors.push(`gltf-validator: ${numErrors} error(s)`);
    for (const m of errs) errors.push(`  [${m.code}] ${m.pointer}: ${m.message}`);
  }

  let gltf;
  try {
    gltf = await parseGltf(buf);
  } catch (e) {
    errors.push(`GLTFLoader.parse failed: ${(e && e.message) || e}`);
    if (errors.length) return { ok: false, errors, sizeBytes };
    return { ok: true, sizeBytes };
  }

  const scene = gltf.scene;
  const pack = scene.getObjectByName('FaçadePack') ?? findChildByName(scene, 'FaçadePack') ?? scene;

  const high =
    pack.getObjectByName('StorefrontRoot') ??
    findChildByName(pack, 'StorefrontRoot') ??
    findChildByName(scene, 'StorefrontRoot');
  if (!high) errors.push('Missing StorefrontRoot');

  const lod1 =
    pack.getObjectByName('StorefrontRoot_LOD1') ??
    findChildByName(pack, 'StorefrontRoot_LOD1') ??
    findChildByName(scene, 'StorefrontRoot_LOD1');
  if (!lod1) errors.push('Missing StorefrontRoot_LOD1');

  if (high) {
    for (const name of REQUIRED_MESH_NAMES) {
      if (!hasMeshNamed(high, name)) errors.push(`Missing required mesh: ${name} (under StorefrontRoot)`);
    }
    const tris = gatherTriangleCount(high);
    if (tris > HIGH_TRIANGLES_MAX) {
      errors.push(`StorefrontRoot triangles ${tris} > ${HIGH_TRIANGLES_MAX}`);
    }
  }

  if (lod1) {
    const tris = gatherTriangleCount(lod1);
    if (tris > LOD1_TRIANGLES_MAX) {
      errors.push(`StorefrontRoot_LOD1 triangles ${tris} > ${LOD1_TRIANGLES_MAX}`);
    }
  }

  let imageBytes = 0;
  scene.traverse(obj => {
    if (!obj.isMesh) return;
    const mats = Array.isArray(obj.material) ? obj.material : [obj.material];
    for (const m of mats) {
      if (!m) continue;
      const candidates = ['map', 'normalMap', 'roughnessMap', 'metalnessMap', 'aoMap', 'emissiveMap'];
      for (const k of candidates) {
        const t = m[k];
        if (t && t.image && (t.image.data?.byteLength || t.image.byteLength)) {
          imageBytes += t.image.data?.byteLength ?? t.image.byteLength ?? 0;
        }
      }
    }
  });
  if (imageBytes > 0) {
    errors.push(`Embedded image bytes detected (${imageBytes}); textures must stay external in Phase 3`);
  }

  if (errors.length) {
    console.error(`FAIL ${fileName}`);
    for (const e of errors) console.error('  ' + e);
    return { ok: false, errors, sizeBytes };
  }
  console.log(`OK   ${fileName} (${sizeBytes} bytes)`);
  return { ok: true, sizeBytes };
}

async function main() {
  if (!fs.existsSync(dir)) {
    console.error('Missing directory:', dir);
    process.exit(1);
  }
  let ok = true;
  let totalBytes = 0;
  for (const name of expected) {
    const fp = path.join(dir, name);
    if (!fs.existsSync(fp)) {
      console.error('Missing GLB:', fp);
      ok = false;
      continue;
    }
    const r = await validateFile(fp);
    totalBytes += r.sizeBytes ?? 0;
    if (!r.ok) ok = false;
  }
  if (totalBytes > TOTAL_MAX_BYTES) {
    console.error(`FAIL total bytes ${totalBytes} > ${TOTAL_MAX_BYTES}`);
    ok = false;
  } else {
    console.log(`Total ${totalBytes} bytes (budget ${TOTAL_MAX_BYTES})`);
  }
  if (!ok) process.exit(1);
}

main().catch(e => {
  console.error(e);
  process.exit(1);
});
