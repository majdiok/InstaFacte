/**
 * Per-theme palette + accent colors used by the GLB generator.
 * Mirrors the values in `facade-theme-presets.ts`; do not drift independently.
 */

export function palette(style) {
  switch (style) {
    case 'classic':
      return { wall: 0x334155, trim: 0x1e293b, accent: 0x64748b, awning: 0x4f46e5, glow: 0xfff2c8 };
    case 'modern':
      return { wall: 0x1e293b, trim: 0x0f172a, accent: 0x38bdf8, awning: 0x0ea5e9, glow: 0xa5f3fc };
    case 'vintage':
      return { wall: 0x3f2e26, trim: 0x292018, accent: 0x78716c, awning: 0xb45309, glow: 0xfde68a };
    case 'minimal':
      return { wall: 0xe2e8f0, trim: 0xcbd5e1, accent: 0x94a3b8, awning: 0xf8fafc, glow: 0xffffff };
    case 'artisan':
      return { wall: 0x5c4033, trim: 0x3f2a22, accent: 0xa18072, awning: 0xc2410c, glow: 0xfed7aa };
    default:
      return { wall: 0x334155, trim: 0x1e293b, accent: 0x64748b, awning: 0x4f46e5, glow: 0xfff2c8 };
  }
}

export function awningStripeHex(style) {
  switch (style) {
    case 'classic':
      return 0xf8fafc;
    case 'modern':
      return 0xe2e8f0;
    case 'vintage':
      return 0xfde68a;
    case 'minimal':
      return 0x94a3b8;
    case 'artisan':
      return 0xfde68a;
    default:
      return 0xf8fafc;
  }
}

export function lanternGlowHex(style) {
  return palette(style).glow;
}

export function darken(hex, amount) {
  const r = Math.max(0, ((hex >> 16) & 0xff) - amount);
  const g = Math.max(0, ((hex >> 8) & 0xff) - amount);
  const b = Math.max(0, (hex & 0xff) - amount);
  return (r << 16) | (g << 8) | b;
}

export function lighten(hex, amount) {
  const r = Math.min(255, ((hex >> 16) & 0xff) + amount);
  const g = Math.min(255, ((hex >> 8) & 0xff) + amount);
  const b = Math.min(255, (hex & 0xff) + amount);
  return (r << 16) | (g << 8) | b;
}
