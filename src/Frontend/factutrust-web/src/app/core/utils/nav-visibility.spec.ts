import { canSeeNavEntry } from './nav-visibility';

import { AuthService } from '../services/auth.service';

import { AppModule } from '../models/app-module';



function mockAuth(overrides: Partial<{

  platformSettings: boolean;

  modules: AppModule[];

  permissions: string[];

}>): AuthService {

  return {

    canAccessPlatformSettings: () => overrides.platformSettings ?? true,

    hasAllModules: (mods: AppModule[]) =>

      !overrides.modules || overrides.modules.every(m => mods.includes(m)),

    hasAllPermissions: (perms: string[]) =>

      !overrides.permissions || overrides.permissions.every(p => perms.includes(p))

  } as unknown as AuthService;

}



describe('canSeeNavEntry', () => {

  it('allows entry without restrictions', () => {

    expect(canSeeNavEntry(mockAuth({}), {})).toBe(true);

  });



  it('blocks platformSettingsOnly when user cannot access platform settings', () => {

    expect(

      canSeeNavEntry(mockAuth({ platformSettings: false }), { platformSettingsOnly: true })

    ).toBe(false);

  });



  it('blocks when required module is missing', () => {

    expect(

      canSeeNavEntry(mockAuth({ modules: [AppModule.AI] }), { modules: [AppModule.Forecasting] })

    ).toBe(false);

  });



  it('blocks when required permission is missing', () => {

    expect(

      canSeeNavEntry(mockAuth({ permissions: ['clients:read'] }), {

        permissionsAll: ['invoices:read']

      })

    ).toBe(false);

  });



  it('allows when all modules and permissions match', () => {

    expect(

      canSeeNavEntry(

        mockAuth({ modules: [AppModule.AI], permissions: ['ai:chat'] }),

        { modules: [AppModule.AI], permissionsAll: ['ai:chat'] }

      )

    ).toBe(true);

  });

});

