import { createClientUuid, isUuidV4 } from './safe-random-uuid.util';

describe('createClientUuid', () => {
  const originalCrypto = globalThis.crypto;

  afterEach(() => {
    Object.defineProperty(globalThis, 'crypto', {
      configurable: true,
      writable: true,
      value: originalCrypto
    });
  });

  it('utilise crypto.randomUUID quand disponible et n\'appelle pas getRandomValues', () => {
    const getRandomValues = jasmine.createSpy('getRandomValues');
    const randomUUID = jasmine
      .createSpy('randomUUID')
      .and.returnValue('11111111-2222-4333-a444-555555555555');

    Object.defineProperty(globalThis, 'crypto', {
      configurable: true,
      writable: true,
      value: { randomUUID, getRandomValues }
    });

    expect(createClientUuid()).toBe('11111111-2222-4333-a444-555555555555');
    expect(randomUUID).toHaveBeenCalledTimes(1);
    expect(getRandomValues).not.toHaveBeenCalled();
  });

  it('fallback getRandomValues produit un UUID v4 (version 4, variant RFC 4122)', () => {
    const getRandomValues = jasmine
      .createSpy('getRandomValues')
      .and.callFake((arr: Uint8Array) => {
        for (let i = 0; i < arr.length; i++) {
          arr[i] = (i * 17 + 3) & 0xff;
        }
        return arr;
      });

    Object.defineProperty(globalThis, 'crypto', {
      configurable: true,
      writable: true,
      value: { getRandomValues }
    });

    const id = createClientUuid();
    expect(isUuidV4(id)).toBeTrue();
    expect(getRandomValues).toHaveBeenCalledTimes(1);
    // version nibble = 4
    expect(id.charAt(14)).toBe('4');
    // variant nibble in {8,9,a,b}
    expect(['8', '9', 'a', 'b']).toContain(id.charAt(19).toLowerCase());
  });

  it('sans crypto du tout : UUID v4 valide et IDs distincts', () => {
    Object.defineProperty(globalThis, 'crypto', {
      configurable: true,
      writable: true,
      value: undefined
    });

    const ids = new Set<string>();
    for (let i = 0; i < 20; i++) {
      const id = createClientUuid();
      expect(isUuidV4(id)).toBeTrue();
      ids.add(id);
    }
    expect(ids.size).toBe(20);
  });

  it('deux appels successifs produisent des IDs distincts (getRandomValues)', () => {
    let counter = 0;
    const getRandomValues = jasmine
      .createSpy('getRandomValues')
      .and.callFake((arr: Uint8Array) => {
        for (let i = 0; i < arr.length; i++) {
          arr[i] = (counter + i) & 0xff;
        }
        counter += 31;
        return arr;
      });

    Object.defineProperty(globalThis, 'crypto', {
      configurable: true,
      writable: true,
      value: { getRandomValues }
    });

    const a = createClientUuid();
    const b = createClientUuid();
    expect(a).not.toBe(b);
    expect(isUuidV4(a)).toBeTrue();
    expect(isUuidV4(b)).toBeTrue();
  });
});
