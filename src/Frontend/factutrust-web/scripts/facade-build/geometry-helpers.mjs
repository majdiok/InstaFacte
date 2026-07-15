/**
 * Mesh-building helpers shared by all theme builders. Pure THREE — no DOM,
 * no asset I/O, so they work inside the Node-side GLB exporter.
 *
 * Triangle budgets are conservative; final per-theme StorefrontRoot must stay
 * under 7 800 tris (validator hard-fails).
 */
import * as THREE from 'three';

export function boxBeveled(w, h, d, bevel) {
  const b = Math.min(bevel, w / 4, h / 4, d / 4);
  if (b <= 0.0001) return new THREE.BoxGeometry(w, h, d);
  const shape = new THREE.Shape();
  shape.moveTo(-w / 2 + b, -h / 2);
  shape.lineTo(w / 2 - b, -h / 2);
  shape.quadraticCurveTo(w / 2, -h / 2, w / 2, -h / 2 + b);
  shape.lineTo(w / 2, h / 2 - b);
  shape.quadraticCurveTo(w / 2, h / 2, w / 2 - b, h / 2);
  shape.lineTo(-w / 2 + b, h / 2);
  shape.quadraticCurveTo(-w / 2, h / 2, -w / 2, h / 2 - b);
  shape.lineTo(-w / 2, -h / 2 + b);
  shape.quadraticCurveTo(-w / 2, -h / 2, -w / 2 + b, -h / 2);
  const extrude = new THREE.ExtrudeGeometry(shape, {
    depth: d,
    bevelEnabled: true,
    bevelSize: b * 0.4,
    bevelThickness: b * 0.4,
    bevelSegments: 1,
    curveSegments: 4
  });
  extrude.translate(0, 0, -d / 2);
  return extrude;
}

export function mullionGrid(width, height, cols, rows, thickness, mat) {
  const group = new THREE.Group();
  group.name = 'Mullions';
  const t = thickness;
  for (let i = 1; i < cols; i++) {
    const x = -width / 2 + (i / cols) * width;
    const m = new THREE.Mesh(new THREE.BoxGeometry(t, height, t), mat);
    m.position.set(x, 0, 0);
    m.castShadow = true;
    group.add(m);
  }
  for (let j = 1; j < rows; j++) {
    const y = -height / 2 + (j / rows) * height;
    const m = new THREE.Mesh(new THREE.BoxGeometry(width, t, t), mat);
    m.position.set(0, y, 0);
    m.castShadow = true;
    group.add(m);
  }
  return group;
}

/**
 * Lathed canopy with a slight sag — 12 segments. Returns a Mesh named `Awning_Canopy`.
 */
export function awningCanopy(width, depth, sag, mat) {
  const segments = 12;
  const positions = [];
  const indices = [];
  const uvs = [];
  for (let i = 0; i <= segments; i++) {
    const u = i / segments;
    const x = -width / 2 + u * width;
    const ys = Math.sin(u * Math.PI) * sag;
    positions.push(x, ys, 0);
    uvs.push(u, 0);
    positions.push(x, -0.04 + ys * 0.6, -depth);
    uvs.push(u, 1);
  }
  for (let i = 0; i < segments; i++) {
    const a = i * 2;
    indices.push(a, a + 1, a + 2, a + 1, a + 3, a + 2);
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  const mesh = new THREE.Mesh(geo, mat);
  mesh.name = 'Awning_Canopy';
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

export function lanternHousing(palette) {
  const group = new THREE.Group();
  group.name = 'Lantern';

  const armMat = new THREE.MeshStandardMaterial({
    color: 0x111111,
    roughness: 0.4,
    metalness: 0.85
  });
  const arm = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.04, 0.04), armMat);
  arm.position.set(0.16, 0, 0);
  arm.castShadow = true;
  group.add(arm);

  const housingMat = new THREE.MeshStandardMaterial({
    color: 0x222222,
    roughness: 0.35,
    metalness: 0.78
  });
  const housing = new THREE.Mesh(new THREE.CylinderGeometry(0.14, 0.18, 0.32, 6), housingMat);
  housing.name = 'Lantern_Housing';
  housing.position.set(0.32, -0.18, 0);
  housing.castShadow = true;
  group.add(housing);

  const glassMat = new THREE.MeshStandardMaterial({
    color: palette.glow,
    roughness: 0.35,
    metalness: 0,
    emissive: palette.glow,
    emissiveIntensity: 0.55
  });
  const glass = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.15, 0.26, 6), glassMat);
  glass.position.set(0.32, -0.18, 0);
  group.add(glass);

  const anchor = new THREE.Object3D();
  anchor.name = 'Lantern_Bulb_Anchor';
  anchor.position.set(0.32, -0.18, 0);
  group.add(anchor);

  return group;
}

export function planterBox(width, suffix) {
  const group = new THREE.Group();
  group.name = `Planter_${suffix}`;
  const boxMat = new THREE.MeshStandardMaterial({
    color: 0x3b2a1c,
    roughness: 0.85,
    metalness: 0.05
  });
  const box = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.32, 0.36), boxMat);
  box.position.y = 0.16;
  box.castShadow = true;
  box.receiveShadow = true;
  group.add(box);
  const foliageMat = new THREE.MeshStandardMaterial({
    color: 0x4a7c2e,
    roughness: 0.82,
    metalness: 0
  });
  const foliage = new THREE.Mesh(new THREE.IcosahedronGeometry(0.22, 0), foliageMat);
  foliage.name = `Foliage_${suffix}`;
  foliage.position.y = 0.46;
  foliage.castShadow = true;
  group.add(foliage);
  return group;
}

export function addressPlate() {
  const mat = new THREE.MeshStandardMaterial({
    color: 0xf8fafc,
    roughness: 0.6,
    metalness: 0.18
  });
  const plate = new THREE.Mesh(new THREE.PlaneGeometry(0.24, 0.18), mat);
  plate.name = 'Address_Plate';
  return plate;
}

/**
 * 3 inward planes forming a recess behind the glass. Vertex colors give a baked
 * dark interior so the recess reads even without scene lights.
 */
export function interiorRecess(w, h, d) {
  const mat = new THREE.MeshStandardMaterial({
    color: 0x0b1220,
    roughness: 0.95,
    metalness: 0,
    side: THREE.DoubleSide
  });
  const back = new THREE.Mesh(new THREE.PlaneGeometry(w, h), mat);
  back.position.set(0, h / 2, -d);
  back.name = 'Interior_Back';
  const left = new THREE.Mesh(new THREE.PlaneGeometry(d, h), mat);
  left.rotation.y = Math.PI / 2;
  left.position.set(-w / 2, h / 2, -d / 2);
  const right = new THREE.Mesh(new THREE.PlaneGeometry(d, h), mat);
  right.rotation.y = -Math.PI / 2;
  right.position.set(w / 2, h / 2, -d / 2);
  const group = new THREE.Group();
  group.name = 'Interior_Recess';
  group.add(back, left, right);
  return group;
}

export function doorAssembly(w, h, palette) {
  const door = new THREE.Group();
  door.name = 'Door';
  const dw = Math.min(w * 0.32, 1.05);
  const dh = h * 0.55;
  const frameMat = new THREE.MeshStandardMaterial({
    color: palette.trim,
    roughness: 0.5,
    metalness: 0.25
  });
  const leafMat = new THREE.MeshStandardMaterial({
    color: palette.wall,
    roughness: 0.45,
    metalness: 0.2
  });
  const handleMat = new THREE.MeshStandardMaterial({
    color: 0xd4af37,
    roughness: 0.18,
    metalness: 0.92
  });

  const ft = 0.05;
  const frameTop = new THREE.Mesh(new THREE.BoxGeometry(dw + ft * 2, ft, 0.07), frameMat);
  frameTop.position.set(0, dh + ft / 2, 0);
  const frameLeft = new THREE.Mesh(new THREE.BoxGeometry(ft, dh + ft, 0.07), frameMat);
  frameLeft.position.set(-(dw / 2 + ft / 2), dh / 2, 0);
  const frameRight = new THREE.Mesh(new THREE.BoxGeometry(ft, dh + ft, 0.07), frameMat);
  frameRight.position.set(dw / 2 + ft / 2, dh / 2, 0);
  const frame = new THREE.Group();
  frame.name = 'Door_Frame';
  frame.add(frameTop, frameLeft, frameRight);
  door.add(frame);

  const leaf = new THREE.Mesh(new THREE.BoxGeometry(dw, dh, 0.05), leafMat);
  leaf.name = 'Door_Leaf';
  leaf.position.set(0, dh / 2, -0.01);
  leaf.userData.openable = true;
  leaf.castShadow = true;
  door.add(leaf);

  const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 0.16, 12), handleMat);
  handle.name = 'Door_Handle';
  handle.rotation.z = Math.PI / 2;
  handle.position.set(dw * 0.36, dh * 0.5, 0.04);
  door.add(handle);

  return door;
}

export function awningBracket() {
  const shape = new THREE.Shape();
  shape.moveTo(0, 0);
  shape.lineTo(0.18, 0);
  shape.lineTo(0, -0.22);
  shape.lineTo(0, 0);
  return new THREE.ExtrudeGeometry(shape, {
    depth: 0.04,
    bevelEnabled: false
  });
}
