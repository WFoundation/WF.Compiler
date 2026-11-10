// Upload only the repository's approved, locally generated demo outputs.
// The GitHub workflow supplies credentials ONLY to this post-compilation step.
import {readFile, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const directory = new URL('./output/', import.meta.url);
const manifest = JSON.parse((await readFile(new URL('manifest.json', directory),'utf8')).replace(/^\uFEFF/,''));
const base = process.env.COMPILER_SERVICE_URL;
const token = process.env.DEMO_PUBLISH_TOKEN;
if (!base || new URL(base).protocol !== 'https:' || !token) throw Error('Configure COMPILER_SERVICE_URL and DEMO_PUBLISH_TOKEN before publishing.');
const targets = ['Garmin','iPhone','WhereYouGo'];
if (!Array.isArray(manifest.builds) || manifest.builds.length !== 3 || !targets.every(d => manifest.builds.some(b => b.device === d && b.filename === `demonstration-${d}.gwc`))) throw Error('The approved demo manifest is invalid.');
const files = new Map();
for (const build of manifest.builds) {
  const bytes = await readFile(new URL(build.filename,directory));
  if (bytes.length !== build.bytes || createHash('sha256').update(bytes).digest('hex') !== build.sha256) throw Error('Demo output integrity check failed.');
  files.set(build.device,bytes);
}
async function api(path, options = {}) {
  const response = await fetch(new URL(path,base), {...options, signal:AbortSignal.timeout(60_000),headers:{Authorization:`Bearer ${token}`,...options.headers}});
  if (!response.ok) throw Error(`Demo publication failed (HTTP ${response.status}).`);
  return response.json();
}
const form = new FormData();
form.set('cartridge',new File([await readFile(new URL('demonstration.gwz',directory))],'demonstration.gwz'));
for (const device of targets) { form.append('compile',device); form.append('download',device); }
const job = await api('/internal/demo',{method:'POST',body:form});
const headers = {'X-Job-Lease':job.lease};
for (const device of targets) await api(`/internal/demo/${job.jobId}/build/${device}`,{method:'PUT',headers,body:files.get(device)});
await api(`/internal/demo/${job.jobId}/complete`,{method:'POST',headers,body:JSON.stringify({results:targets.map(device=>({device,ok:true,messages:[]}))})});
const published = {title:manifest.title,expiresAt:job.expiresAt,downloads:targets.map(device=>({device,url:new URL(`/api/v1/compile/${job.jobId}/download/${device}`,base).href}))};
await writeFile(new URL('published.json',directory),JSON.stringify(published,null,2));
console.log(`Published three approved demonstration downloads, expiring ${new Date(job.expiresAt).toISOString()}.`);
