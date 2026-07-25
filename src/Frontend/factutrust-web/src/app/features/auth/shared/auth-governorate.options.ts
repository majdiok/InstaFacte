import { TUNISIAN_GOVERNORATES } from '@shared/validation/validation-rules';

export interface GovernorateOption {
  label: string;
  value: string;
}

/** Dropdown options aligned with backend TunisianGovernorates / validation-rules. */
export const GOVERNORATE_OPTIONS: GovernorateOption[] = TUNISIAN_GOVERNORATES.map(g => ({
  label: g,
  value: g
}));
