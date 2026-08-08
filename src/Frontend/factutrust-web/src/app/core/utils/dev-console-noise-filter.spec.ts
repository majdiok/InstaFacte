import {
  installDevConsoleNoiseFilter,
  isExtensionConsoleNoise,
  isExtensionMessagingNoise,
  resetDevConsoleNoiseFilterForTests
} from './dev-console-noise-filter';

describe('dev-console-noise-filter', () => {
  afterEach(() => {
    resetDevConsoleNoiseFilterForTests();
  });

  describe('isExtensionMessagingNoise', () => {
    it('filtre la signature exacte de messagerie d\'extension', () => {
      expect(
        isExtensionMessagingNoise(
          'Could not establish connection. Receiving end does not exist.'
        )
      ).toBeTrue();
      expect(
        isExtensionMessagingNoise({
          message: 'Could not establish connection. Receiving end does not exist.'
        })
      ).toBeTrue();
    });

    it('filtre message port closed', () => {
      expect(
        isExtensionMessagingNoise('The message port closed before a response was received.')
      ).toBeTrue();
    });

    it('ne filtre pas un rejet applicatif', () => {
      expect(isExtensionMessagingNoise('Erreur HTTP 500')).toBeFalse();
      expect(isExtensionMessagingNoise({ message: 'Session expirée' })).toBeFalse();
    });
  });

  describe('isExtensionConsoleNoise', () => {
    it('filtre [Auth] Failed to get auth status', () => {
      expect(
        isExtensionConsoleNoise(['[Auth] Failed to get auth status:', { message: 'foo' }])
      ).toBeTrue();
    });

    it('filtre l\'objet message port closed en second argument', () => {
      expect(
        isExtensionConsoleNoise([
          '[Auth] Failed to get auth status:',
          { message: 'The message port closed before a response was received.' }
        ])
      ).toBeTrue();
      expect(
        isExtensionConsoleNoise([
          '[Auth] Failed to get auth status:',
          { message: 'Could not establish connection. Receiving end does not exist.' }
        ])
      ).toBeTrue();
    });

    it('ne filtre pas un log applicatif réel', () => {
      expect(isExtensionConsoleNoise(['Erreur lors du chargement du dashboard'])).toBeFalse();
      expect(
        isExtensionConsoleNoise(['HTTP error', { status: 500, message: 'Internal Server Error' }])
      ).toBeFalse();
    });
  });

  describe('installDevConsoleNoiseFilter', () => {
    it('supprime les console.warn d\'extension sans bloquer les logs applicatifs', () => {
      const warnSpy = spyOn(console, 'warn').and.callThrough();
      installDevConsoleNoiseFilter();

      console.warn('[Auth] Failed to get auth status:', {
        message: 'Could not establish connection. Receiving end does not exist.'
      });
      console.warn('Dashboard KPI load failed');

      expect(warnSpy).toHaveBeenCalledTimes(1);
      expect(warnSpy).toHaveBeenCalledWith('Dashboard KPI load failed');
    });

    it('supprime les console.error d\'extension sans bloquer les logs applicatifs', () => {
      const errorSpy = spyOn(console, 'error').and.callThrough();
      installDevConsoleNoiseFilter();

      console.error('[Auth] Failed to get auth status:', {
        message: 'The message port closed before a response was received.'
      });
      console.error('API request failed', { status: 401 });

      expect(errorSpy).toHaveBeenCalledTimes(1);
      expect(errorSpy).toHaveBeenCalledWith('API request failed', { status: 401 });
    });

    it('est idempotent', () => {
      const warnSpy = spyOn(console, 'warn').and.callThrough();
      installDevConsoleNoiseFilter();
      installDevConsoleNoiseFilter();

      console.warn('once');
      expect(warnSpy).toHaveBeenCalledTimes(1);
    });
  });
});
