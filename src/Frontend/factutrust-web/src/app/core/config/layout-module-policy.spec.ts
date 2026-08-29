import { AppModule } from '@core/models/app-module';
import {
  MODULES_REQUIRED_BY_FIRST_SEGMENT,
  PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT
} from './layout-module-policy';

describe('layout-module-policy — return-notes', () => {
  it('requires the Sales module and return_notes:read', () => {
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['return-notes']).toEqual([AppModule.Sales]);
    expect(PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT['return-notes']).toEqual(['return_notes:read']);
  });
});

describe('layout-module-policy — projects', () => {
  it('requires the Projects module and projects:read', () => {
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['projects']).toEqual([AppModule.Projects]);
    expect(PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT['projects']).toEqual(['projects:read']);
  });
});

describe('layout-module-policy — ai-assistant', () => {
  it('requires the AI module and ai:chat (route accessible ⇔ menu visible ⇔ API autorisée)', () => {
    // Aligné avec l'entrée du menu (`app-navigation.registry.ts`, permissionsAll: ['ai:chat'])
    // et le garde de la coquille IA (`canUseAiAssistant`, main-layout.component.ts).
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['ai-assistant']).toEqual([AppModule.AI]);
    expect(PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT['ai-assistant']).toEqual(['ai:chat']);
  });
});

describe('layout-module-policy — stock vouchers (sous /stock)', () => {
  it('keeps BE/BS under the Stock module and stock:read (no dedicated first segment)', () => {
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['stock']).toEqual([AppModule.Stock]);
    expect(PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT['stock']).toEqual(['stock:read']);
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['entries']).toBeUndefined();
    expect(MODULES_REQUIRED_BY_FIRST_SEGMENT['issues']).toBeUndefined();
  });
});
