/**
 * Tiny seedable Perlin/value noise. Pure JS, no deps. Sufficient for 256–1024 px tileable
 * baseColor generation. Values returned in [0, 1].
 */
function mulberry32(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 0xffffffff;
  };
}

export function makeNoise2D(seed) {
  const rnd = mulberry32(seed);
  const size = 256;
  const grid = new Float32Array(size * size);
  for (let i = 0; i < grid.length; i++) grid[i] = rnd();
  const sample = (x, y) => grid[((y % size) + size) % size * size + (((x % size) + size) % size)];
  return (x, y) => {
    const xi = Math.floor(x);
    const yi = Math.floor(y);
    const xf = x - xi;
    const yf = y - yi;
    const u = xf * xf * (3 - 2 * xf);
    const v = yf * yf * (3 - 2 * yf);
    const a = sample(xi, yi);
    const b = sample(xi + 1, yi);
    const c = sample(xi, yi + 1);
    const d = sample(xi + 1, yi + 1);
    const top = a * (1 - u) + b * u;
    const bot = c * (1 - u) + d * u;
    return top * (1 - v) + bot * v;
  };
}

export function fbm2D(noise, x, y, octaves = 4, persistence = 0.5, lacunarity = 2.0) {
  let amp = 1;
  let freq = 1;
  let total = 0;
  let max = 0;
  for (let i = 0; i < octaves; i++) {
    total += amp * noise(x * freq, y * freq);
    max += amp;
    amp *= persistence;
    freq *= lacunarity;
  }
  return total / max;
}
