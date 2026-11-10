// Start Wrangler locally as described in README.md, and run native-smoke.ps1 first.
// Tests only localhost and self-authored fixtures; never sends data to production.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const base = 'http://127.0.0.1:8788';
async function request(path, options = {}) {
  const response = await fetch(base + path, options);
  if (!response.ok) throw Error(`${response.status}: ${await response.text()}`);
  return response;
}
const runner = {Authorization:'Bearer local-test-runner'};
await request('/internal/runner/claim', {method:'POST',headers:runner});
const health = await (await request('/api/v1/health')).json();
assert.equal(health.compilerReady,true);
const source = await readFile(new URL('./output/valid.gwz',import.meta.url));
const originalGwc = await readFile(new URL('./output/Garmin.gwc',import.meta.url));
const form = new FormData();
form.set('cartridge',new File([source],'trusted-smoke.gwz'));
form.set('compile','Garmin'); form.set('download','Garmin');
const {jobId} = await (await request('/api/v1/compile',{method:'POST',body:form})).json();
const job = await (await request('/internal/runner/claim',{method:'POST',headers:runner})).json();
assert.equal(job.jobId,jobId);
const headers = {...runner,'X-Job-Lease':job.lease};
const storedSource = new Uint8Array(await (await request(`/internal/runner/${jobId}/source`,{headers})).arrayBuffer());
assert.deepEqual(storedSource,new Uint8Array(source));
await request(`/internal/runner/${jobId}/build/Garmin`,{method:'PUT',headers,body:originalGwc});
await request(`/internal/runner/${jobId}/complete`,{method:'POST',headers,body:JSON.stringify({results:[{device:'Garmin',ok:true,messages:[]}]})});
const status = await (await request(`/api/v1/compile/${jobId}`)).json();
assert.equal(status.status,'done');
const bytes = new Uint8Array(await (await request(status.results[0].downloadUrl)).arrayBuffer());
assert.deepEqual(bytes,new Uint8Array(originalGwc));
const storage = await (await request('/internal/storage',{headers:{Authorization:'Bearer local-test-website'}})).json();
assert.ok(storage.reservedBytes >= source.length + 20_000_000 && storage.reservedBytes <= storage.capBytes);
console.log('PASS: real local Worker/Durable Object/R2 gateway accepts GWZ, assigns job, preserves source, serves genuine GWC and accounts for reserved space.');
