// Integration test against Wrangler's localhost-only emulated R2/DO bindings.
// The hostname rewrite is confined to this test; no production requests occur.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const originalFetch = globalThis.fetch;
const base = 'http://127.0.0.1:8788';
process.env.COMPILER_SERVICE_URL = 'https://compiler.test';
process.env.DEMO_PUBLISH_TOKEN = 'local-test-demo';
globalThis.fetch = (input,options) => {
  const url = new URL(input);
  assert.equal(url.origin,'https://compiler.test');
  return originalFetch(base + url.pathname,options);
};
try { await import('../demo/publish-demo.mjs'); }
finally { globalThis.fetch = originalFetch; }
const response = await fetch(base+'/api/v1/demo');
assert.equal(response.status,200);
const demo = await response.json();
assert.equal(demo.results.length,3);
for(const result of demo.results){
  const download = await fetch(base+result.downloadUrl);
  assert.equal(download.status,200);
  const expected = await readFile(new URL(`../demo/output/demonstration-${result.device}.gwc`,import.meta.url));
  assert.deepEqual(new Uint8Array(await download.arrayBuffer()),new Uint8Array(expected));
}
assert.equal((await (await fetch(base+'/api/v1/health')).json()).compilerReady,false);
assert.equal((await fetch(base+'/api/v1/compile',{method:'POST',body:'unused'})).status,503);
console.log('PASS: approved demo publisher -> real local Worker/R2 -> three genuine GWC downloads, with live uploads disabled.');
