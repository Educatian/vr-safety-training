'use strict';
// Metadata/schema checks only. Does not verify source truth, mesh geometry or legal compliance.
function validateManifest(manifest) {
  const errors = [];
  const fail = (path, reason) => errors.push(`${path}: ${reason}`);
  if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) return ['manifest: expected object'];
  if (manifest.schemaVersion !== 1) fail('schemaVersion', 'expected 1');
  if (!/^[a-f0-9]{40}$/.test(manifest.baselineCommit || '')) fail('baselineCommit', 'expected full Git SHA');
  if (!/^6000\.3\.25f1$/.test(manifest.unityVersion || '')) fail('unityVersion', 'baseline must be 6000.3.25f1');
  if (!Array.isArray(manifest.props) || manifest.props.length === 0) return [...errors, 'props: must be non-empty array'];
  if (manifest.pipeline !== 'URP 17.3.0') fail('pipeline', 'expected URP 17.3.0');
  if (manifest.props.length !== 4) fail('props', 'recipe requires exactly four variants');
  const ids = new Set(), paths = new Set(), variants = new Set();
  manifest.props.forEach((prop, i) => {
    const prefix = `props[${i}]`;
    if (!prop || typeof prop !== 'object' || Array.isArray(prop)) { fail(prefix, 'expected object'); return; }
    if (typeof prop.id !== 'string' || !/^[a-z0-9][a-z0-9_-]+$/.test(prop.id)) fail(prefix + '.id', 'invalid id');
    else if (ids.has(prop.id)) fail(prefix + '.id', 'duplicate');
    else ids.add(prop.id);
    if (typeof prop.assetPath !== 'string' ||
        !/^Assets\/_Game\/Art\/Models\/B-PROC\/[A-Za-z0-9_-]+\.fbx$/.test(prop.assetPath))
      fail(prefix + '.assetPath', 'must be a flat FBX path in _Game/Art/Models/B-PROC');
    else if (paths.has(prop.assetPath.toLowerCase())) fail(prefix + '.assetPath', 'duplicate');
    else paths.add(prop.assetPath.toLowerCase());
    for (const axis of ['x', 'y', 'z']) {
      const value = prop.expectedSizeMeters?.[axis];
      if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0)
        fail(prefix + '.expectedSizeMeters.' + axis, 'must be a positive finite meter value');
    }
    if (!Number.isFinite(prop.sizeToleranceMeters) || prop.sizeToleranceMeters <= 0 || prop.sizeToleranceMeters > 0.05)
      fail(prefix + '.sizeToleranceMeters', 'must be >0 and <=0.05 m');
    for (const field of ['maxTriangles', 'maxMaterials'])
      if (!Number.isSafeInteger(prop[field]) || prop[field] < 1) fail(prefix + '.' + field, 'must be positive integer');
    if (!['clean', 'service_worn', 'missing_midrail', 'missing_midrail_worn'].includes(prop.variant)) fail(prefix + '.variant', 'unknown candidate variant');
    if (variants.has(prop.variant)) fail(prefix + '.variant', 'duplicate variant');
    variants.add(prop.variant);
    if (prop.assetPath !== 'Assets/_Game/Art/Models/B-PROC/SM_Guardrail_' + prop.variant + '.fbx') fail(prefix + '.assetPath', 'filename must match the variant and SM_ convention');
    if (prop.id !== 'guardrail-' + prop.variant) fail(prefix + '.id', 'id must match the variant');
    if (prop.maxTriangles > 40000) fail(prefix + '.maxTriangles', 'hero budget ceiling is 40000');
    if (prop.maxMaterials > 4) fail(prefix + '.maxMaterials', 'recipe material ceiling is four');
    for (const [axis, target] of Object.entries({x: 2, y: 1.0668, z: 0.18}))
      if (Math.abs(prop.expectedSizeMeters?.[axis] - target) > 1e-6) fail(prefix + '.expectedSizeMeters.' + axis, 'does not match authored recipe');
    for (const field of ['strengthVerified', 'anchorageVerified', 'scenarioBound'])
      if (prop[field] !== false) fail(prefix + '.' + field, 'this candidate increment must not claim verification or scene integration');
    if (!Array.isArray(prop.evidence) || prop.evidence.length === 0) fail(prefix + '.evidence', 'source metadata required');
    else prop.evidence.forEach((source, j) => {
      if (!source || typeof source.url !== 'string' || !/^https:\/\/www\.osha\.gov\//.test(source.url) ||
          typeof source.section !== 'string' || !source.section.trim() ||
          typeof source.scope !== 'string' || !source.scope.trim())
        fail(prefix + `.evidence[${j}]`, 'official source URL, section and bounded claim required');
    });
    if (!Array.isArray(prop.authoredAssumptions) || prop.authoredAssumptions.length === 0 ||
        prop.authoredAssumptions.some(value => typeof value !== 'string' || !value.trim()))
      fail(prefix + '.authoredAssumptions', 'explicit non-regulatory geometry assumptions required');
    const g = prop.geometry;
    if (!g || typeof g !== 'object') fail(prefix + '.geometry', 'guardrail geometry targets required');
    else {
      if (!Number.isFinite(g.topEdgeMeters) || Math.abs(g.topEdgeMeters - 1.0668) > 1e-6) fail(prefix + '.geometry.topEdgeMeters', 'recipe uses 42-inch nominal top edge');
      if (!Number.isFinite(g.midRailCenterMeters) || Math.abs(g.midRailCenterMeters - g.topEdgeMeters / 2) > 1e-6) fail(prefix + '.geometry.midRailCenterMeters', 'must be midway for this recipe');
      if (!Number.isFinite(g.toeBoardTopMeters) || Math.abs(g.toeBoardTopMeters - 0.0889) > 1e-6) fail(prefix + '.geometry.toeBoardTopMeters', 'invalid toeboard top target');
      if (!Number.isFinite(g.toeBoardGapMeters) || Math.abs(g.toeBoardGapMeters - 0.005) > 1e-6) fail(prefix + '.geometry.toeBoardGapMeters', 'recipe uses exactly 0.005 m clearance');
    }
    if (!Array.isArray(prop.generatedWith)) fail(prefix + '.generatedWith', 'must record actual completed tool generations as array');
  });
  return errors;
}
module.exports = { validateManifest };
if (typeof require === 'function' && require.main === module) {
  try {
    const fs = require('node:fs');
    const file = process.argv[2];
    if (!file) throw new Error('Usage: node validate_manifest.cjs docs/GuardrailSpecs.json');
    const errors = validateManifest(JSON.parse(fs.readFileSync(file, 'utf8')));
    console.log(JSON.stringify({ metadataChecksPassed: errors.length === 0, errors }, null, 2));
    process.exitCode = errors.length ? 1 : 0;
  } catch (error) { console.error(error.message); process.exitCode = 2; }
}
