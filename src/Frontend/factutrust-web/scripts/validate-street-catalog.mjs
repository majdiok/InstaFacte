/**
 * Validates the future immutable virtual-street catalogue against its public
 * contract. This script intentionally does not generate or overwrite assets.
 */
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { resolve } from 'node:path';
import { validateBytes } from 'gltf-validator';

const root = resolve(process.argv[2] ?? 'src/assets/virtual-street/catalogs');
const versionsArg = process.argv[3]?.trim();
const manifestFile = resolve(process.argv[4] ?? `${root}/manifest-v2.json`);
const budgets = {
  initialBytes: { economy: 6 * 1024 ** 2, standard: 10 * 1024 ** 2 },
  interiorBytes: { economy: 4 * 1024 ** 2, standard: 6 * 1024 ** 2 },
  localExteriorBytes: { economy: 6 * 1024 ** 2, standard: 10 * 1024 ** 2 },
  triangles: { economy: 120_000, standard: 300_000 },
  drawCalls: { economy: 100, standard: 180 },
  textureBytes: { economy: 96 * 1024 ** 2, standard: 192 * 1024 ** 2 },
};
const profiles = ['economy', 'standard'];
const fail = m => { throw new Error(m); };
const is = (v, n) => Number.isFinite(v) && v > 0 && v <= n;
const manifest = JSON.parse(readFileSync(manifestFile, 'utf8'));
if (manifest.schemaVersion !== 2) fail('schemaVersion 2 required');
if (!/^[0-9A-Za-z][0-9A-Za-z._-]*$/.test(manifest.catalogVersion ?? '')) fail('invalid immutable catalogVersion');
if (manifest.renderer?.engine !== 'three' || manifest.renderer?.revision !== '161') fail('renderer must be three r161');
const seenProfiles = new Set();
const sceneByKey = new Map();
const assetByKey = new Map();
// Register scenes first so profile references resolve against the full catalog.
for (const s of manifest.scenes ?? []) {
  if (sceneByKey.has(s.sceneKey)) fail(`duplicate scene ${s.sceneKey}`);
  sceneByKey.set(s.sceneKey, s);
  if (!s.bounds || !s.navigation?.spawn || !s.navigation?.exit) fail(`${s.sceneKey}: bounds, spawn and exit required`);
  if (!Array.isArray(s.navigation.walkableBounds) || !Array.isArray(s.navigation.collisionBounds)) fail(`${s.sceneKey}: walkable/collision bounds required`);
  for (const variant of profiles) {
    const v = s.variants?.[variant];
    if (!v) fail(`${s.sceneKey}: missing ${variant} variant`);
    if (!is(v.transferBytes, Number.MAX_SAFE_INTEGER) || !is(v.triangles, budgets.triangles[variant]) || !is(v.drawCalls, budgets.drawCalls[variant]) || !is(v.estimatedTextureBytes, budgets.textureBytes[variant])) fail(`${s.sceneKey}/${variant}: budget exceeded or invalid`);
  }
  if (s.interactions?.some(i => !['exit', 'openStorefront', 'inspectScene'].includes(i.action?.role))) fail(`${s.sceneKey}: unsupported interaction role`);
}
for (const a of manifest.assets ?? []) {
  if (assetByKey.has(a.assetKey)) fail(`duplicate asset ${a.assetKey}`);
  assetByKey.set(a.assetKey, a);
  if (!/^[0-9A-Za-z][0-9A-Za-z._-]*$/.test(a.assetKey ?? '')) fail(`${a.assetKey}: invalid asset key`);
  if (!a.path || a.path.startsWith('/') || /^[a-z][a-z0-9+.-]*:/i.test(a.path) || a.path.includes('..')) fail(`${a.assetKey}: path must stay relative to catalog root`);
  if (!/^[0-9a-f]{64}$/.test(a.sha256 ?? '')) fail(`${a.assetKey}: sha256 required`);
  for (const dep of a.dependencyAssetKeys ?? []) if (!assetByKey.has(dep) && !(manifest.assets ?? []).some(x => x.assetKey === dep)) fail(`${a.assetKey}: unknown dependency ${dep}`);
}
for (const p of manifest.profiles ?? []) {
  const id = `${p.profileKey}\u0000${p.editorialRevision}`;
  if (seenProfiles.has(id)) fail(`duplicate accepted profile ${id}`);
  seenProfiles.add(id);
  for (const [kind, key] of [['facade', p.facadeSceneKey], ['interior', p.interiorSceneKey], ['local exterior', p.localExteriorSceneKey]]) {
    if (!key) continue;
    if (!sceneByKey.has(key)) fail(`${id}: missing ${kind} scene ${key}`);
  }
  if (p.status === 'active' && (!p.facadeSceneKey || !p.interiorSceneKey)) fail(`${id}: active profile requires facade and interior`);
}
const roots = (manifest.assets ?? []).filter(a => ['glb', 'texture', 'environment', 'lightmap', 'decoder'].includes(a.kind));
const files = versionsArg ? versionsArg.split(',').filter(Boolean) : roots.map(a => `${manifest.catalogVersion}/${a.path}`);
for (const relative of files) {
  const full = resolve(root, relative);
  if (!full.startsWith(resolve(root))) fail(`path escapes catalog root: ${relative}`);
  if (!existsSync(full)) fail(`missing asset ${relative}`);
  const bytes = readFileSync(full);
  const a = roots.find(x => x.path === relative.slice(manifest.catalogVersion.length + 1));
  if (a) {
    if (a.encodedBytes !== statSync(full).size) fail(`${a.assetKey}: encoded byte size mismatch`);
    if (a.sha256 !== createHash('sha256').update(bytes).digest('hex')) fail(`${a.assetKey}: sha256 mismatch`);
    if (a.kind === 'glb') {
      const report = await validateBytes(new Uint8Array(bytes));
      if (report.issues.numErrors) fail(`${a.assetKey}: glTF has ${report.issues.numErrors} error(s)`);
      const bad = a.gltf?.requiredExtensions?.find(e => !['KHR_draco_mesh_compression', 'EXT_meshopt_compression', 'KHR_texture_basisu'].includes(e));
      if (bad) fail(`${a.assetKey}: unsupported required extension ${bad}`);
    }
  }
}
console.log(`catalog ${manifest.catalogVersion}: ${manifest.profiles.length} profiles, ${manifest.scenes.length} scenes, ${manifest.assets.length} assets validated`);
