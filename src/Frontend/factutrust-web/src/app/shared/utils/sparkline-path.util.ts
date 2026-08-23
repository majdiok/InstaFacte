export interface SparklinePathOptions {
  width?: number;
  height?: number;
  padding?: number;
}

export interface SparklinePaths {
  line: string;
  area: string;
}

/**
 * Builds SVG path `d` attributes for a sparkline (line + closed area).
 * Returns null when fewer than 2 plottable points are available.
 *
 * ViewBox defaults (120×28) match the existing `.stat-sparkline` CSS.
 * When every value is equal, the line sits at mid-height so a flat series
 * stays visible instead of collapsing onto the baseline.
 */
export function buildSparklinePaths(
  values: ReadonlyArray<number | null | undefined>,
  options: SparklinePathOptions = {}
): SparklinePaths | null {
  if (!values || values.length < 2) {
    return null;
  }

  const width = options.width ?? 120;
  const height = options.height ?? 28;
  const padding = options.padding ?? 2;
  const data = values.map(coerceFiniteNumber);
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min;
  const drawableHeight = Math.max(height - padding * 2, 1);
  const stepX = width / (data.length - 1);
  const midY = height / 2;

  const points = data.map((value, index) => {
    const x = index * stepX;
    const y = range === 0
      ? midY
      : height - padding - ((value - min) / range) * drawableHeight;
    return [x, y] as const;
  });

  let line = `M ${fmt(points[0][0])} ${fmt(points[0][1])}`;
  for (let i = 1; i < points.length; i++) {
    line += ` L ${fmt(points[i][0])} ${fmt(points[i][1])}`;
  }

  const area = `${line} L ${fmt(width)} ${fmt(height)} L 0.00 ${fmt(height)} Z`;
  return { line, area };
}

function coerceFiniteNumber(value: number | null | undefined): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function fmt(value: number): string {
  return value.toFixed(2);
}
