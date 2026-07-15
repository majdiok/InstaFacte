import type { Color, Group, Mesh, Object3D, Texture } from 'three';
import type { StreetMapEntry } from '../services/street-api.service';
import { getFacadePreset } from './facade-theme-presets';
import { parseHexColor } from './street-media-url';
import { findMeshByNames, resolveFacadeVisualRoot, LOGO_MESH_NAMES, SIGN_MESH_NAMES } from './street-facade-mesh-utils';
import { vsEnv } from './street-env';

export type ThreeModule = typeof import('three');

interface FacadePalette {
  primary: number;
  secondary: number;
}

function lighten(hex: number, amount: number): number {
  const r = Math.min(255, Math.round(((hex >> 16) & 0xff) + amount));
  const g = Math.min(255, Math.round(((hex >> 8) & 0xff) + amount));
  const b = Math.min(255, Math.round((hex & 0xff) + amount));
  return (r << 16) | (g << 8) | b;
}

function darken(hex: number, amount: number): number {
  const r = Math.max(0, Math.round(((hex >> 16) & 0xff) - amount));
  const g = Math.max(0, Math.round(((hex >> 8) & 0xff) - amount));
  const b = Math.max(0, Math.round((hex & 0xff) - amount));
  return (r << 16) | (g << 8) | b;
}

function buildMullions(T: ThreeModule, w: number, h: number, palette: FacadePalette): Mesh {
  const mat = new T.MeshStandardMaterial({
    color: darken(palette.secondary, 24),
    roughness: 0.55,
    metalness: 0.32
  });
  const cols = 3;
  const rows = 2;
  const t = 0.04;
  const grid = new T.Group();
  const innerW = w * 0.78;
  const innerH = h * 0.55;
  for (let i = 1; i < cols; i++) {
    const x = -innerW / 2 + (i / cols) * innerW;
    const m = new T.Mesh(new T.BoxGeometry(t, innerH, t), mat);
    m.position.set(x, 0, 0);
    grid.add(m);
  }
  for (let j = 1; j < rows; j++) {
    const y = -innerH / 2 + (j / rows) * innerH;
    const m = new T.Mesh(new T.BoxGeometry(innerW, t, t), mat);
    m.position.set(0, y, 0);
    grid.add(m);
  }
  const mullions = new T.Mesh(new T.BoxGeometry(0.001, 0.001, 0.001), mat);
  mullions.add(...grid.children);
  mullions.name = 'Mullions';
  return mullions;
}

function buildDoor(T: ThreeModule, w: number, h: number, palette: FacadePalette): Group {
  const door = new T.Group();
  door.name = 'Door';
  const dw = Math.min(w * 0.32, 1.05);
  const dh = h * 0.55;
  const frameMat = new T.MeshStandardMaterial({
    color: darken(palette.primary, 30),
    roughness: 0.5,
    metalness: 0.25
  });
  const leafMat = new T.MeshStandardMaterial({
    color: darken(palette.secondary, 14),
    roughness: 0.45,
    metalness: 0.2
  });
  const handleMat = new T.MeshStandardMaterial({
    color: 0xd4af37,
    roughness: 0.18,
    metalness: 0.92
  });

  const ft = 0.05;
  const frameTop = new T.Mesh(new T.BoxGeometry(dw + ft * 2, ft, 0.07), frameMat);
  frameTop.position.set(0, dh + ft / 2, 0);
  const frameLeft = new T.Mesh(new T.BoxGeometry(ft, dh + ft, 0.07), frameMat);
  frameLeft.position.set(-(dw / 2 + ft / 2), dh / 2, 0);
  const frameRight = new T.Mesh(new T.BoxGeometry(ft, dh + ft, 0.07), frameMat);
  frameRight.position.set(dw / 2 + ft / 2, dh / 2, 0);
  const frame = new T.Group();
  frame.name = 'Door_Frame';
  frame.add(frameTop, frameLeft, frameRight);
  door.add(frame);

  const leaf = new T.Mesh(new T.BoxGeometry(dw, dh, 0.05), leafMat);
  leaf.name = 'Door_Leaf';
  leaf.position.set(0, dh / 2, -0.01);
  leaf.userData['openable'] = true;
  door.add(leaf);

  const handle = new T.Mesh(new T.CylinderGeometry(0.025, 0.025, 0.16, 12), handleMat);
  handle.name = 'Door_Handle';
  handle.rotation.z = Math.PI / 2;
  handle.position.set(dw * 0.36, dh * 0.5, 0.04);
  door.add(handle);

  return door;
}

function buildAwningSimple(T: ThreeModule, w: number, palette: FacadePalette): Mesh {
  const segments = 12;
  const width = w * 0.96;
  const depth = 0.6;
  const sag = 0.08;
  const positions: number[] = [];
  const indices: number[] = [];
  const normals: number[] = [];
  const uvs: number[] = [];
  for (let i = 0; i <= segments; i++) {
    const u = i / segments;
    const x = -width / 2 + u * width;
    const dropFront = -depth + sag * Math.sin(u * Math.PI);
    positions.push(x, 0, 0);
    normals.push(0, 1, 0);
    uvs.push(u, 0);
    positions.push(x, -0.05 + Math.sin(u * Math.PI) * 0.02, dropFront);
    normals.push(0, 1, 0);
    uvs.push(u, 1);
  }
  for (let i = 0; i < segments; i++) {
    const a = i * 2;
    const b = a + 1;
    const c = a + 2;
    const d = a + 3;
    indices.push(a, b, c, b, d, c);
  }
  const geo = new T.BufferGeometry();
  geo.setAttribute('position', new T.Float32BufferAttribute(positions, 3));
  geo.setAttribute('normal', new T.Float32BufferAttribute(normals, 3));
  geo.setAttribute('uv', new T.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  const mat = new T.MeshStandardMaterial({
    color: palette.primary,
    roughness: 0.85,
    metalness: 0.05,
    side: T.DoubleSide
  });
  const awning = new T.Mesh(geo, mat);
  awning.name = 'Awning_Canopy';
  awning.userData['baseRotX'] = 0;
  awning.castShadow = true;
  awning.receiveShadow = true;
  return awning;
}

function buildLanternSimple(T: ThreeModule, palette: FacadePalette): Group {
  const lantern = new T.Group();
  lantern.name = 'Lantern';

  const armMat = new T.MeshStandardMaterial({
    color: 0x111111,
    roughness: 0.4,
    metalness: 0.85
  });
  const arm = new T.Mesh(new T.BoxGeometry(0.32, 0.04, 0.04), armMat);
  arm.position.set(0.16, 0, 0);
  lantern.add(arm);

  const housingMat = new T.MeshStandardMaterial({
    color: 0x222222,
    roughness: 0.35,
    metalness: 0.78
  });
  const housing = new T.Mesh(new T.CylinderGeometry(0.14, 0.18, 0.32, 6), housingMat);
  housing.name = 'Lantern_Housing';
  housing.position.set(0.32, -0.18, 0);
  lantern.add(housing);

  const glassMat = new T.MeshPhysicalMaterial({
    color: 0xfff2c8,
    roughness: 0.2,
    metalness: 0,
    transmission: 0.7,
    thickness: 0.15,
    emissive: new T.Color(lighten(palette.primary, 80)),
    emissiveIntensity: 0.6
  });
  const glass = new T.Mesh(new T.CylinderGeometry(0.11, 0.15, 0.26, 6), glassMat);
  glass.position.set(0.32, -0.18, 0);
  lantern.add(glass);

  const anchor = new T.Object3D();
  anchor.name = 'Lantern_Bulb_Anchor';
  anchor.position.set(0.32, -0.18, 0);
  lantern.add(anchor);

  return lantern;
}

function buildPlanters(T: ThreeModule, w: number): Group {
  const group = new T.Group();
  const boxMat = new T.MeshStandardMaterial({
    color: 0x3b2a1c,
    roughness: 0.85,
    metalness: 0.05
  });
  const foliageMat = new T.MeshStandardMaterial({
    color: 0x4a7c2e,
    roughness: 0.85,
    metalness: 0,
    emissive: 0x0d1f0a,
    emissiveIntensity: 0.06
  });
  const make = (xOffset: number, suffix: 'Left' | 'Right'): Group => {
    const planter = new T.Group();
    planter.name = `Planter_${suffix}`;
    const box = new T.Mesh(new T.BoxGeometry(0.4, 0.32, 0.36), boxMat);
    box.position.y = 0.16;
    box.castShadow = true;
    box.receiveShadow = true;
    planter.add(box);
    const foliage = new T.Mesh(new T.IcosahedronGeometry(0.22, 0), foliageMat);
    foliage.name = `Foliage_${suffix}`;
    foliage.position.y = 0.46;
    foliage.castShadow = true;
    planter.add(foliage);
    planter.position.x = xOffset;
    return planter;
  };
  group.add(make(-w * 0.46, 'Left'));
  group.add(make(w * 0.46, 'Right'));
  return group;
}

function buildAddressPlate(T: ThreeModule, idx: number): Mesh {
  const canvas = document.createElement('canvas');
  canvas.width = 128;
  canvas.height = 128;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.fillStyle = '#0f172a';
    ctx.fillRect(0, 0, 128, 128);
    ctx.strokeStyle = '#94a3b8';
    ctx.lineWidth = 4;
    ctx.strokeRect(8, 8, 112, 112);
    ctx.fillStyle = '#f8fafc';
    ctx.font = 'bold 64px system-ui,Segoe UI,sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(idx + 1), 64, 70);
  }
  const tex = new T.CanvasTexture(canvas);
  tex.colorSpace = T.SRGBColorSpace;
  const plate = new T.Mesh(
    new T.PlaneGeometry(0.22, 0.22),
    new T.MeshStandardMaterial({
      map: tex,
      roughness: 0.6,
      metalness: 0.18
    })
  );
  plate.name = 'Address_Plate';
  return plate;
}

function buildRichStorefrontExtras(
  T: ThreeModule,
  group: Group,
  entry: StreetMapEntry,
  preset: ReturnType<typeof getFacadePreset>,
  palette: FacadePalette
): void {
  const { bodyWidth: bw, bodyHeight: bh, bodyDepth: bd } = preset;

  const mullions = buildMullions(T, bw, bh, palette);
  mullions.position.set(0, bh * 0.48, bd / 2 + 0.025);
  group.add(mullions);

  const door = buildDoor(T, bw, bh, palette);
  door.position.set(0, 0, bd / 2 + 0.01);
  group.add(door);

  const awning = buildAwningSimple(T, bw, palette);
  awning.position.set(0, bh * 0.84, bd / 2 + 0.02);
  group.add(awning);

  const lantern = buildLanternSimple(T, palette);
  lantern.position.set(bw * 0.5 + 0.04, bh * 0.66, bd / 2 + 0.02);
  group.add(lantern);

  const planters = buildPlanters(T, bw);
  planters.position.set(0, 0, bd / 2 + 0.32);
  group.add(planters);

  const plate = buildAddressPlate(T, Math.max(0, entry.streetPositionIndex | 0));
  plate.position.set(bw * 0.36, bh * 0.18, bd / 2 + 0.03);
  group.add(plate);
}

function initialsFromDisplayName(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0]! + parts[1]![0]!).toUpperCase();
}

export function makeSignLabelTexture(
  T: ThreeModule,
  text: string,
  primaryHex: number,
  secondaryHex: number
): Texture {
  const w = 512;
  const h = 128;
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    const tex = new T.CanvasTexture(canvas);
    return tex;
  }
  const g = ctx.createLinearGradient(0, 0, w, h);
  const toCss = (n: number) => '#' + n.toString(16).padStart(6, '0');
  g.addColorStop(0, toCss(primaryHex));
  g.addColorStop(1, toCss(secondaryHex));
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, w, h);
  ctx.fillStyle = 'rgba(15,23,42,0.88)';
  ctx.fillRect(8, 8, w - 16, h - 16);
  ctx.font = 'bold 52px system-ui,Segoe UI,sans-serif';
  ctx.fillStyle = '#f8fafc';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  const safe = text.length > 42 ? text.slice(0, 40) + '…' : text;
  ctx.fillText(safe, w / 2, h / 2 - 10);
  ctx.font = '600 28px system-ui,Segoe UI,sans-serif';
  ctx.fillStyle = '#94a3b8';
  ctx.fillText(initialsFromDisplayName(text), w / 2, h / 2 + 28);
  const tex = new T.CanvasTexture(canvas);
  tex.colorSpace = T.SRGBColorSpace;
  tex.needsUpdate = true;
  return tex;
}

/**
 * Procedural « galerie moderne » module: corps, vitrine verre, enseigne, plan logo (texture async).
 */
export function buildStorefrontGroup(T: ThreeModule, entry: StreetMapEntry): Group {
  const preset = getFacadePreset(entry.facadeTheme);
  const primary = parseHexColor(entry.brandPrimaryColorHex, 0x2563eb);
  const secondary = parseHexColor(entry.brandSecondaryColorHex, 0x0f172a);

  const group = new T.Group();
  group.userData['pickSlug'] = entry.slug;
  group.userData['displayName'] = entry.displayName;

  const { bodyWidth: bw, bodyHeight: bh, bodyDepth: bd, signBandHeight: sh } = preset;

  const bodyGeo = new T.BoxGeometry(bw, bh, bd);
  const bodyMat = new T.MeshPhysicalMaterial({
    color: secondary,
    roughness: preset.wallRoughness,
    metalness: preset.metalness,
    clearcoat: 0.15,
    clearcoatRoughness: 0.4
  });
  const body = new T.Mesh(bodyGeo, bodyMat);
  body.position.y = bh / 2;
  body.castShadow = true;
  body.receiveShadow = true;
  body.name = 'facadeBody';
  group.add(body);

  const glassGeo = new T.PlaneGeometry(bw * 0.92, bh * 0.72);
  const glassMat = new T.MeshPhysicalMaterial({
    color: 0xffffff,
    metalness: 0,
    roughness: preset.glassRoughness,
    transmission: preset.glassTransmission,
    thickness: 0.45,
    transparent: true,
    opacity: 1,
    ior: 1.45
  });
  const glass = new T.Mesh(glassGeo, glassMat);
  glass.position.set(0, bh * 0.48, bd / 2 + 0.02);
  glass.name = 'facadeGlass';
  group.add(glass);

  const signGeo = new T.BoxGeometry(bw * 1.02, sh, bd * 0.35);
  const signTex = makeSignLabelTexture(T, entry.displayName, primary, secondary);
  const signMat = new T.MeshStandardMaterial({
    map: signTex,
    roughness: 0.45,
    metalness: 0.1,
    emissive: new T.Color(primary).multiplyScalar(0.08)
  });
  const sign = new T.Mesh(signGeo, signMat);
  sign.position.set(0, bh + sh / 2 - 0.02, bd * 0.12);
  sign.name = 'facadeSign';
  sign.userData['emissiveBase'] = signMat.emissive.clone() as Color;
  group.add(sign);

  const logoGeo = new T.PlaneGeometry(bw * 0.35, bw * 0.35);
  const logoMat = new T.MeshStandardMaterial({
    color: 0xffffff,
    roughness: 0.35,
    metalness: 0.05,
    transparent: true,
    opacity: 0.001
  });
  const logo = new T.Mesh(logoGeo, logoMat);
  logo.position.set(bw * 0.28, bh * 0.55, bd / 2 + 0.04);
  logo.name = 'facadeLogo';
  logo.visible = false;
  group.add(logo);

  const corniceGeo = new T.BoxGeometry(bw * 1.08 * preset.corniceScale, 0.12, bd * 1.05);
  const corniceMat = new T.MeshStandardMaterial({
    color: primary,
    roughness: 0.55,
    metalness: 0.12
  });
  const cornice = new T.Mesh(corniceGeo, corniceMat);
  cornice.position.set(0, bh - 0.06, 0);
  cornice.name = 'facadeCornice';
  group.add(cornice);

  if (vsEnv.storefrontProceduralRichEnabled) {
    buildRichStorefrontExtras(T, group, entry, preset, { primary, secondary });
  }

  return group;
}

export function applyLogoTextureToGroup(
  T: ThreeModule,
  group: Group,
  texture: Texture,
  maxEdgePx = 256
): void {
  const root = resolveFacadeVisualRoot(group);
  const logo = findMeshByNames(root, LOGO_MESH_NAMES);
  if (!logo || !logo.material) return;

  let map = texture;
  if (texture.image && texture.image.width > maxEdgePx) {
    const scale = maxEdgePx / Math.max(texture.image.width, texture.image.height);
    const tw = Math.max(1, Math.round(texture.image.width * scale));
    const th = Math.max(1, Math.round(texture.image.height * scale));
    const c = document.createElement('canvas');
    c.width = tw;
    c.height = th;
    const ctx = c.getContext('2d');
    if (ctx) {
      ctx.drawImage(texture.image as HTMLImageElement, 0, 0, tw, th);
      map = new T.CanvasTexture(c);
      map.colorSpace = T.SRGBColorSpace;
      map.needsUpdate = true;
      texture.dispose();
    }
  }

  const mat = logo.material as import('three').MeshStandardMaterial;
  if (mat.map) mat.map.dispose();
  mat.map = map;
  mat.opacity = 1;
  mat.transparent = false;
  mat.needsUpdate = true;
  logo.visible = true;
}

export function setGroupHighlight(T: ThreeModule, group: Group, active: boolean): void {
  const root = resolveFacadeVisualRoot(group);
  const sign = findMeshByNames(root, SIGN_MESH_NAMES);
  if (!sign || !sign.material || Array.isArray(sign.material)) return;
  const m = sign.material as import('three').MeshStandardMaterial;
  const base = sign.userData['emissiveBase'] as Color | undefined;
  if (!base) return;
  if (active) {
    m.emissive = base.clone().add(new T.Color(0x38bdf8).multiplyScalar(0.35));
  } else {
    m.emissive = base.clone();
  }
  m.needsUpdate = true;
}
