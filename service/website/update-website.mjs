// Apply the prepared integration to the existing website, preserving unrelated files.
import {readFile, writeFile, readdir} from 'node:fs/promises';
import {join, resolve} from 'node:path';
const root = resolve(process.argv[2] || '');
if (!process.argv[2]) throw Error('Pass the website checkout path.');
const replacements = {
  'js/config.js': [
    ["  apiPath: '/api/v1',", "  compilerBaseUrl: '', // HTTPS URL of the separate compiler service; leave empty until deployed.\n  apiPath: '/api/v1',"]
  ],
  'js/api.js': [
    ["async function request(path, {method = 'GET', query, json, form, signal} = {})", "async function request(path, {method = 'GET', query, json, form, signal, compiler = false} = {})"],
    ["  const url = new URL(root() + path, location.href);", "  const apiRoot = compiler && c.compilerBaseUrl ? c.compilerBaseUrl.replace(/\\/+$/, '') + c.apiPath : root();\n  const url = new URL(apiRoot + path, location.href);"],
    ["export const resolveUrl = path => new URL(path, new URL(getConfig().baseUrl || '/', location.href)).href;", "export const resolveUrl = (path, compiler = false) => new URL(path, new URL((compiler && getConfig().compilerBaseUrl) || getConfig().baseUrl || '/', location.href)).href;"],
    ["return request('/compile', {method: 'POST', form});", "return request('/compile', {method: 'POST', form, compiler: true});"],
    ["return request('/compile/' + encodeURIComponent(jobId));", "return request('/compile/' + encodeURIComponent(jobId), {compiler: true});"]
  ],
  'js/test-compiler.js': [
    ['accept=".gwz,.gwc,.zip"', 'accept=".gwz"'],
    ['    <h2>Available Devices</h2>', '    <p>Upload a GWZ source package up to 20 MB. Test uploads and generated downloads expire after 24 hours. Testing does not publish your cartridge.</p>\n    <h2>Available Devices</h2>'],
    ['esc(resolveUrl(r.downloadUrl))', 'esc(resolveUrl(r.downloadUrl, true))'],
    ["        return;\n      }\n      await wait(1500);", "        if (job.expiresAt) {\n          const expiry = document.createElement('span');\n          expiry.textContent = ' Downloads expire ' + new Date(job.expiresAt).toLocaleString() + '.';\n          status.append(expiry);\n        }\n        return;\n      }\n      await wait(1500);"],
    ["    status.textContent = e.message;\n  }\n}", "    status.textContent = e.message;\n  } finally {\n    form.querySelector('button[type=submit]').disabled = false;\n    delete form.dataset.compiling;\n  }\n}"],
    ["  event.preventDefault();\n  const status", "  event.preventDefault();\n  if (form.dataset.compiling) return;\n  const status"],
    ["  else if (!devices)", "  else if (!/\\.gwz$/i.test(file.name)) status.textContent = 'Choose a GWZ source package. Compiled GWC files cannot be recompiled.';\n  else if (file.size > 20000000) status.textContent = 'The source package must be 20 MB or smaller.';\n  else if (checked(form, 'download').some(device => !checked(form, 'compile').includes(device))) status.textContent = 'Select Compile for every device you want to download.';\n  else if (!devices)"],
    ["  else if (isApi('compiler')) compileWithApi(form, status, file);", "  else if (isApi('compiler')) {\n    form.dataset.compiling = 'true';\n    form.querySelector('button[type=submit]').disabled = true;\n    compileWithApi(form, status, file);\n  }"]
  ],
  'js/app.js': [["['/compile/', 'Compile and test a cartridge']", "['/test-compiler/', 'Compile and test a cartridge']"]]
};
const pending = new Map();
for (const [file, changes] of Object.entries(replacements)) {
  let text = await readFile(join(root,file),'utf8');
  for (const [before,after] of changes) {
    if (!text.includes(before)) throw Error(`Expected source has changed: ${file}. No files written.`);
    text = text.replace(before,after);
  }
  pending.set(join(root,file),text);
}
// The website's README requires cache tags to change consistently.
async function gather(directory) {
  for (const entry of await readdir(directory,{withFileTypes:true})) {
    if (['.git','assets','data','node_modules'].includes(entry.name)) continue;
    const path = join(directory,entry.name);
    if (entry.isDirectory()) await gather(path);
    else if (/\.(html|js)$/.test(entry.name)) {
      const text = pending.get(path) ?? await readFile(path,'utf8');
      const updated = text.replaceAll('?v=20261027zzx','?v=20261109compiler1');
      if (updated !== text || pending.has(path)) pending.set(path,updated);
    }
  }
}
await gather(root);
for (const [path,text] of pending) await writeFile(path,text,'utf8');
console.log(`Updated ${pending.size} website files; compiler stays disabled until its service is deployed.`);
