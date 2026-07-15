/**
 * Contrat JSON des enums immobilisations cote API InstaFact.
 * L'API serialise les enums en chaines PascalCase via JsonStringEnumConverter
 * (ex. status: "Draft", depreciationMethod: "Linear").
 */

export enum FixedAssetStatus {
  Draft = 'Draft',
  InService = 'InService',
  FullyDepreciated = 'FullyDepreciated',
  Disposed = 'Disposed'
}

export enum DepreciationMethod {
  Linear = 'Linear',
  Accelerated = 'Accelerated',
  Integral = 'Integral'
}

const STATUS_BY_STRING: Record<string, FixedAssetStatus> = {
  draft: FixedAssetStatus.Draft,
  inservice: FixedAssetStatus.InService,
  fullydepreciated: FixedAssetStatus.FullyDepreciated,
  disposed: FixedAssetStatus.Disposed
};

const STATUS_BY_INT: Record<number, FixedAssetStatus> = {
  0: FixedAssetStatus.Draft,
  1: FixedAssetStatus.InService,
  2: FixedAssetStatus.FullyDepreciated,
  3: FixedAssetStatus.Disposed
};

const METHOD_BY_STRING: Record<string, DepreciationMethod> = {
  linear: DepreciationMethod.Linear,
  accelerated: DepreciationMethod.Accelerated,
  integral: DepreciationMethod.Integral
};

const METHOD_BY_INT: Record<number, DepreciationMethod> = {
  0: DepreciationMethod.Linear,
  1: DepreciationMethod.Accelerated,
  2: DepreciationMethod.Integral
};

export type ParseFixedAssetStatusResult =
  | { ok: true; value: FixedAssetStatus }
  | { ok: false };

export function parseFixedAssetStatus(raw: unknown): FixedAssetStatus | null {
  const result = parseFixedAssetStatusResult(raw);
  return result.ok ? result.value : null;
}

export function parseFixedAssetStatusResult(raw: unknown): ParseFixedAssetStatusResult {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw in STATUS_BY_INT) {
    return { ok: true, value: STATUS_BY_INT[raw] };
  }
  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    if (!trimmed) return { ok: false };
    const direct = STATUS_BY_STRING[trimmed.toLowerCase()];
    if (direct) return { ok: true, value: direct };
    const fromPascal = trimmed.charAt(0).toLowerCase() + trimmed.slice(1);
    const v = STATUS_BY_STRING[fromPascal.toLowerCase()];
    return v ? { ok: true, value: v } : { ok: false };
  }
  return { ok: false };
}

export function parseDepreciationMethod(raw: unknown): DepreciationMethod {
  if (typeof raw === 'number' && Number.isInteger(raw) && raw in METHOD_BY_INT) {
    return METHOD_BY_INT[raw];
  }
  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    const direct = METHOD_BY_STRING[trimmed.toLowerCase()];
    if (direct) return direct;
    const fromPascal = trimmed.charAt(0).toLowerCase() + trimmed.slice(1);
    const v = METHOD_BY_STRING[fromPascal.toLowerCase()];
    if (v) return v;
  }
  return DepreciationMethod.Linear;
}

export function isDraftStatus(status: FixedAssetStatus): boolean {
  return status === FixedAssetStatus.Draft;
}

export function isInServiceOrBeyond(status: FixedAssetStatus): boolean {
  return status !== FixedAssetStatus.Draft;
}

export function isActiveAssetStatus(status: FixedAssetStatus): boolean {
  return status === FixedAssetStatus.InService || status === FixedAssetStatus.FullyDepreciated;
}

export function canDisposeStatus(status: FixedAssetStatus): boolean {
  return isInServiceOrBeyond(status) && status !== FixedAssetStatus.Disposed;
}