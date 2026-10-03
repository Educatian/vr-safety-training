'use strict';
function runTests(validateManifest, seed) {
  const results = [];
  function check(name, mutation, shouldPass = false) {
    const value = JSON.parse(JSON.stringify(seed));
    const candidate = mutation(value) ?? value;
    const errors = validateManifest(candidate);
    const passed = (errors.length === 0) === shouldPass;
    results.push({ name, passed, errorCount: errors.length });
  }
  check('valid four-variant manifest', () => {}, true);
  check('wrong schema', m => { m.schemaVersion = 2; });
  check('short commit', m => { m.baselineCommit = 'e40fd32'; });
  check('wrong Unity baseline', m => { m.unityVersion = '6000.4.9f1'; });
  check('empty prop list', m => { m.props = []; });
  check('missing prop list', m => { delete m.props; });
  check('null prop', m => { m.props[0] = null; });
  check('duplicate id', m => { m.props[1].id = m.props[0].id; });
  check('duplicate asset path case insensitive', m => { m.props[1].assetPath = m.props[0].assetPath.replace('/SM_', '/sm_'); });
  check('path traversal', m => { m.props[0].assetPath = 'Assets/_Game/Art/Models/B-PROC/../other.fbx'; });
  check('outside managed folder', m => { m.props[0].assetPath = 'Assets/ThirdParty/existing.fbx'; });
  check('wrong format', m => { m.props[0].assetPath = 'Assets/_Game/Art/Models/B-PROC/test.blend'; });
  check('zero dimension', m => { m.props[0].expectedSizeMeters.y = 0; });
  check('negative dimension', m => { m.props[0].expectedSizeMeters.y = -1; });
  check('NaN dimension', m => { m.props[0].expectedSizeMeters.y = NaN; });
  check('infinite dimension', m => { m.props[0].expectedSizeMeters.y = Infinity; });
  check('string dimension', m => { m.props[0].expectedSizeMeters.y = '1.0668'; });
  check('missing dimensions', m => { delete m.props[0].expectedSizeMeters; });
  check('zero tolerance', m => { m.props[0].sizeToleranceMeters = 0; });
  check('excess tolerance', m => { m.props[0].sizeToleranceMeters = 1; });
  check('invalid triangle budget', m => { m.props[0].maxTriangles = 1.5; });
  check('missing material budget', m => { delete m.props[0].maxMaterials; });
  check('unknown condition variant', m => { m.props[0].variant = 'certified'; });
  check('unsupported strength claim', m => { m.props[0].strengthVerified = true; });
  check('unsupported anchorage claim', m => { m.props[0].anchorageVerified = true; });
  check('unsupported scene-integration claim', m => { m.props[0].scenarioBound = true; });
  check('source missing', m => { m.props[0].evidence = []; });
  check('lookalike source host', m => { m.props[0].evidence[0].url = 'https://www.osha.gov.example.org/fake'; });
  check('source section missing', m => { m.props[0].evidence[0].section = ''; });
  check('claim scope missing', m => { m.props[0].evidence[0].scope = ''; });
  check('authored assumptions missing', m => { m.props[0].authoredAssumptions = []; });
  check('generation provenance absent', m => { delete m.props[0].generatedWith; });
  check('missing guardrail geometry', m => { delete m.props[0].geometry; });
  check('wrong nominal top edge', m => { m.props[0].geometry.topEdgeMeters = 1; });
  check('wrong midpoint', m => { m.props[0].geometry.midRailCenterMeters = 0.6; });
  check('insufficient toeboard target', m => { m.props[0].geometry.toeBoardTopMeters = 0.08; });
  check('excess ground gap', m => { m.props[0].geometry.toeBoardGapMeters = 0.01; });
  check('negative ground gap', m => { m.props[0].geometry.toeBoardGapMeters = -0.01; });
  check('wrong renderer', m => { m.pipeline = 'Built-in'; });
  check('missing variant', m => { m.props.pop(); });
  check('duplicate variant', m => { m.props[1].variant = m.props[0].variant; });
  check('filename variant mismatch', m => { m.props[0].assetPath = m.props[0].assetPath.replace('SM_Guardrail_', 'Wrong_'); });
  check('excess hero budget', m => { m.props[0].maxTriangles = 40001; });
  check('excess material budget', m => { m.props[0].maxMaterials = 5; });
  check('wrong positive width', m => { m.props[0].expectedSizeMeters.x = 3; });
  check('wrong recipe toe height', m => { m.props[0].geometry.toeBoardTopMeters = 0.09; });
  check('wrong recipe allowed gap', m => { m.props[0].geometry.toeBoardGapMeters = 0.006; });
  const invalidRoots = [null, [], 'manifest'];
  invalidRoots.forEach((root, i) => results.push({ name: 'invalid root ' + i,
    passed: validateManifest(root).length > 0 }));
  return { suite: 'JavaScript manifest metadata only', total: results.length,
    passed: results.filter(r => r.passed).length, failed: results.filter(r => !r.passed).length, results };
}
module.exports = { runTests };
if (typeof require === 'function' && require.main === module) {
  const { validateManifest } = require('./validate_manifest.cjs');
  const seed = require('../../docs/GuardrailSpecs.json');
  const report = runTests(validateManifest, seed);
  console.log(JSON.stringify(report, null, 2));
  process.exitCode = report.failed ? 1 : 0;
}
