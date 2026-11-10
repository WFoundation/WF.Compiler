import {readFile,writeFile,readdir,mkdir} from 'node:fs/promises';
import {join,resolve} from 'node:path';
const root=resolve(process.argv[2]||'');
if(!process.argv[2])throw Error('Pass the existing website checkout.');
const changes=new Map();
async function edit(file, replacements){
  let text=changes.get(join(root,file))??await readFile(join(root,file),'utf8');
  for(const [before,after]of replacements){
    if(!text.includes(before))throw Error(`Expected content changed in ${file}. No files written.`);
    text=text.replace(before,after);
  }
  changes.set(join(root,file),text);
}
await edit('js/app.js',[
  ["import {renderUploadCartridge}","import {renderUploadStart, renderUploadError, installUploadFlow} from './upload-flow.js?v=20261109upload1';\nimport {renderUploadCartridge}"],
  ["  'upload/new': renderUploadCartridge,","  'upload/new': renderUploadCartridge,\n  'upload/error': renderUploadError,"],
  ['  upload: protectedPage,','  upload: renderUploadStart,'],
  ["main.innerHTML = (pages[route] || renderNotFound)();","main.innerHTML = (pages[route] || renderNotFound)();\nif (['upload', 'upload/new', 'upload/error'].includes(route)) installUploadFlow(route).catch(console.error);"],
  ["    'upload/new': 'build',","    'upload/new': 'build',\n    'upload/error': 'build',"],
  ["'upload', 'upload/new', 'compile'","'upload', 'upload/new', 'upload/error', 'compile'"]
]);
await edit('js/upload-cartridge.js',[
  ["import {builders}","import {renderUploadNotice} from './upload-flow.js?v=20261109upload1';\nimport {builders}"],
  ['    `<div class="os-page">','    `<div class="os-page">${renderUploadNotice()}'],
  ['<form class="os-upload" novalidate>','<form class="os-upload" data-publish-cartridge novalidate>'],
  ['id="up-title" name="title" type="text"','id="up-title" name="title" type="text" required maxlength="160"'],
  ['<button class="button" type="submit">Submit for review</button>','<button class="button" type="submit" data-publish-button disabled>Publish</button>'],
  ['    </form></div>`','      <p data-publish-status role="status" aria-live="polite" hidden></p>\n    </form></div>`']
]);
const detailsPath=join(root,'js/upload-cartridge.js');
const details=changes.get(detailsPath);
if(!details.includes("field('up-file'"))throw Error('The details file chooser has already changed.');
changes.set(detailsPath,details.split('\n').filter(line=>!line.includes("field('up-file'")).join('\n'));
await edit('js/route-metadata.js',[
  ["  'upload/new': ['Upload a Cartridge', 'Share your completed adventure, and choose whether other players can copy it.'],","  'upload/new': ['Upload a Cartridge', 'Add listing details for your validated cartridge and publish your adventure.'],\n  'upload/error': ['Cartridge Check Failed', 'Read the cartridge checker’s error report, correct your GWZ, and try again.'],"],
  ["  upload: ['Upload a Cartridge', 'Continue to Wherigo.com to upload a completed cartridge.'],","  upload: ['Upload a Cartridge', 'Upload a GWZ and check its Lua source and media before adding listing details.'],"]
]);
const apiPath=join(root,'js/api.js');
let api=await readFile(apiPath,'utf8');
if(api.includes('function startUploadValidation'))throw Error('Upload API integration already exists.');
api+=`
// Validate once, then publish the server-retained source by its upload reference.
export function startUploadValidation(file) {
  const form = new FormData();
  form.append('cartridge', file);
  return request('/uploads/validate', {method: 'POST', form, compiler: true});
}
export const uploadValidationStatus = uploadId => request('/uploads/' + encodeURIComponent(uploadId), {compiler: true});
export const publishValidatedCartridge = (uploadId, details) => request('/cartridges', {method: 'POST', json: {uploadId, ...details}});
`;
changes.set(apiPath,api);
changes.set(join(root,'js/upload-flow.js'),await readFile(new URL('./upload-flow.js',import.meta.url),'utf8'));
const newShell=await readFile(join(root,'upload/new/index.html'),'utf8');
changes.set(join(root,'upload/error/index.html'),newShell.replace('window.__WG_PATH__="/upload/new/"','window.__WG_PATH__="/upload/error/"').replace('<title>Upload a Cartridge</title>','<title>Cartridge Check Failed</title>').replace('Share your completed adventure, and choose whether other players can copy it.','Read the cartridge check report and correct your GWZ source package.'));
const cssPath=join(root,'css/site.css');
let css=await readFile(cssPath,'utf8');
css+=`
/* Cartridge upload/check flow */
.upload-validation-notice {padding:1rem; margin:0 0 1.5rem; border:1px solid var(--border, #cad5e2); border-radius:8px;}
.upload-validation-notice p {margin:0 0 .5rem;}
.upload-diagnostics {margin:1.5rem 0;}
.upload-diagnostics ul {padding-left:1.5rem;}
.upload-diagnostics li {margin:.7rem 0;}
.upload-diagnostics pre {margin:0; white-space:pre-wrap; overflow-wrap:anywhere; font-size:.9rem;}
[data-upload-check-status], [data-publish-status] {margin-top:1rem;}
`;
changes.set(cssPath,css);
async function gather(directory){
  for(const entry of await readdir(directory,{withFileTypes:true})){
    if(['.git','assets','data','node_modules'].includes(entry.name))continue;
    const path=join(directory,entry.name);
    if(entry.isDirectory())await gather(path);
    else if(/\.(html|js)$/.test(entry.name)){
      let text=changes.get(path)??await readFile(path,'utf8');
      if(entry.name!=='upload-flow.js')text=text.replace(/(["'])\/upload\/new\/(?=[?"'])/g,'$1/upload/');
      const updated=text.replaceAll('?v=20261027zzx','?v=20261109upload1').replaceAll('?v=20261109compiler2','?v=20261109upload1');
      if(updated!==text||changes.has(path))changes.set(path,updated);
      else if(text!==(await readFile(path,'utf8')))changes.set(path,text);
    }
  }
}
await gather(root);
// Newly generated shells were not present during directory traversal.
for(const[path,text]of changes)changes.set(path,text.replaceAll('?v=20261027zzx','?v=20261109upload1').replaceAll('?v=20261109compiler2','?v=20261109upload1'));
// Entry links change to /upload/, but the details shell must keep its own route.
const detailsShellPath=join(root,'upload/new/index.html');
changes.set(detailsShellPath,(changes.get(detailsShellPath)??newShell).replace('window.__WG_PATH__="/upload/"','window.__WG_PATH__="/upload/new/"'));
for(const[path,text]of changes){await mkdir(resolve(path,'..'),{recursive:true});await writeFile(path,text,'utf8');}
console.log('Added upload/check and diagnostic pages, preserved the details form, and wired validated-source publication.');
