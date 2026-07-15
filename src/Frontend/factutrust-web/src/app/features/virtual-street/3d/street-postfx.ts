import type { PerspectiveCamera, Scene, WebGLRenderer } from 'three';
import type { ThreeModule } from './street-facade.builder';

export interface PostFxBundle {
  setSize(width: number, height: number, pixelRatio: number): void;
  render(): void;
  dispose(): void;
}

/**
 * Best-effort post-processing chain: RenderPass → UnrealBloomPass → FXAA.
 *
 * Returns `null` when:
 * - dynamic imports fail (Safari with disabled module workers, etc.),
 * - the renderer is WebGL1 only (UnrealBloom samples float textures),
 * - any pass throws during construction.
 *
 * The runtime always falls back to direct `renderer.render(scene, camera)` when
 * this resolves to `null`, so post-FX is a pure visual upgrade and never a hard
 * dependency.
 */
export async function createPostFxBundle(
  three: ThreeModule,
  renderer: WebGLRenderer,
  scene: Scene,
  camera: PerspectiveCamera
): Promise<PostFxBundle | null> {
  if (!renderer.capabilities.isWebGL2) return null;
  try {
    const [{ EffectComposer }, { RenderPass }, { UnrealBloomPass }, { ShaderPass }, { FXAAShader }] =
      await Promise.all([
        import('three/examples/jsm/postprocessing/EffectComposer.js'),
        import('three/examples/jsm/postprocessing/RenderPass.js'),
        import('three/examples/jsm/postprocessing/UnrealBloomPass.js'),
        import('three/examples/jsm/postprocessing/ShaderPass.js'),
        import('three/examples/jsm/shaders/FXAAShader.js')
      ]);

    const composer = new EffectComposer(renderer);
    const renderPass = new RenderPass(scene, camera);
    composer.addPass(renderPass);

    const size = new three.Vector2();
    renderer.getSize(size);
    const bloom = new UnrealBloomPass(size, 0.42, 0.7, 0.92);
    composer.addPass(bloom);

    const fxaa = new ShaderPass(FXAAShader);
    composer.addPass(fxaa);

    const updateFxaa = (w: number, h: number, pr: number): void => {
      const u = (fxaa.material as { uniforms?: { resolution?: { value: { set(x: number, y: number): void } } } }).uniforms;
      u?.resolution?.value.set(1 / (w * pr), 1 / (h * pr));
    };
    updateFxaa(size.x, size.y, renderer.getPixelRatio());

    return {
      setSize(width, height, pixelRatio) {
        composer.setSize(width, height);
        composer.setPixelRatio?.(pixelRatio);
        bloom.setSize?.(width, height);
        updateFxaa(width, height, pixelRatio);
      },
      render() {
        composer.render();
      },
      dispose() {
        try {
          composer.dispose?.();
        } catch {
          /* dispose chain best-effort */
        }
      }
    };
  } catch {
    return null;
  }
}
