/**
 * Copies Three.js bundled Draco WASM/JS decoders into src/assets for same-origin loading
 * when environment.storefrontGltfDraco is true. Run: npm run vendor:draco
 */
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const srcDir = path.join(__dirname, '../node_modules/three/examples/jsm/libs/draco/gltf');
const destDir = path.join(__dirname, '../src/assets/vendor/draco/gltf');

function copyRecursive(from, to) {
  fs.mkdirSync(to, { recursive: true });
  for (const name of fs.readdirSync(from)) {
    const fp = path.join(from, name);
    const tp = path.join(to, name);
    if (fs.statSync(fp).isDirectory()) copyRecursive(fp, tp);
    else fs.copyFileSync(fp, tp);
  }
}

if (!fs.existsSync(srcDir)) {
  console.error('Missing Three Draco sources:', srcDir);
  process.exit(1);
}
copyRecursive(srcDir, destDir);
console.log('Copied Draco decoder to', destDir);
