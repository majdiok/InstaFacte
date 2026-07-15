/**
 * Generates retail-style GLB façades (theme-specific) + artistic LOD1 sibling.
 * Run: npm run virtual-street:build-glbs
 *
 * Hierarchy exported: `FaçadePack` → `StorefrontRoot` (detail) + `StorefrontRoot_LOD1` (silhouette).
 * Legacy single-root GLBs are still supported at load time.
 *
 * Composition is delegated to scripts/facade-build/ — keep this file as the
 * thin entry point. Triangle / byte budgets are enforced by validate-facade-glbs.mjs.
 */
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { buildStorefrontRoot } from './facade-build/theme-builders.mjs';
import { buildStorefrontLod1 } from './facade-build/lod1-builders.mjs';

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
const outDir = path.join(__dirname, '../src/assets/virtual-street/facades');

/** Theme index 0–4 = FacadeTheme enum (backend). Aligned with procedural scale ×1.14. */
const themes = [
  { file: 'classic.glb', w: 3.36, h: 4.73, d: 2.57, style: 'classic' },
  { file: 'modern.glb', w: 3.1, h: 4.05, d: 2.37, style: 'modern' },
  { file: 'vintage.glb', w: 3.48, h: 4.85, d: 2.64, style: 'vintage' },
  { file: 'minimal.glb', w: 3.02, h: 3.82, d: 2.3, style: 'minimal' },
  { file: 'artisan.glb', w: 3.28, h: 4.5, d: 2.49, style: 'artisan' }
];

function buildPackedFacade(t) {
  const pack = new THREE.Group();
  pack.name = 'FaçadePack';
  const high = buildStorefrontRoot(t, t.style);
  high.name = 'StorefrontRoot';
  const lod = buildStorefrontLod1(t, t.style);
  lod.name = 'StorefrontRoot_LOD1';
  pack.add(high);
  pack.add(lod);
  return pack;
}

async function exportBinary(root, destFile) {
  const exporter = new GLTFExporter();
  const arrayBuffer = await new Promise((resolve, reject) => {
    exporter.parse(
      root,
      gltf => {
        if (gltf instanceof ArrayBuffer) {
          resolve(gltf);
          return;
        }
        if (gltf?.arrayBuffer) {
          resolve(gltf.arrayBuffer);
          return;
        }
        reject(new Error('GLTFExporter: expected binary ArrayBuffer'));
      },
      err => reject(err),
      { binary: true, onlyVisible: true }
    );
  });
  fs.writeFileSync(destFile, Buffer.from(arrayBuffer));
}

async function main() {
  fs.mkdirSync(outDir, { recursive: true });
  for (const t of themes) {
    const root = buildPackedFacade(t);
    await exportBinary(root, path.join(outDir, t.file));
    console.log('Wrote', path.join(outDir, t.file));
  }
}

main().catch(e => {
  console.error(e);
  process.exit(1);
});
