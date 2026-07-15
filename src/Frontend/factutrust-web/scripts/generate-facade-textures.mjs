/**
 * Generates 4 tileable PBR texture sets (baseColor / normal / roughness / ao)
 * under src/assets/virtual-street/textures/. Pure procedural — no external assets.
 *
 * Sets:
 *  - plaster-wall  (Classic, Vintage, Artisan walls)  512×512
 *  - metal-panel   (Modern walls)                     512×512
 *  - wood-trim     (Vintage, Artisan trim)            512×512
 *  - concrete-sidewalk (ground)                       1024×1024
 *
 * Run: npm run virtual-street:build-textures
 */
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { makeNoise2D, fbm2D } from './facade-build/noise.mjs';
import { encodePng } from './facade-build/png-writer.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outRoot = path.join(__dirname, '../src/assets/virtual-street/textures');

const sets = [
  { name: 'plaster-wall', size: 256, seed: 1337, kind: 'plaster' },
  { name: 'metal-panel', size: 256, seed: 8421, kind: 'metal' },
  { name: 'wood-trim', size: 256, seed: 3927, kind: 'wood' },
  { name: 'concrete-sidewalk', size: 512, seed: 5051, kind: 'concrete' }
];

function clamp01(v) {
  return v < 0 ? 0 : v > 1 ? 1 : v;
}

function mix(a, b, t) {
  return a + (b - a) * t;
}

function lerpRgb(a, b, t) {
  return [mix(a[0], b[0], t), mix(a[1], b[1], t), mix(a[2], b[2], t)];
}

function buildPlasterBase(noise, size, seed) {
  const data = new Float32Array(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const u = (x / size) * 4;
      const v = (y / size) * 4;
      const n = fbm2D(noise, u, v, 5, 0.55, 2.1);
      data[y * size + x] = n;
    }
  }
  return data;
}

function buildMetalBase(noise, size) {
  const data = new Float32Array(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const u = (x / size) * 6;
      const v = (y / size) * 12;
      const brushed = Math.sin(v * Math.PI * 8) * 0.04;
      const n = fbm2D(noise, u, v, 3, 0.5, 2.0) * 0.6;
      data[y * size + x] = clamp01(0.45 + n * 0.4 + brushed);
    }
  }
  return data;
}

function buildWoodBase(noise, size) {
  const data = new Float32Array(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const u = (x / size) * 1.2;
      const v = (y / size) * 12;
      const ring = Math.sin((u + fbm2D(noise, u * 4, v * 0.4, 3, 0.5, 2.0) * 0.7) * Math.PI * 6);
      const grain = fbm2D(noise, u * 8, v * 0.7, 4, 0.5, 2.0);
      data[y * size + x] = clamp01(0.45 + ring * 0.18 + grain * 0.18);
    }
  }
  return data;
}

function buildConcreteBase(noise, size) {
  const data = new Float32Array(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const u = (x / size) * 4;
      const v = (y / size) * 4;
      const slabX = Math.floor((x / size) * 4);
      const slabY = Math.floor((y / size) * 4);
      const slabId = (slabX * 73856093) ^ (slabY * 19349663);
      const slabBias = ((slabId >>> 0) % 1000) / 1000 * 0.1 - 0.05;
      const grout = (x % (size / 4) < 2 || y % (size / 4) < 2) ? -0.35 : 0;
      const n = fbm2D(noise, u, v, 5, 0.5, 2.0);
      data[y * size + x] = clamp01(0.55 + n * 0.25 + slabBias + grout);
    }
  }
  return data;
}

const palettes = {
  plaster: { lo: [232, 230, 222], hi: [205, 198, 184] },
  metal: { lo: [80, 92, 108], hi: [145, 160, 178] },
  wood: { lo: [78, 51, 32], hi: [155, 110, 70] },
  concrete: { lo: [120, 124, 130], hi: [180, 184, 188] }
};

function colorize(kind, height) {
  const p = palettes[kind] ?? palettes.plaster;
  const t = clamp01(height);
  const rgb = lerpRgb(p.lo, p.hi, t);
  return [Math.round(rgb[0]), Math.round(rgb[1]), Math.round(rgb[2])];
}

function deriveNormal(height, size, strength = 4.0) {
  const buf = Buffer.alloc(size * size * 3);
  const sample = (x, y) => height[((y % size) + size) % size * size + (((x % size) + size) % size)];
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const dx = (sample(x + 1, y) - sample(x - 1, y)) * strength;
      const dy = (sample(x, y + 1) - sample(x, y - 1)) * strength;
      const nx = -dx;
      const ny = -dy;
      const nz = 1.0;
      const len = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
      const i = (y * size + x) * 3;
      buf[i] = Math.round(((nx / len) * 0.5 + 0.5) * 255);
      buf[i + 1] = Math.round(((ny / len) * 0.5 + 0.5) * 255);
      buf[i + 2] = Math.round(((nz / len) * 0.5 + 0.5) * 255);
    }
  }
  return buf;
}

function deriveRoughness(height, size, kind) {
  const buf = Buffer.alloc(size * size);
  const base = kind === 'metal' ? 0.35 : kind === 'wood' ? 0.55 : kind === 'concrete' ? 0.85 : 0.78;
  for (let i = 0; i < height.length; i++) {
    const r = clamp01(base + (height[i] - 0.5) * 0.25);
    buf[i] = Math.round(r * 255);
  }
  return buf;
}

function deriveAO(height, size) {
  const buf = Buffer.alloc(size * size);
  const sample = (x, y) => height[((y % size) + size) % size * size + (((x % size) + size) % size)];
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let occ = 0;
      const r = 2;
      let count = 0;
      for (let oy = -r; oy <= r; oy++) {
        for (let ox = -r; ox <= r; ox++) {
          occ += sample(x + ox, y + oy);
          count++;
        }
      }
      const avg = occ / count;
      const local = sample(x, y);
      const shade = clamp01(0.7 + (local - avg) * 0.8);
      buf[y * size + x] = Math.round(shade * 255);
    }
  }
  return buf;
}

function buildSet(set) {
  const noise = makeNoise2D(set.seed);
  let height;
  switch (set.kind) {
    case 'plaster':
      height = buildPlasterBase(noise, set.size, set.seed);
      break;
    case 'metal':
      height = buildMetalBase(noise, set.size);
      break;
    case 'wood':
      height = buildWoodBase(noise, set.size);
      break;
    case 'concrete':
      height = buildConcreteBase(noise, set.size);
      break;
    default:
      throw new Error('Unknown set kind: ' + set.kind);
  }

  const rgb = Buffer.alloc(set.size * set.size * 3);
  for (let i = 0; i < height.length; i++) {
    const c = colorize(set.kind, height[i]);
    rgb[i * 3] = c[0];
    rgb[i * 3 + 1] = c[1];
    rgb[i * 3 + 2] = c[2];
  }

  const normal = deriveNormal(height, set.size, set.kind === 'concrete' ? 2.6 : 3.4);
  const rough = deriveRoughness(height, set.size, set.kind);
  const ao = deriveAO(height, set.size);

  return {
    base: encodePng(set.size, set.size, 3, rgb),
    normal: encodePng(set.size, set.size, 3, normal),
    roughness: encodePng(set.size, set.size, 1, rough),
    ao: encodePng(set.size, set.size, 1, ao)
  };
}

function main() {
  fs.mkdirSync(outRoot, { recursive: true });
  for (const set of sets) {
    const dir = path.join(outRoot, set.name);
    fs.mkdirSync(dir, { recursive: true });
    const out = buildSet(set);
    fs.writeFileSync(path.join(dir, 'baseColor.png'), out.base);
    fs.writeFileSync(path.join(dir, 'normal.png'), out.normal);
    fs.writeFileSync(path.join(dir, 'roughness.png'), out.roughness);
    fs.writeFileSync(path.join(dir, 'ao.png'), out.ao);
    console.log(
      `Wrote ${set.name} (${set.size}×${set.size}) — ${(out.base.length + out.normal.length + out.roughness.length + out.ao.length) / 1024 | 0} KB`
    );
  }
}

main();
