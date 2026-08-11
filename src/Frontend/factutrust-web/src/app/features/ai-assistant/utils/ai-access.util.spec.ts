import { canUseAiAssistant } from './ai-access.util';

describe('canUseAiAssistant', () => {
  function mockAuth(overrides: Partial<{
    permissions: string[];
    isAccountingFirm: boolean;
    isDelegatedMode: boolean;
  }> = {}) {
    const {
      permissions = ['ai:chat'],
      isAccountingFirm = false,
      isDelegatedMode = false
    } = overrides;
    return {
      hasAllPermissions: (required: readonly string[]) =>
        required.every(p => permissions.includes(p)),
      isAccountingFirm: () => isAccountingFirm,
      isDelegatedMode: () => isDelegatedMode
    };
  }

  it('allows standard tenant with ai:chat', () => {
    expect(canUseAiAssistant(mockAuth())).toBeTrue();
  });

  it('denies without ai:chat', () => {
    expect(canUseAiAssistant(mockAuth({ permissions: [] }))).toBeFalse();
  });

  it('denies accounting firm in native mode', () => {
    expect(canUseAiAssistant(mockAuth({ isAccountingFirm: true, isDelegatedMode: false }))).toBeFalse();
  });

  it('allows accounting firm in delegated mode', () => {
    expect(canUseAiAssistant(mockAuth({ isAccountingFirm: true, isDelegatedMode: true }))).toBeTrue();
  });
});
