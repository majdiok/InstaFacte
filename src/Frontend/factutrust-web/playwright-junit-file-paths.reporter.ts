import type { FullConfig, FullResult, Reporter, Suite, TestCase } from '@playwright/test/reporter';
import * as fs from 'fs';
import * as path from 'path';

type Options = { outputFile?: string };

/**
 * Playwright's built-in junit reporter omits testcase @file. Trunk Flaky Tests
 * warns on that; this reporter rewrites the same XML with repo-relative paths.
 */
class JunitAddFilePathsReporter implements Reporter {
  private readonly outputFile: string;
  private suite: Suite | undefined;
  private result: FullResult | undefined;
  private startedAt = new Date();
  private configDir = process.cwd();

  constructor(options: Options = {}) {
    this.outputFile = options.outputFile ?? 'e2e/test-results/junit.xml';
  }

  onBegin(config: FullConfig, suite: Suite): void {
    this.suite = suite;
    this.startedAt = new Date();
    this.configDir = config.configFile ? path.dirname(config.configFile) : config.rootDir;
  }

  async onEnd(result: FullResult): Promise<void> {
    this.result = result;
  }

  async onExit(): Promise<void> {
    if (!this.suite || !this.result) {
      return;
    }

    const repoRoot = findRepoRoot(this.configDir);
    const xml = buildJunitXml(this.suite, this.result, this.startedAt, repoRoot);
    const xmlPath = path.resolve(this.configDir, this.outputFile);
    fs.mkdirSync(path.dirname(xmlPath), { recursive: true });
    fs.writeFileSync(xmlPath, xml, 'utf8');
  }
}

function findRepoRoot(start: string): string {
  let dir = start;
  while (true) {
    if (fs.existsSync(path.join(dir, '.git')) || fs.existsSync(path.join(dir, 'azure-pipelines.yml'))) {
      return dir;
    }
    const parent = path.dirname(dir);
    if (parent === dir) {
      return start;
    }
    dir = parent;
  }
}

function escapeXml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function testCaseName(test: TestCase): string {
  const parts = test.titlePath();
  // Drop project + file; keep describe/test titles (Playwright junit uses slice(3)).
  const sliced = parts.length > 3 ? parts.slice(3) : parts.slice(2);
  return sliced.join(' › ') || test.title;
}

function buildJunitXml(root: Suite, result: FullResult, startedAt: Date, repoRoot: string): string {
  const suites: string[] = [];
  let totalTests = 0;
  let totalFailures = 0;
  let totalSkipped = 0;

  for (const projectSuite of root.suites) {
    for (const fileSuite of projectSuite.suites) {
      const tests = fileSuite.allTests();
      let failures = 0;
      let skipped = 0;
      let durationSec = 0;
      const cases: string[] = [];

      for (const test of tests) {
        const outcome = test.outcome();
        if (outcome === 'skipped') {
          skipped += 1;
        }
        if (!test.ok()) {
          failures += 1;
        }
        const time = test.results.reduce((sum, item) => sum + item.duration, 0) / 1000;
        durationSec += time;
        const file = path.relative(repoRoot, test.location.file).replace(/\\/g, '/');
        const name = testCaseName(test);
        let body = '';
        if (outcome === 'skipped') {
          body = '<skipped></skipped>';
        } else if (!test.ok()) {
          const error = test.results[test.results.length - 1]?.error;
          const message = escapeXml(error?.message ?? `${path.basename(test.location.file)} ${test.title}`);
          body = `<failure message="${message}" type="FAILURE"></failure>`;
        }
        cases.push(
          `<testcase name="${escapeXml(name)}" classname="${escapeXml(fileSuite.title)}" file="${escapeXml(file)}" time="${time}">${body}</testcase>`
        );
      }

      totalTests += tests.length;
      totalFailures += failures;
      totalSkipped += skipped;
      suites.push(
        `<testsuite name="${escapeXml(fileSuite.title)}" timestamp="${startedAt.toISOString()}" hostname="${escapeXml(projectSuite.title)}" tests="${tests.length}" failures="${failures}" skipped="${skipped}" time="${durationSec}" errors="0">\n${cases.join('\n')}\n</testsuite>`
      );
    }
  }

  return `<testsuites id="" name="" tests="${totalTests}" failures="${totalFailures}" skipped="${totalSkipped}" errors="0" time="${result.duration / 1000}">\n${suites.join('\n')}\n</testsuites>\n`;
}

export default JunitAddFilePathsReporter;
