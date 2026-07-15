export type ScreenAnalysisHighlightType = 'anomaly' | 'top' | 'warning';

export type ScreenAnalysisSamplingStrategy =
  | 'all'
  | 'top-by-amount'
  | 'recent'
  | 'anomaly-first';

export interface ScreenAnalysisHighlight {
  type: ScreenAnalysisHighlightType;
  label: string;
  value: unknown;
  context?: string;
}

export interface ScreenAnalysisSampling {
  strategy: ScreenAnalysisSamplingStrategy;
  totalAvailable: number;
  included: number;
  truncated: boolean;
}

export interface ScreenAnalysisDataQuality {
  hasData: boolean;
  isPartial: boolean;
  warnings: string[];
}

export interface ScreenAnalysisPayloadV2 {
  schemaVersion: '2';
  screenId: string;
  capturedAt: string;
  filters: Record<string, unknown>;
  summary: Record<string, number | string | null>;
  highlights: ScreenAnalysisHighlight[];
  rows?: unknown[];
  sampling: ScreenAnalysisSampling;
  dataQuality: ScreenAnalysisDataQuality;
  /** Screen-specific extension fields */
  [key: string]: unknown;
}

export interface ScreenAnalysisEnvelope {
  schemaVersion: '1' | '2';
  screenId: string;
  payload: unknown;
}

export const REQUIRED_V2_FIELDS: (keyof ScreenAnalysisPayloadV2)[] = [
  'schemaVersion',
  'screenId',
  'capturedAt',
  'filters',
  'summary',
  'highlights',
  'sampling',
  'dataQuality'
];
