import {CAP_BYTES, MAX_FILE_BYTES, RETENTION_MS, DEVICES, HttpError, assert, boundedBytes, sourceFile, gwcFile, selectedDevices, reservation} from './policy.js';

const json = (value, status = 200) => new Response(JSON.stringify(value), {status, headers: {'Content-Type': 'application/json', 'Cache-Control': 'no-store'}});
const authenticated = (request, secret) => Boolean(secret) && request.headers.get('Authorization') === `Bearer ${secret}`;
const jobKey = id => `job:${id}`;

export default {
  async fetch(request, env) {
    const origin = request.headers.get('Origin');
    if (origin && origin !== env.ALLOWED_ORIGIN) return json({error: 'This website is not allowed to use the compiler.'}, 403);
    let response;
    if (request.method === 'OPTIONS') response = new Response(null, {status: 204});
    else {
      try {
        const id = env.COORDINATOR.idFromName('global-storage-and-jobs-v1');
        response = await env.COORDINATOR.get(id).fetch(request);
      } catch { response = json({error: 'The compiler service is temporarily unavailable.'}, 503); }
    }
    const headers = new Headers(response.headers);
    if (origin) {
      headers.set('Access-Control-Allow-Origin', origin);
      headers.set('Vary', 'Origin');
      headers.set('Access-Control-Allow-Methods', 'GET, POST, PUT, OPTIONS');
      headers.set('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    }
    headers.set('X-Content-Type-Options', 'nosniff');
    headers.set('Referrer-Policy', 'no-referrer');
    return new Response(response.body, {status: response.status, headers});
  }
};

// Every file-writing path goes through one coordinator. The serialized operation
// queue prevents interleaving across R2 awaits; durable reservations survive restarts.
// Do not write to these buckets through a second service or the dashboard.
export class CompilerCoordinator {
  constructor(ctx, env) { this.ctx = ctx; this.env = env; this.tail = Promise.resolve(); }
  serialize(work) {
    const result = this.tail.then(work);
    this.tail = result.catch(() => {});
    return result;
  }
  fetch(request) {
    return this.serialize(async () => {
      try { return await this.route(request); }
      catch (error) {
        return json({error: error instanceof HttpError ? error.message : 'The compiler service could not complete this request.'}, error.status || 500);
      }
    });
  }
  async reserve(bytes) {
    await this.ctx.storage.transaction(async tx => {
      const used = (await tx.get('reservedBytes')) || 0;
      assert(Number.isSafeInteger(bytes) && bytes > 0 && used + bytes <= CAP_BYTES, 507, 'Storage is full. Please try again after temporary builds have expired.');
      await tx.put('reservedBytes', used + bytes);
    });
  }
  async countRequest() {
    // A conservative public request budget limits polling and download abuse.
    // This is not an account-wide Cloudflare billing guarantee.
    const day = Math.floor(Date.now() / 86400000);
    const old = await this.ctx.storage.get('requests');
    const count = old?.day === day ? old.count + 1 : 1;
    assert(count <= 20_000, 429, 'The compiler has reached its daily request limit. Please try again tomorrow.');
    await this.ctx.storage.put('requests', {day, count});
  }
  async job(id) {
    assert(/^[a-f0-9-]{36}$/.test(id), 404, 'Compilation not found.');
    const job = await this.ctx.storage.get(jobKey(id));
    assert(job && job.expiresAt > Date.now(), 410, 'This compilation has expired. Upload the source again to create a new download.');
    return job;
  }
  async route(request) {
    const path = new URL(request.url).pathname;
    const method = request.method;
    if (path === '/api/v1/health' && method === 'GET') {
      const heartbeat = await this.ctx.storage.get('runnerHeartbeat');
      return json({ok: true, compilerReady: this.env.SERVICE_ENABLED === 'true' && Date.now() - (heartbeat || 0) < 120_000, version: 'wf-service-1'});
    }
    if (path.startsWith('/internal/runner/')) {
      assert(authenticated(request, this.env.RUNNER_TOKEN), 401, 'Runner authentication required.');
      return this.runner(request, path);
    }
    if (path === '/internal/demo' || path.startsWith('/internal/demo/')) {
      assert(authenticated(request, this.env.DEMO_PUBLISH_TOKEN), 401, 'Demo publisher authentication required.');
      if (path === '/internal/demo' && method === 'POST') {
        const {bytes, devices, downloads} = await this.input(request);
        assert(devices.length === 3 && ['Garmin', 'iPhone', 'WhereYouGo'].every(d => devices.includes(d) && downloads.includes(d)), 400, 'The approved demo requires Garmin, iPhone and WhereYouGo builds.');
        return this.createJob(bytes, devices, downloads, true);
      }
      const demoMatch = path.match(/^\/internal\/demo\/([a-f0-9-]+)\/(complete|build\/[A-Za-z0-9]+)$/);
      assert(demoMatch, 404, 'Demo endpoint not found.');
      const demoJob = await this.job(demoMatch[1]);
      assert(demoJob.demo, 403, 'The demo publisher cannot modify visitor jobs.');
      return this.runner(request, path.replace('/internal/demo/', '/internal/runner/'));
    }
    // These internal endpoints are for the authenticated website backend only.
    // Never put WEBSITE_TOKEN in browser JavaScript or localStorage.
    if (path === '/internal/sources' && method === 'POST') {
      assert(authenticated(request, this.env.WEBSITE_TOKEN), 401, 'Website authentication required.');
      const bytes = await boundedBytes(request);
      sourceFile(bytes, request.headers.get('X-Filename') || '');
      assert(request.headers.get('X-Validated') === 'true', 400, 'Validate the cartridge before retaining its source.');
      const id = crypto.randomUUID(), key = `published/${id}/source.gwz`;
      await this.reserve(bytes.length);
      // Persist the reservation before the R2 write. An uncertain write stays
      // counted until an operator reconciles it; failures cannot create free space.
      await this.ctx.storage.put(`source:${id}`, {key, bytes: bytes.length, status: 'writing'});
      await this.env.SOURCES.put(key, bytes, {httpMetadata: {contentType: 'application/zip'}});
      await this.ctx.storage.put(`source:${id}`, {key, bytes: bytes.length, status: 'ready'});
      return json({sourceId: id}, 201);
    }
    if (path === '/internal/storage' && method === 'GET') {
      assert(authenticated(request, this.env.WEBSITE_TOKEN), 401, 'Website authentication required.');
      return json({reservedBytes: (await this.ctx.storage.get('reservedBytes')) || 0, capBytes: CAP_BYTES});
    }
    if (path.startsWith('/api/v1/compile') || path.startsWith('/api/v1/uploads') || path === '/api/v1/demo') await this.countRequest();
    if (path === '/api/v1/uploads/validate' && method === 'POST') {
      const heartbeat = await this.ctx.storage.get('runnerHeartbeat');
      assert(this.env.SERVICE_ENABLED === 'true' && Date.now() - (heartbeat || 0) < 120_000, 503, 'The cartridge checker is currently unavailable. Your cartridge has not been checked. Please try again later.');
      const {bytes, filename} = await this.input(request, true);
      return this.createJob(bytes, ['Emulator'], [], false, {purpose: 'upload', filename});
    }
    const uploadMatch = path.match(/^\/(api\/v1|internal)\/uploads\/([a-f0-9-]+)(\/publish)?$/);
    if (uploadMatch) {
      const internal = uploadMatch[1] === 'internal';
      if (internal) assert(authenticated(request, this.env.WEBSITE_TOKEN), 401, 'Website authentication required.');
      assert(internal || !uploadMatch[3], 404, 'Not found.');
      const job = await this.job(uploadMatch[2]);
      assert(job.purpose === 'upload', 404, 'This is not a cartridge upload.');
      const valid = job.status === 'done' && job.results.length > 0 && job.results.every(r => r.ok);
      if (uploadMatch[3] && method === 'POST') {
        assert(valid, 409, 'This cartridge must pass validation before it can be published.');
        return this.retainValidatedSource(job);
      }
      assert(!uploadMatch[3] && method === 'GET', 405, 'Method not allowed.');
      return json({uploadId: job.id, filename: job.filename, expiresAt: job.expiresAt,
        status: valid ? 'valid' : job.status === 'failed' || job.status === 'done' ? 'invalid' : 'checking',
        messages: job.results.flatMap(r => r.messages || [])});
    }
    if (path === '/api/v1/demo' && method === 'GET') {
      const id = await this.ctx.storage.get('latestDemo');
      assert(id, 404, 'No demonstration downloads have been published yet.');
      const job = await this.job(id);
      assert(job.status === 'done' && job.results.every(r => r.ok), 404, 'The demonstration is not ready.');
      return json({title: 'Wherigo Compiler Demonstration', builtAt: job.createdAt, expiresAt: job.expiresAt,
        results: job.results.map(r => ({device: r.device, downloadUrl: `/api/v1/compile/${job.id}/download/${r.device}`}))});
    }
    if (path === '/api/v1/compile/devices' && method === 'GET') return json({devices: DEVICES});
    if (path === '/api/v1/compile' && method === 'POST') {
      const heartbeat = await this.ctx.storage.get('runnerHeartbeat');
      assert(this.env.SERVICE_ENABLED === 'true' && Date.now() - (heartbeat || 0) < 120_000, 503, 'The compiler is not available yet. No file has been stored.');
      const {bytes, devices, downloads} = await this.input(request);
      return this.createJob(bytes, devices, downloads);
    }
    const match = path.match(/^\/api\/v1\/compile\/([a-f0-9-]+)(?:\/download\/([A-Za-z0-9]+))?$/);
    if (match && method === 'GET') {
      const job = await this.job(match[1]);
      if (match[2]) {
        const device = match[2];
        assert(job.downloads.includes(device) && job.results.some(r => r.device === device && r.ok), 404, 'This download is not available.');
        const object = await this.env.TEMPORARY.get(`${job.id}/${device}.gwc`);
        assert(object, 410, 'This download has expired. Please compile the cartridge again.');
        return new Response(object.body, {headers: {'Content-Type': 'application/octet-stream', 'Content-Disposition': `attachment; filename="cartridge-${device}.gwc"`, 'Cache-Control': 'private, no-store'}});
      }
      return json({status: job.status === 'queued' ? 'running' : job.status, expiresAt: job.expiresAt,
        results: job.results.map(r => ({...r, downloadUrl: r.ok && job.downloads.includes(r.device) ? `/api/v1/compile/${job.id}/download/${r.device}` : null}))});
    }
    throw new HttpError(404, 'Not found.');
  }
  async input(request, validationOnly = false) {
    const body = await boundedBytes(request, MAX_FILE_BYTES + 100_000);
    const form = await new Response(body, {headers: {'Content-Type': request.headers.get('Content-Type') || ''}}).formData();
    const file = form.get('cartridge');
    assert(file && typeof file.arrayBuffer === 'function' && file.size <= MAX_FILE_BYTES, 400, 'Choose a GWZ source package up to 20 MB.');
    const bytes = new Uint8Array(await file.arrayBuffer());
    sourceFile(bytes, file.name);
    return {bytes, filename: file.name.slice(0, 255), ...(validationOnly ? {} : selectedDevices(form.getAll('compile'), form.getAll('download')))};
  }
  async createJob(bytes, devices, downloads, demo = false, upload = {}) {
    const now = Date.now(), id = crypto.randomUUID();
    const job = {id, devices, downloads, sourceSize: bytes.length, budget: reservation(bytes.length, downloads),
      createdAt: now, expiresAt: now + RETENTION_MS, status: 'uploading', results: [], builds: {}, demo,
      purpose: upload.purpose || 'test', filename: upload.filename || ''};
    await this.reserve(job.budget);
    await this.ctx.storage.put(jobKey(id), job);
    // Schedule cleanup BEFORE uploading, so a crash cannot strand a temporary job.
    const alarm = await this.ctx.storage.getAlarm();
    if (!alarm || alarm > job.expiresAt) await this.ctx.storage.setAlarm(job.expiresAt);
    await this.env.TEMPORARY.put(`${id}/source.gwz`, bytes, {httpMetadata: {contentType: 'application/zip'}});
    job.status = demo ? 'running' : 'queued';
    if (demo) { job.startedAt = now; job.lease = crypto.randomUUID(); }
    await this.ctx.storage.put(jobKey(id), job);
    return json({jobId: id, ...(job.purpose === 'upload' ? {uploadId: id} : {}), expiresAt: job.expiresAt, ...(demo ? {lease: job.lease} : {})}, 202);
  }
  async retainValidatedSource(job) {
    // Only the authenticated publication backend calls this after checking
    // ownership/session and before creating the listing. Browser state is never
    // accepted as proof that the Lua/media checks passed.
    if (job.publishedSourceId) return json({sourceId: job.publishedSourceId});
    let id = job.publishIntent;
    if (!id) {
      id = crypto.randomUUID();
      await this.reserve(job.sourceSize);
      await this.ctx.storage.put(`source:${id}`, {key: `published/${id}/source.gwz`, bytes: job.sourceSize, status: 'writing'});
      job.publishIntent = id;
      await this.ctx.storage.put(jobKey(job.id), job);
    }
    const source = await this.env.TEMPORARY.get(`${job.id}/source.gwz`);
    assert(source, 410, 'This upload has expired. Please upload the cartridge again.');
    const key = `published/${id}/source.gwz`;
    await this.env.SOURCES.put(key, source.body, {httpMetadata: {contentType: 'application/zip'}});
    await this.ctx.storage.put(`source:${id}`, {key, bytes: job.sourceSize, status: 'ready'});
    job.publishedSourceId = id;
    await this.ctx.storage.put(jobKey(job.id), job);
    return json({sourceId: id}, 201);
  }
  async runner(request, path) {
    if (path === '/internal/runner/claim' && request.method === 'POST') {
      await this.ctx.storage.put('runnerHeartbeat', Date.now());
      const jobs = await this.ctx.storage.list({prefix: 'job:'});
      // No automatic requeue: an uncertain output must never be overwritten by a
      // competing runner. A timed-out job is failed and cleaned up at expiry.
      for (const job of jobs.values()) {
        if (job.demo) continue;
        if (job.status === 'running' && Date.now() - job.startedAt > 10 * 60_000) {
          job.status = 'failed';
          job.results = job.devices.map(device => ({device, ok: false, messages: ['Compilation timed out. Please try again.']}));
          await this.ctx.storage.put(jobKey(job.id), job);
        }
        if (job.status !== 'queued' || job.expiresAt <= Date.now()) continue;
        job.status = 'running'; job.startedAt = Date.now(); job.lease = crypto.randomUUID();
        await this.ctx.storage.put(jobKey(job.id), job);
        return json({jobId: job.id, lease: job.lease, devices: job.devices, downloads: job.downloads});
      }
      return new Response(null, {status: 204});
    }
    const match = path.match(/^\/internal\/runner\/([a-f0-9-]+)\/(source|complete|build\/[A-Za-z0-9]+)$/);
    assert(match, 404, 'Runner endpoint not found.');
    const job = await this.job(match[1]);
    assert(job.status === 'running' && request.headers.get('X-Job-Lease') === job.lease, 409, 'This job is no longer assigned to this runner.');
    if (match[2] === 'source' && request.method === 'GET') {
      const object = await this.env.TEMPORARY.get(`${job.id}/source.gwz`);
      assert(object, 410, 'The source has expired.');
      return new Response(object.body, {headers: {'Content-Type': 'application/zip', 'Cache-Control': 'no-store'}});
    }
    if (match[2].startsWith('build/') && request.method === 'PUT') {
      const device = match[2].slice(6);
      assert(job.downloads.includes(device) && !job.builds[device], 409, 'This build is not requested or was already uploaded.');
      const bytes = await boundedBytes(request);
      gwcFile(bytes);
      job.builds[device] = {status: 'writing'};
      await this.ctx.storage.put(jobKey(job.id), job);
      await this.env.TEMPORARY.put(`${job.id}/${device}.gwc`, bytes, {httpMetadata: {contentType: 'application/octet-stream'}});
      job.builds[device] = {status: 'ready', bytes: bytes.length};
      await this.ctx.storage.put(jobKey(job.id), job);
      return json({ok: true});
    }
    if (match[2] === 'complete' && request.method === 'POST') {
      const data = JSON.parse(new TextDecoder().decode(await boundedBytes(request, 32_000)));
      assert(Array.isArray(data.results) && data.results.length === job.devices.length, 400, 'Return one result for each device.');
      assert(new Set(data.results.map(r => r.device)).size === job.devices.length, 400, 'Duplicate device result.');
      job.results = data.results.map(r => {
        assert(job.devices.includes(r.device) && typeof r.ok === 'boolean', 400, 'Invalid device result.');
        assert(!r.ok || !job.downloads.includes(r.device) || job.builds[r.device]?.status === 'ready', 400, 'Upload the requested build before reporting success.');
        return {device: r.device, ok: r.ok, messages: (Array.isArray(r.messages) ? r.messages : []).slice(0, 8).map(m => String(m).slice(0, 2000))};
      });
      job.status = job.results.some(r => r.ok) ? 'done' : 'failed';
      // Keep the conservative reservation until confirmed deletion. This also
      // counts uncertain writes following a crash against the global cap.
      await this.ctx.storage.put(jobKey(job.id), job);
      if (job.demo && job.results.every(r => r.ok)) await this.ctx.storage.put('latestDemo', job.id);
      return json({ok: true});
    }
    throw new HttpError(405, 'Method not allowed.');
  }
  alarm() { return this.serialize(() => this.cleanup()); }
  async cleanup() {
    // Re-arm first: if R2 deletion fails, reservations remain and a retry exists.
    await this.ctx.storage.setAlarm(Date.now() + 60_000);
    const jobs = await this.ctx.storage.list({prefix: 'job:'});
    let next = Infinity;
    for (const job of jobs.values()) {
      if (job.expiresAt > Date.now()) { next = Math.min(next, job.expiresAt); continue; }
      await this.env.TEMPORARY.delete([`${job.id}/source.gwz`, ...job.downloads.map(d => `${job.id}/${d}.gwc`)]);
      await this.ctx.storage.transaction(async tx => {
        const current = await tx.get(jobKey(job.id));
        if (!current) return;
        const used = (await tx.get('reservedBytes')) || 0;
        await tx.put('reservedBytes', Math.max(0, used - current.budget));
        await tx.delete(jobKey(job.id));
      });
    }
    if (Number.isFinite(next)) await this.ctx.storage.setAlarm(next);
    else await this.ctx.storage.deleteAlarm();
  }
}
