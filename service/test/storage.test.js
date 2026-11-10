import test from 'node:test';
import assert from 'node:assert/strict';
import worker, {CompilerCoordinator} from '../src/worker.js';
import {CAP_BYTES, MAX_FILE_BYTES, RETENTION_MS, boundedBytes} from '../src/policy.js';

class Storage {
  values = new Map();
  async get(key) { return structuredClone(this.values.get(key)); }
  async put(key, value) { this.values.set(key, structuredClone(value)); }
  async delete(key) { return this.values.delete(key); }
  async list({prefix}) { return new Map([...this.values].filter(([key]) => key.startsWith(prefix)).map(([k,v]) => [k, structuredClone(v)])); }
  async transaction(fn) { return fn(this); }
  async setAlarm(time) { this.alarm = time; }
  async getAlarm() { return this.alarm; }
  async deleteAlarm() { this.alarm = null; }
}
class Bucket {
  objects = new Map();
  async put(key, bytes) { this.objects.set(key, new Uint8Array(bytes)); if (this.failWrite) throw Error('uncertain write'); }
  async get(key) { const value = this.objects.get(key); return value && {body: value}; }
  async delete(keys) { if (this.failDelete) throw Error('delete failed'); for (const key of [keys].flat()) this.objects.delete(key); }
}
function setup() {
  const storage = new Storage();
  const env = {SOURCES: new Bucket(), TEMPORARY: new Bucket(), RUNNER_TOKEN: 'runner-test', WEBSITE_TOKEN: 'website-test', DEMO_PUBLISH_TOKEN: 'demo-test', SERVICE_ENABLED: 'true'};
  const coordinator = new CompilerCoordinator({storage}, env);
  return {storage, env, coordinator};
}
const zip = new Uint8Array([80, 75, 3, 4]);
const gwc = new Uint8Array([2, 10, 67, 65, 82, 84, 0, 0, 0, 0]);
const req = (path, options = {}) => new Request('https://compiler.test' + path, options);
async function create(coordinator, devices = ['Garmin'], downloads = ['Garmin']) {
  await coordinator.ctx.storage.put('runnerHeartbeat', Date.now());
  const form = new FormData();
  form.set('cartridge', new File([zip], 'test.gwz'));
  for (const d of devices) form.append('compile', d);
  for (const d of downloads) form.append('download', d);
  return coordinator.fetch(req('/api/v1/compile', {method: 'POST', body: form}));
}
const runnerHeaders = lease => ({Authorization: 'Bearer runner-test', 'X-Job-Lease': lease});

test('reserves output space before concurrent jobs can exceed the 9 GB cap', async () => {
  const {storage, env, coordinator} = setup();
  await storage.put('reservedBytes', CAP_BYTES - MAX_FILE_BYTES - zip.length);
  const responses = await Promise.all([create(coordinator), create(coordinator)]);
  assert.deepEqual(responses.map(r => r.status).sort(), [202, 507]);
  assert.equal(await storage.get('reservedBytes'), CAP_BYTES);
  assert.equal(env.TEMPORARY.objects.size, 1);
});

test('queue -> real runner result -> private download, no source access from public routes', async () => {
  const {coordinator} = setup();
  const {jobId} = await (await create(coordinator)).json();
  const claim = await coordinator.fetch(req('/internal/runner/claim', {method: 'POST', headers: runnerHeaders()}));
  const {lease} = await claim.json();
  assert.equal((await coordinator.fetch(req(`/internal/runner/${jobId}/source`))).status, 401);
  const result = await coordinator.fetch(req(`/internal/runner/${jobId}/build/Garmin`, {method: 'PUT', headers: runnerHeaders(lease), body: gwc}));
  assert.equal(result.status, 200);
  assert.equal((await coordinator.fetch(req(`/internal/runner/${jobId}/complete`, {method: 'POST', headers: runnerHeaders(lease), body: JSON.stringify({results: [{device: 'Garmin', ok: true, messages: []}]})}))).status, 200);
  const status = await (await coordinator.fetch(req(`/api/v1/compile/${jobId}`))).json();
  assert.equal(status.status, 'done');
  const download = await coordinator.fetch(req(status.results[0].downloadUrl));
  assert.equal(download.status, 200);
  assert.deepEqual(new Uint8Array(await download.arrayBuffer()), gwc);
  assert.equal(download.headers.get('cache-control'), 'private, no-store');
});

test('24-hour expiry removes temporary sources and builds but preserves published sources', async () => {
  const {coordinator, storage, env} = setup();
  const {jobId} = await (await create(coordinator)).json();
  const job = await storage.get(`job:${jobId}`);
  assert.equal(job.expiresAt - job.createdAt, RETENTION_MS);
  job.expiresAt = Date.now() - 1;
  await storage.put(`job:${jobId}`, job);
  await env.TEMPORARY.put(`${jobId}/Garmin.gwc`, gwc);
  await env.SOURCES.put('published/original/source.gwz', zip);
  assert.equal((await coordinator.fetch(req(`/api/v1/compile/${jobId}`))).status, 410);
  await coordinator.alarm();
  assert.equal(env.TEMPORARY.objects.size, 0);
  assert.equal(env.SOURCES.objects.size, 1);
  assert.equal(await storage.get('reservedBytes'), 0);
});

test('failed deletion keeps its reservation and schedules retry', async () => {
  const {coordinator, storage, env} = setup();
  const {jobId} = await (await create(coordinator)).json();
  const job = await storage.get(`job:${jobId}`);
  job.expiresAt = 1;
  await storage.put(`job:${jobId}`, job);
  env.TEMPORARY.failDelete = true;
  await assert.rejects(coordinator.alarm());
  assert.equal(await storage.get('reservedBytes'), job.budget);
  assert.ok(storage.alarm > Date.now());
  env.TEMPORARY.failDelete = false;
  await coordinator.alarm();
  await coordinator.alarm();
  assert.equal(await storage.get('reservedBytes'), 0);
});

test('an uncertain R2 upload remains reserved until confirmed cleanup', async () => {
  const {coordinator, storage, env} = setup();
  env.TEMPORARY.failWrite = true;
  assert.equal((await create(coordinator)).status, 500);
  const [job] = (await storage.list({prefix:'job:'})).values();
  assert.equal(await storage.get('reservedBytes'), job.budget);
  assert.ok(storage.alarm);
  job.expiresAt = 1;
  await storage.put(`job:${job.id}`, job);
  await coordinator.alarm();
  assert.equal(await storage.get('reservedBytes'), 0);
  assert.equal(env.TEMPORARY.objects.size, 0);
});

test('refuses GWC input and mismatched compile/download selections', async () => {
  const {coordinator} = setup();
  assert.equal((await create(coordinator, ['Garmin'], ['iPhone'])).status, 400);
  assert.equal((await create(coordinator, ['Unknown'], [])).status, 400);
  const form = new FormData();
  form.set('cartridge', new File([gwc], 'test.gwc')); form.set('compile', 'Garmin');
  assert.equal((await coordinator.fetch(req('/api/v1/compile', {method:'POST', body:form}))).status, 400);
});

test('refuses fake GWC outputs, duplicate uploads, and success without requested output', async () => {
  const {coordinator} = setup();
  const {jobId} = await (await create(coordinator)).json();
  const {lease} = await (await coordinator.fetch(req('/internal/runner/claim', {method:'POST', headers:runnerHeaders()}))).json();
  const path = `/internal/runner/${jobId}/build/Garmin`;
  assert.equal((await coordinator.fetch(req(path, {method:'PUT', headers:runnerHeaders('wrong'), body:gwc}))).status, 409);
  assert.equal((await coordinator.fetch(req(path, {method:'PUT', headers:runnerHeaders(lease), body:zip}))).status, 400);
  const complete = () => coordinator.fetch(req(`/internal/runner/${jobId}/complete`, {method:'POST', headers:runnerHeaders(lease), body:JSON.stringify({results:[{device:'Garmin',ok:true}]})}));
  assert.equal((await complete()).status, 400);
  assert.equal((await coordinator.fetch(req(path, {method:'PUT', headers:runnerHeaders(lease), body:gwc}))).status, 200);
  assert.equal((await coordinator.fetch(req(path, {method:'PUT', headers:runnerHeaders(lease), body:gwc}))).status, 409);
  assert.equal((await complete()).status, 200);
});

test('does not accept uploads until service and runner are ready', async () => {
  const {coordinator, env} = setup(); env.SERVICE_ENABLED = 'false';
  assert.equal((await create(coordinator)).status, 503);
  env.SERVICE_ENABLED = 'true'; await coordinator.ctx.storage.put('runnerHeartbeat', 1);
  assert.equal((await coordinator.fetch(req('/api/v1/compile', {method:'POST',body:'unused'}))).status, 503);
});

test('bounded streaming rejects excessive data even without Content-Length', async () => {
  await assert.rejects(boundedBytes(req('/upload', {method:'POST',body:new Uint8Array(11)}), 10), error => error.status === 413);
});

test('CORS permits only the configured website', async () => {
  const env = {ALLOWED_ORIGIN:'https://wherigo.test', COORDINATOR:{idFromName:()=>0,get:()=>({fetch:()=>new Response('{}')})}};
  assert.equal((await worker.fetch(req('/api/v1/health', {headers:{Origin:'https://other.test'}}),env)).status,403);
  const response = await worker.fetch(req('/api/v1/health', {headers:{Origin:'https://wherigo.test'}}),env);
  assert.equal(response.headers.get('access-control-allow-origin'),'https://wherigo.test');
});

async function demo(coordinator) {
  const form = new FormData();
  form.set('cartridge',new File([zip],'approved.gwz'));
  for (const device of ['Garmin','iPhone','WhereYouGo']) { form.append('compile',device); form.append('download',device); }
  return coordinator.fetch(req('/internal/demo',{method:'POST',headers:{Authorization:'Bearer demo-test'},body:form}));
}

test('approved demo can publish while live uploads are disabled, and becomes public only when complete',async()=>{
  const {coordinator,env,storage} = setup(); env.SERVICE_ENABLED='false';
  const response = await demo(coordinator); assert.equal(response.status,202);
  const {jobId,lease} = await response.json();
  const headers = {Authorization:'Bearer demo-test','X-Job-Lease':lease};
  assert.equal((await coordinator.fetch(req('/api/v1/demo'))).status,404);
  for (const device of ['Garmin','iPhone','WhereYouGo']) {
    assert.equal((await coordinator.fetch(req(`/internal/demo/${jobId}/build/${device}`,{method:'PUT',headers,body:gwc}))).status,200);
  }
  assert.equal(await storage.get('latestDemo'),undefined);
  const results = ['Garmin','iPhone','WhereYouGo'].map(device=>({device,ok:true}));
  assert.equal((await coordinator.fetch(req(`/internal/demo/${jobId}/complete`,{method:'POST',headers,body:JSON.stringify({results})}))).status,200);
  const published = await (await coordinator.fetch(req('/api/v1/demo'))).json();
  assert.equal(published.results.length,3);
  const download = await coordinator.fetch(req(published.results[0].downloadUrl));
  assert.equal(download.status,200);
  assert.equal((await coordinator.fetch(req('/api/v1/compile',{method:'POST',body:'unused'}))).status,503);
});

test('demo credentials cannot modify visitor jobs, fetch source, or claim jobs',async()=>{
  const {coordinator} = setup();
  const {jobId} = await (await create(coordinator)).json();
  const headers = {Authorization:'Bearer demo-test'};
  assert.equal((await coordinator.fetch(req(`/internal/demo/${jobId}/build/Garmin`,{method:'PUT',headers,body:gwc}))).status,403);
  assert.equal((await coordinator.fetch(req(`/internal/demo/${jobId}/source`,{headers}))).status,404);
  assert.equal((await coordinator.fetch(req('/internal/runner/claim',{method:'POST',headers}))).status,401);
  assert.equal((await coordinator.fetch(req('/internal/demo',{method:'POST',headers:{Authorization:'Bearer runner-test'},body:'unused'}))).status,401);
});

test('demo reserves against the same 9 GB cap and expires in 24 hours',async()=>{
  const {coordinator,storage} = setup();
  await storage.put('reservedBytes',CAP_BYTES-1);
  assert.equal((await demo(coordinator)).status,507);
  await storage.put('reservedBytes',0);
  const {jobId} = await (await demo(coordinator)).json();
  const job = await storage.get(`job:${jobId}`);
  assert.equal(job.budget,zip.length+3*MAX_FILE_BYTES);
  assert.equal(job.expiresAt-job.createdAt,RETENTION_MS);
  job.expiresAt=1; await storage.put(`job:${jobId}`,job);
  await storage.put('latestDemo',jobId);
  assert.equal((await coordinator.fetch(req('/api/v1/demo'))).status,410);
  await coordinator.alarm();
  assert.equal(await storage.get('reservedBytes'),0);
});

async function validationUpload(coordinator) {
  await coordinator.ctx.storage.put('runnerHeartbeat',Date.now());
  const form=new FormData(); form.set('cartridge',new File([zip],'my-cartridge.gwz'));
  return coordinator.fetch(req('/api/v1/uploads/validate',{method:'POST',body:form}));
}
async function finishValidation(coordinator,ok,messages=[]) {
  const job=await(await coordinator.fetch(req('/internal/runner/claim',{method:'POST',headers:runnerHeaders()}))).json();
  const response=await coordinator.fetch(req(`/internal/runner/${job.jobId}/complete`,{method:'POST',headers:runnerHeaders(job.lease),body:JSON.stringify({results:[{device:'Emulator',ok,messages}]})}));
  assert.equal(response.status,200);
  return job;
}

test('upload validation returns a retained upload reference and detailed compiler feedback',async()=>{
  const {coordinator}=setup();
  const {uploadId}=await(await validationUpload(coordinator)).json();
  assert.ok(uploadId);
  const pending=await(await coordinator.fetch(req(`/api/v1/uploads/${uploadId}`))).json();
  assert.equal(pending.status,'checking');
  assert.equal(pending.filename,'my-cartridge.gwz');
  const diagnostics=['cartridge.lua, line 2: unexpected symbol','Line 1: local x = 1','Line 2: if then','Line 3: return cartridge'];
  await finishValidation(coordinator,false,diagnostics);
  const result=await(await coordinator.fetch(req(`/api/v1/uploads/${uploadId}`))).json();
  assert.equal(result.status,'invalid');
  assert.deepEqual(result.messages,diagnostics);
  assert.equal((await coordinator.fetch(req(`/internal/uploads/${uploadId}/publish`,{method:'POST',headers:{Authorization:'Bearer website-test'}}))).status,409);
});

test('only verified uploads can be retained for publication, idempotently and under the shared quota',async()=>{
  const {coordinator,storage,env}=setup();
  const {uploadId}=await(await validationUpload(coordinator)).json();
  const path=`/internal/uploads/${uploadId}/publish`;
  assert.equal((await coordinator.fetch(req(path,{method:'POST'}))).status,401);
  assert.equal((await coordinator.fetch(req(`/api/v1/uploads/${uploadId}/publish`,{method:'POST'}))).status,404);
  await finishValidation(coordinator,true);
  assert.equal((await(await coordinator.fetch(req(`/api/v1/uploads/${uploadId}`))).json()).status,'valid');
  const publish=()=>coordinator.fetch(req(path,{method:'POST',headers:{Authorization:'Bearer website-test'}}));
  const first=await publish(); assert.equal(first.status,201);
  const {sourceId}=await first.json();
  assert.equal((await(await publish()).json()).sourceId,sourceId);
  assert.equal(env.SOURCES.objects.size,1);
  assert.equal(await storage.get('reservedBytes'),zip.length*2);
  const job=await storage.get(`job:${uploadId}`); job.expiresAt=1; await storage.put(`job:${uploadId}`,job);
  await coordinator.alarm();
  assert.equal(env.SOURCES.objects.size,1);
  assert.equal(env.TEMPORARY.objects.size,0);
  assert.equal(await storage.get('reservedBytes'),zip.length);
});

test('demo/test jobs cannot masquerade as validated uploads and expired validation cannot publish',async()=>{
  const {coordinator,storage}=setup();
  const {jobId}=await(await create(coordinator)).json();
  assert.equal((await coordinator.fetch(req(`/api/v1/uploads/${jobId}`))).status,404);
  assert.equal((await coordinator.fetch(req(`/internal/uploads/${jobId}/publish`,{method:'POST',headers:{Authorization:'Bearer website-test'}}))).status,404);
  const {uploadId}=await(await validationUpload(coordinator)).json();
  const job=await storage.get(`job:${uploadId}`);job.expiresAt=1;await storage.put(`job:${uploadId}`,job);
  assert.equal((await coordinator.fetch(req(`/internal/uploads/${uploadId}/publish`,{method:'POST',headers:{Authorization:'Bearer website-test'}}))).status,410);
});

test('validation refuses to store files when there is no runner',async()=>{
  const {coordinator,env}=setup();env.SERVICE_ENABLED='false';
  assert.equal((await validationUpload(coordinator)).status,503);
  assert.equal(env.TEMPORARY.objects.size,0);
});
