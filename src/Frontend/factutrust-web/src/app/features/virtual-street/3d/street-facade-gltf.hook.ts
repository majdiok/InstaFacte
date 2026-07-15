import { environment } from '../../../../environments/environment';
import { disposeGltfTemplateCache } from './street-facade-gltf';

/**
 * Optional GLTF façades per FacadeTheme. When `storefrontGltfFacades` is true, templates
 * load from `/assets/virtual-street/facades/*.glb` (see README in that folder).
 */
export function isStorefrontGltfFacadesEnabled(): boolean {
  return !!environment.storefrontGltfFacades;
}

export { disposeGltfTemplateCache };
