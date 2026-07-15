import { DisposableRegistry } from './street-disposable-registry';

describe('DisposableRegistry', () => {
  it('drains items in LIFO order', () => {
    const reg = new DisposableRegistry();
    const order: string[] = [];
    reg.add({
      dispose() {
        order.push('a');
      }
    });
    reg.add({
      dispose() {
        order.push('b');
      }
    });
    reg.add({
      dispose() {
        order.push('c');
      }
    });
    reg.disposeAll();
    expect(order).toEqual(['c', 'b', 'a']);
  });

  it('tolerates per-item exceptions', () => {
    const reg = new DisposableRegistry();
    let lastDisposed = false;
    reg.add({
      dispose() {
        lastDisposed = true;
      }
    });
    reg.add({
      dispose() {
        throw new Error('boom');
      }
    });
    expect(() => reg.disposeAll()).not.toThrow();
    expect(lastDisposed).toBeTrue();
  });

  it('returns the added item from add()', () => {
    const reg = new DisposableRegistry();
    const d = { dispose: () => {} };
    expect(reg.add(d)).toBe(d);
  });

  it('reports size correctly', () => {
    const reg = new DisposableRegistry();
    expect(reg.size()).toBe(0);
    reg.add({ dispose: () => {} });
    reg.add({ dispose: () => {} });
    expect(reg.size()).toBe(2);
    reg.disposeAll();
    expect(reg.size()).toBe(0);
  });

  it('addAll registers multiple items at once', () => {
    const reg = new DisposableRegistry();
    const items = [{ dispose: () => {} }, { dispose: () => {} }, { dispose: () => {} }];
    reg.addAll(items);
    expect(reg.size()).toBe(3);
  });
});
