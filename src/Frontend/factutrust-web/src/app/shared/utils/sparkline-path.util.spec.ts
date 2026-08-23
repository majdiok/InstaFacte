import { buildSparklinePaths } from './sparkline-path.util';

describe('buildSparklinePaths', () => {
  it('returns null when there are fewer than 2 points', () => {
    expect(buildSparklinePaths([])).toBeNull();
    expect(buildSparklinePaths([1])).toBeNull();
  });

  it('returns null for a missing series', () => {
    expect(buildSparklinePaths(null as unknown as number[])).toBeNull();
  });

  it('draws a mid-height flat line when every value is equal', () => {
    const paths = buildSparklinePaths([0, 0, 0], { width: 120, height: 28 });
    expect(paths).not.toBeNull();
    expect(paths!.line).toContain('M 0.00 14.00');
    expect(paths!.line).toMatch(/L 120\.00 14\.00$/);
    expect(new Set(yCoords(paths!.line))).toEqual(new Set(['14.00']));
  });

  it('maps min to the bottom padding and max to the top padding', () => {
    const paths = buildSparklinePaths([0, 10], { width: 120, height: 28, padding: 2 });
    expect(paths).not.toBeNull();
    expect(paths!.line).toBe('M 0.00 26.00 L 120.00 2.00');
  });

  it('keeps credit-note negatives in the min/max scale', () => {
    const paths = buildSparklinePaths([-10, 10], { width: 100, height: 20, padding: 0 });
    expect(paths).not.toBeNull();
    expect(paths!.line).toBe('M 0.00 20.00 L 100.00 0.00');
  });

  it('treats NaN and undefined as zero', () => {
    const paths = buildSparklinePaths([undefined, Number.NaN, 10], { width: 100, height: 20, padding: 0 });
    expect(paths).not.toBeNull();
    expect(paths!.line.startsWith('M 0.00 20.00')).toBeTrue();
    expect(paths!.line.endsWith('L 100.00 0.00')).toBeTrue();
  });

  it('uses a 120×28 viewBox by default and closes the area path', () => {
    const paths = buildSparklinePaths([1, 3, 2]);
    expect(paths).not.toBeNull();
    expect(paths!.line.startsWith('M 0.00 ')).toBeTrue();
    expect(paths!.line).toContain(' L ');
    expect(paths!.area.startsWith(paths!.line)).toBeTrue();
    expect(paths!.area.endsWith(' L 120.00 28.00 L 0.00 28.00 Z')).toBeTrue();
  });
});

function yCoords(line: string): string[] {
  return [...line.matchAll(/[\d.]+ (\d+\.\d+)/g)].map(match => match[1]);
}
