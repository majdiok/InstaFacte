import { getFacadeGlbUrl } from './street-facade-gltf';

describe('street-facade-gltf getFacadeGlbUrl', () => {
  it('returns same-origin path with cache-busting query for theme', () => {
    const u = getFacadeGlbUrl(1);
    expect(u).toMatch(/^\/assets\/virtual-street\/facades\/modern\.glb\?v=/);
  });
});
