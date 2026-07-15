export interface Disposable {
  dispose(): void;
}

/**
 * LIFO sink for arbitrary disposable resources tied to a single VirtualStreetScene
 * lifecycle. Drained inside `VirtualStreetScene.dispose()` *before* the global GLTF
 * template / texture caches are released so per-instance materials are freed first.
 */
export class DisposableRegistry {
  private items: Disposable[] = [];

  add<T extends Disposable>(d: T): T {
    this.items.push(d);
    return d;
  }

  addAll(ds: readonly Disposable[]): void {
    for (const d of ds) this.items.push(d);
  }

  size(): number {
    return this.items.length;
  }

  disposeAll(): void {
    while (this.items.length) {
      const d = this.items.pop();
      if (!d) continue;
      try {
        d.dispose();
      } catch {
        /* swallow per-item dispose errors so one leak cannot cascade */
      }
    }
  }
}
