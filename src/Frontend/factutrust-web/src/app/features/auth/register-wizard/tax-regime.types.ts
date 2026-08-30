/**
 * Shared `<p-select>` option shape for the tax regime dropdown (Régime réel /
 * forfaitaire / Exonéré). Single source of truth for the wizard container and
 * the step components that render/consume it, instead of each declaring its
 * own copy of the same `{ label, value }` interface.
 */
export interface TaxRegime {
  label: string;
  value: number;
}
