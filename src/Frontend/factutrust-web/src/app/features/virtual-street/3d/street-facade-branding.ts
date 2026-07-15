import type { Group } from 'three';
import type { StreetMapEntry } from '../services/street-api.service';
import { makeSignLabelTexture, type ThreeModule } from './street-facade.builder';
import { findMeshByNames, SIGN_MESH_NAMES } from './street-facade-mesh-utils';
import { parseHexColor } from './street-media-url';

/**
 * Applies tenant branding to a loaded façade (GLTF or procedural): sign canvas texture + emissive base for selection highlight.
 */
export function applyBrandingToFacadeVisual(
  T: ThreeModule,
  facadeVisualRoot: Group,
  entry: StreetMapEntry
): void {
  const sign = findMeshByNames(facadeVisualRoot, SIGN_MESH_NAMES);
  if (!sign || !sign.material || Array.isArray(sign.material)) return;

  const primary = parseHexColor(entry.brandPrimaryColorHex, 0x2563eb);
  const secondary = parseHexColor(entry.brandSecondaryColorHex, 0x0f172a);
  const tex = makeSignLabelTexture(T, entry.displayName, primary, secondary);

  const old = sign.material as import('three').Material & {
    map?: { dispose: () => void };
  };
  if ('map' in old && old.map) old.map.dispose();
  old.dispose();

  const mat = new T.MeshStandardMaterial({
    map: tex,
    roughness: 0.45,
    metalness: 0.1,
    emissive: new T.Color(primary).multiplyScalar(0.08)
  });
  sign.material = mat;
  sign.userData['emissiveBase'] = mat.emissive.clone();
}
