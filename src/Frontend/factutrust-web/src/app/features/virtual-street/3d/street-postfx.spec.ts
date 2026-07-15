import * as THREE from 'three';
import { createPostFxBundle } from './street-postfx';

describe('createPostFxBundle', () => {
  it('returns null when the renderer is not WebGL2', async () => {
    const fakeRenderer = {
      capabilities: { isWebGL2: false, getMaxAnisotropy: () => 1 },
      getSize: (v: { set: (x: number, y: number) => unknown }) => v.set(640, 480),
      getPixelRatio: () => 1
    } as unknown as THREE.WebGLRenderer;
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera();
    const bundle = await createPostFxBundle(THREE, fakeRenderer, scene, camera);
    expect(bundle).toBeNull();
  });
});
