import {
  isModerationReadyForSubmit,
  isValidStorefrontHexColor,
  STOREFRONT_DESCRIPTION_MAX_LENGTH
} from './storefront-profile-edit.model';

describe('storefront-profile-edit.model', () => {
  it('isModerationReadyForSubmit requires non-empty description and logo URL', () => {
    expect(isModerationReadyForSubmit('', '')).toBe(false);
    expect(isModerationReadyForSubmit('   ', 'https://x/logo.png')).toBe(false);
    expect(isModerationReadyForSubmit('Hello', '   ')).toBe(false);
    expect(isModerationReadyForSubmit('Hello', 'https://x/logo.png')).toBe(true);
  });

  it('isValidStorefrontHexColor matches #RRGGBB', () => {
    expect(isValidStorefrontHexColor('#2563eb')).toBe(true);
    expect(isValidStorefrontHexColor('#2563EB')).toBe(true);
    expect(isValidStorefrontHexColor('2563eb')).toBe(false);
    expect(isValidStorefrontHexColor('#25')).toBe(false);
  });

  it('description max constant matches domain', () => {
    expect(STOREFRONT_DESCRIPTION_MAX_LENGTH).toBe(4000);
  });
});
