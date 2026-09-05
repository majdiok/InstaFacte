#!/usr/bin/env node
/**
 * Garde de parité du catalogue sectoriel (plan v1 §2.1, lot 0.2).
 *
 * `sector-catalog-parity.spec.ts` épingle le catalogue statique du frontend contre une COPIE
 * du snapshot backend (`sector-catalog.snapshot.fixture.ts`). Cette copie ne peut pas être
 * importée en direct depuis une spec Karma : le `rootDir` du projet Angular est limité à
 * `src/Frontend/factutrust-web/src`, hors duquel `src/Backend/...` est inatteignable.
 *
 * Ce script ferme l'autre moitié du contrat, explicitement laissée ouverte dans l'en-tête du
 * fixture (« Until that CI step exists, keeping the fixture in sync ... is a manual step ») :
 * il compare la copie frontend au fichier backend qui fait foi, et échoue si les deux ont
 * divergé. Sans lui, une modification du catalogue backend peut atteindre la production avec
 * un repli statique frontend périmé — c'est-à-dire une recommandation de modules différente
 * selon que `/public/sector-catalog` répond ou non.
 *
 * Aucune dépendance nouvelle : `typescript` est déjà installé dans le workspace Angular et
 * sert uniquement à effacer l'annotation de type du fixture (son seul `import` est un
 * `import type`, donc effacé — le module transpilé n'a aucune dépendance à l'exécution).
 *
 * Usage : node scripts/ci/check-sector-catalog-parity.mjs
 * Sortie : 0 si les données sont identiques, 1 sinon (avec le chemin de la première divergence).
 */

import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..', '..');

const BACKEND_SNAPSHOT = resolve(repoRoot, 'src/Backend/FactuTrust.API/Resources/sector-catalog.snapshot.json');
const FRONTEND_FIXTURE = resolve(
  repoRoot,
  'src/Frontend/factutrust-web/src/app/features/auth/register-wizard/sector-catalog.snapshot.fixture.ts'
);
const FRONTEND_PACKAGE = resolve(repoRoot, 'src/Frontend/factutrust-web/package.json');

const EXPORT_NAME = 'SECTOR_CATALOG_SNAPSHOT_FIXTURE';

/** Charge le littéral de données du fixture en effaçant ses types (le seul import est `import type`). */
function loadFixture() {
  const requireFromWeb = createRequire(pathToFileURL(FRONTEND_PACKAGE));
  const ts = requireFromWeb('typescript');

  const source = readFileSync(FRONTEND_FIXTURE, 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2020,
      isolatedModules: true
    },
    fileName: FRONTEND_FIXTURE
  });

  const module = { exports: {} };
  // eslint-disable-next-line no-new-func -- fichier versionné du dépôt, jamais une entrée externe.
  new Function('exports', 'module', 'require', outputText)(module.exports, module, () => {
    throw new Error(
      `${FRONTEND_FIXTURE} a acquis un import à l'exécution ; ce fixture doit rester un littéral de données pur.`
    );
  });

  const fixture = module.exports[EXPORT_NAME];
  if (!fixture) {
    throw new Error(`Export « ${EXPORT_NAME} » introuvable dans ${FRONTEND_FIXTURE}.`);
  }
  return fixture;
}

/** Première divergence entre deux valeurs, en notation pointée, ou null si elles sont identiques. */
function firstDifference(expected, actual, path = '$') {
  if (Object.is(expected, actual)) return null;

  if (Array.isArray(expected) || Array.isArray(actual)) {
    if (!Array.isArray(expected) || !Array.isArray(actual)) {
      return { path, expected, actual };
    }
    if (expected.length !== actual.length) {
      return { path: `${path}.length`, expected: expected.length, actual: actual.length };
    }
    for (let i = 0; i < expected.length; i++) {
      const diff = firstDifference(expected[i], actual[i], `${path}[${i}]`);
      if (diff) return diff;
    }
    return null;
  }

  const bothObjects =
    expected !== null && actual !== null && typeof expected === 'object' && typeof actual === 'object';
  if (bothObjects) {
    const keys = [...new Set([...Object.keys(expected), ...Object.keys(actual)])].sort();
    for (const key of keys) {
      if (!(key in expected)) return { path: `${path}.${key}`, expected: '(absent)', actual: actual[key] };
      if (!(key in actual)) return { path: `${path}.${key}`, expected: expected[key], actual: '(absent)' };
      const diff = firstDifference(expected[key], actual[key], `${path}.${key}`);
      if (diff) return diff;
    }
    return null;
  }

  return { path, expected, actual };
}

function main() {
  const backend = JSON.parse(readFileSync(BACKEND_SNAPSHOT, 'utf8'));
  const fixture = loadFixture();

  const diff = firstDifference(backend, fixture);
  if (!diff) {
    const counts = Object.entries(backend)
      .map(([key, value]) => `${key}=${Array.isArray(value) ? value.length : String(value)}`)
      .join(' ');
    console.log(`Parité du catalogue sectoriel OK (backend = fixture frontend) — ${counts}`);
    return;
  }

  console.error('ÉCHEC — le fixture frontend a divergé du snapshot backend.\n');
  console.error(`  Chemin   : ${diff.path}`);
  console.error(`  Backend  : ${JSON.stringify(diff.expected)}`);
  console.error(`  Frontend : ${JSON.stringify(diff.actual)}\n`);
  console.error('Le snapshot backend fait foi. Régénérez le fixture depuis :');
  console.error(`  ${BACKEND_SNAPSHOT}`);
  console.error('puis mettez `registration-catalog.ts` en accord si le changement porte sur le catalogue statique.');
  process.exitCode = 1;
}

main();
