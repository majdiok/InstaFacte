import * as THREE from 'three';
import { palette, awningStripeHex, darken, lighten } from './palette.mjs';
import {
  boxBeveled,
  mullionGrid,
  awningCanopy,
  lanternHousing,
  planterBox,
  addressPlate,
  interiorRecess,
  doorAssembly,
  awningBracket
} from './geometry-helpers.mjs';

/**
 * Common storefront composition used by every theme. Theme-specific details
 * (roof silhouette, wall material) are layered on top by the per-theme builders.
 *
 * Hierarchy:
 *   StorefrontRoot
 *     ├── FacadeBody
 *     ├── Trim
 *     ├── Sign_Plane
 *     ├── Logo_Plane
 *     ├── Mullions
 *     ├── Door (Door_Frame, Door_Leaf, Door_Handle)
 *     ├── Awning_Canopy + Awning_Brackets
 *     ├── Lantern (Lantern_Housing, Lantern_Bulb_Anchor)
 *     ├── Planter_Left, Planter_Right (with Foliage_*)
 *     ├── Address_Plate
 *     ├── Interior_Recess
 *     ├── Window_Prop_Anchor_Left, Window_Prop_Anchor_Right
 *     └── Roof_Style (theme-specific)
 */
function buildBaseStorefront(t, style) {
  const { w, h, d } = t;
  const c = palette(style);
  const root = new THREE.Group();

  const body = new THREE.Mesh(
    boxBeveled(w * 0.94, h * 0.92, d * 0.42, 0.06),
    new THREE.MeshStandardMaterial({
      color: c.wall,
      roughness: style === 'modern' ? 0.55 : style === 'minimal' ? 0.5 : 0.78,
      metalness: style === 'modern' ? 0.22 : 0.1
    })
  );
  body.name = 'FacadeBody';
  body.position.set(0, h * 0.46, -d * 0.08);
  body.castShadow = true;
  body.receiveShadow = true;
  root.add(body);

  const trim = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.06, 0.12, d * 1.02),
    new THREE.MeshStandardMaterial({ color: c.accent, roughness: 0.58, metalness: 0.2 })
  );
  trim.name = 'Trim';
  trim.position.set(0, 0.06, 0);
  trim.receiveShadow = true;
  root.add(trim);

  const fz = d / 2 - 0.04;
  const fy = h * 0.42;
  const gw = w * 0.74;
  const gh = h * 0.5;

  const recess = interiorRecess(gw * 0.92, gh * 0.92, 0.6);
  recess.position.set(0, 0, fz - 0.32);
  root.add(recess);

  const frameMat = new THREE.MeshStandardMaterial({
    color: darken(c.trim, 16),
    roughness: 0.45,
    metalness: 0.35
  });
  const ft = 0.06;
  const topF = new THREE.Mesh(new THREE.BoxGeometry(gw + ft * 2, ft, 0.08), frameMat);
  topF.position.set(0, fy + gh / 2 + ft / 2, fz);
  topF.castShadow = true;
  root.add(topF);
  const botF = new THREE.Mesh(new THREE.BoxGeometry(gw + ft * 2, ft, 0.08), frameMat);
  botF.position.set(0, fy - gh / 2 - ft / 2, fz);
  botF.castShadow = true;
  root.add(botF);
  const leftF = new THREE.Mesh(new THREE.BoxGeometry(ft, gh + ft * 2, 0.08), frameMat);
  leftF.position.set(-gw / 2 - ft / 2, fy, fz);
  leftF.castShadow = true;
  root.add(leftF);
  const rightF = new THREE.Mesh(new THREE.BoxGeometry(ft, gh + ft * 2, 0.08), frameMat);
  rightF.position.set(gw / 2 + ft / 2, fy, fz);
  rightF.castShadow = true;
  root.add(rightF);

  const sill = new THREE.Mesh(
    new THREE.BoxGeometry(gw + 0.2, 0.06, 0.35),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.5, metalness: 0.15 })
  );
  sill.position.set(0, fy - gh / 2 - 0.05, fz + 0.05);
  sill.receiveShadow = true;
  sill.castShadow = true;
  root.add(sill);

  const mullionMat = new THREE.MeshStandardMaterial({
    color: darken(c.trim, 24),
    roughness: 0.55,
    metalness: 0.32
  });
  const mullions = mullionGrid(gw * 0.88, gh * 0.88, 3, 2, 0.05, mullionMat);
  mullions.position.set(0, fy, fz + 0.005);
  root.add(mullions);

  const glass = new THREE.Mesh(
    new THREE.PlaneGeometry(gw * 0.86, gh * 0.86),
    new THREE.MeshPhysicalMaterial({
      color: 0xffffff,
      metalness: 0,
      roughness: 0.08,
      transparent: true,
      opacity: 0.42,
      clearcoat: 0.55,
      clearcoatRoughness: 0.12
    })
  );
  glass.name = 'FacadeGlass';
  glass.position.set(0, fy, fz + 0.012);
  root.add(glass);

  const door = doorAssembly(w, h, c);
  door.position.set(0, 0, fz + 0.03);
  root.add(door);

  const awningMat = new THREE.MeshStandardMaterial({
    color: c.awning,
    roughness: 0.85,
    metalness: 0.05,
    side: THREE.DoubleSide
  });
  const awning = awningCanopy(w * 0.94, 0.62, 0.08, awningMat);
  awning.position.set(0, h * 0.78, d * 0.22);
  awning.userData.baseRotX = -0.18;
  awning.rotation.x = -0.18;
  root.add(awning);

  const stripeMat = new THREE.MeshStandardMaterial({
    color: awningStripeHex(style),
    roughness: 0.85,
    metalness: 0.04,
    side: THREE.DoubleSide
  });
  const stripeCount = 5;
  for (let i = 0; i < stripeCount; i++) {
    const stripeWidth = (w * 0.94) / (stripeCount * 2);
    const x = -w * 0.47 + (i * 2 + 1) * stripeWidth;
    const stripe = new THREE.Mesh(new THREE.BoxGeometry(stripeWidth, 0.012, 0.62), stripeMat);
    stripe.position.set(x, h * 0.78 + 0.02, d * 0.22 - 0.31);
    stripe.rotation.x = -0.18;
    root.add(stripe);
  }
  const bracketMat = new THREE.MeshStandardMaterial({
    color: 0x111111,
    roughness: 0.4,
    metalness: 0.85
  });
  const bracketL = new THREE.Mesh(awningBracket(), bracketMat);
  bracketL.position.set(-w * 0.46, h * 0.78, d * 0.22);
  bracketL.name = 'Awning_Brackets';
  bracketL.castShadow = true;
  root.add(bracketL);
  const bracketR = new THREE.Mesh(awningBracket(), bracketMat);
  bracketR.position.set(w * 0.46 - 0.18, h * 0.78, d * 0.22);
  root.add(bracketR);

  const sign = new THREE.Mesh(
    new THREE.BoxGeometry(w * 0.88, 0.48, 0.06),
    new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.55, metalness: 0.08 })
  );
  sign.name = 'Sign_Plane';
  sign.position.set(0, h + 0.18, d * 0.12);
  sign.castShadow = true;
  root.add(sign);

  const logo = new THREE.Mesh(
    new THREE.PlaneGeometry(0.62, 0.62),
    new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.35,
      metalness: 0.05,
      transparent: true,
      opacity: 0.04
    })
  );
  logo.name = 'Logo_Plane';
  logo.position.set(w * 0.28, fy + gh * 0.15, fz + 0.045);
  root.add(logo);

  const lantern = lanternHousing(c);
  lantern.position.set(w * 0.5 + 0.04, h * 0.66, fz + 0.02);
  root.add(lantern);

  const planterL = planterBox(w, 'Left');
  planterL.position.set(-w * 0.46, 0, d * 0.32);
  root.add(planterL);
  const planterR = planterBox(w, 'Right');
  planterR.position.set(w * 0.46, 0, d * 0.32);
  root.add(planterR);

  const plate = addressPlate();
  plate.position.set(w * 0.36, h * 0.18, fz + 0.03);
  root.add(plate);

  const propAnchorL = new THREE.Object3D();
  propAnchorL.name = 'Window_Prop_Anchor_Left';
  propAnchorL.position.set(-gw * 0.28, fy - gh * 0.2, fz - 0.18);
  root.add(propAnchorL);
  const propAnchorR = new THREE.Object3D();
  propAnchorR.name = 'Window_Prop_Anchor_Right';
  propAnchorR.position.set(gw * 0.28, fy - gh * 0.2, fz - 0.18);
  root.add(propAnchorR);

  return root;
}

function buildClassicRoof(t) {
  const c = palette('classic');
  const { w, h, d } = t;
  const group = new THREE.Group();
  group.name = 'Roof_Style';
  const cornice = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.04, 0.18, d * 0.5),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.48, metalness: 0.24 })
  );
  cornice.position.set(0, h - 0.05, d * 0.12);
  cornice.castShadow = true;
  group.add(cornice);
  const parapet = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.0, 0.36, d * 0.32),
    new THREE.MeshStandardMaterial({ color: darken(c.wall, 12), roughness: 0.78, metalness: 0.1 })
  );
  parapet.position.set(0, h + 0.18, d * 0.06);
  parapet.castShadow = true;
  group.add(parapet);
  return group;
}

function buildModernRoof(t) {
  const c = palette('modern');
  const { w, h, d } = t;
  const group = new THREE.Group();
  group.name = 'Roof_Style';
  const slab = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.06, 0.08, d * 0.6),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.4, metalness: 0.4 })
  );
  slab.position.set(0, h + 0.04, d * 0.18);
  slab.castShadow = true;
  group.add(slab);
  return group;
}

function buildVintageRoof(t) {
  const c = palette('vintage');
  const { w, h, d } = t;
  const group = new THREE.Group();
  group.name = 'Roof_Style';
  const shape = new THREE.Shape();
  shape.moveTo(-w * 0.5, 0);
  shape.lineTo(0, 0.5);
  shape.lineTo(w * 0.5, 0);
  shape.lineTo(-w * 0.5, 0);
  const geom = new THREE.ExtrudeGeometry(shape, { depth: d * 0.55, bevelEnabled: false });
  geom.translate(0, 0, -d * 0.275);
  const gable = new THREE.Mesh(
    geom,
    new THREE.MeshStandardMaterial({ color: darken(c.wall, 10), roughness: 0.85, metalness: 0.05 })
  );
  gable.position.set(0, h - 0.02, d * 0.06);
  gable.castShadow = true;
  group.add(gable);
  return group;
}

function buildMinimalRoof(t) {
  const c = palette('minimal');
  const { w, h, d } = t;
  const group = new THREE.Group();
  group.name = 'Roof_Style';
  const slab = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.04, 0.05, d * 0.55),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.5, metalness: 0.18 })
  );
  slab.position.set(0, h + 0.03, d * 0.14);
  slab.castShadow = true;
  group.add(slab);
  return group;
}

function buildArtisanRoof(t) {
  const c = palette('artisan');
  const { w, h, d } = t;
  const group = new THREE.Group();
  group.name = 'Roof_Style';
  const beam = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.05, 0.16, 0.28),
    new THREE.MeshStandardMaterial({ color: darken(c.trim, 4), roughness: 0.85, metalness: 0.04 })
  );
  beam.position.set(0, h - 0.02, d * 0.42);
  beam.castShadow = true;
  group.add(beam);
  const slab = new THREE.Mesh(
    new THREE.BoxGeometry(w * 1.02, 0.1, d * 0.55),
    new THREE.MeshStandardMaterial({ color: c.trim, roughness: 0.6, metalness: 0.12 })
  );
  slab.position.set(0, h + 0.05, d * 0.14);
  slab.castShadow = true;
  group.add(slab);
  return group;
}

const roofByStyle = {
  classic: buildClassicRoof,
  modern: buildModernRoof,
  vintage: buildVintageRoof,
  minimal: buildMinimalRoof,
  artisan: buildArtisanRoof
};

export function buildStorefrontRoot(t, style) {
  const root = buildBaseStorefront(t, style);
  const roof = (roofByStyle[style] ?? buildClassicRoof)(t);
  root.add(roof);
  return root;
}
