import {readFile,writeFile,readdir} from 'node:fs/promises';
import {join,resolve} from 'node:path';
const root=resolve(process.argv[2]||'');
if(!process.argv[2])throw Error('Pass the website checkout path.');
const pending=new Map();
const api=join(root,'js/api.js');
let apiText=await readFile(api,'utf8');
if(!apiText.includes('// ---- Test compiler ----'))throw Error('The website API source has changed.');
apiText=apiText.replace('// ---- Test compiler ----',"// ---- Approved demonstration downloads (independent of live compilation) ----\nexport const demoBuilds = () => request('/demo', {compiler: true});\n\n// ---- Test compiler ----");
pending.set(api,apiText);
const page=join(root,'js/test-compiler.js');
let pageText=await readFile(page,'utf8');
for(const [before,after]of[
  ["import {isApi}","import {getConfig, isApi}"],
  ["import {startCompile, compileStatus, resolveUrl}","import {startCompile, compileStatus, resolveUrl, demoBuilds}"],
  ['  <form class="compiler-form"',`  <section aria-labelledby="compiler-demo-heading">
    <h2 id="compiler-demo-heading">Try a compiled demonstration</h2>
    <p>Download a small play-anywhere cartridge built with WF.Compiler. Enter DEMO in your player to finish. These are prebuilt demonstration files; the button does not compile an uploaded cartridge.</p>
    <div class="toolbar"><button class="button" type="button" data-compiler-demo>Show demonstration downloads</button></div>
    <p data-compiler-demo-status role="status" hidden></p>
  </section>
  <form class="compiler-form"`]
]){
  if(!pageText.includes(before))throw Error('The compiler page source has changed. No files written.');
  pageText=pageText.replace(before,after);
}
pageText+=`
// Approved sample downloads work while public upload compilation is disabled.
document.addEventListener('click', async event => {
  const button = event.target.closest?.('[data-compiler-demo]');
  if (!button || button.disabled) return;
  const status = button.closest('section').querySelector('[data-compiler-demo-status]');
  status.hidden = false;
  if (!getConfig().compilerBaseUrl) {
    status.textContent = 'Demonstration downloads have not been connected to this preview yet.';
    return;
  }
  button.disabled = true;
  status.textContent = 'Loading demonstration downloads…';
  try {
    const demo = await demoBuilds();
    status.replaceChildren();
    for (const result of demo.results || []) {
      const anchor = document.createElement('a');
      anchor.href = resolveUrl(result.downloadUrl, true);
      anchor.textContent = 'Download for ' + result.device;
      status.append(anchor, document.createElement('br'));
    }
    status.append('Downloads expire ' + new Date(demo.expiresAt).toLocaleString() + '.');
  } catch (error) { status.textContent = error.message; }
  finally { button.disabled = false; }
});
`;
pending.set(page,pageText);
async function gather(directory){
  for(const entry of await readdir(directory,{withFileTypes:true})){
    if(['.git','assets','data','node_modules'].includes(entry.name))continue;
    const path=join(directory,entry.name);
    if(entry.isDirectory())await gather(path);
    else if(/\.(html|js)$/.test(entry.name)){
      const text=pending.get(path)??await readFile(path,'utf8');
      const updated=text.replaceAll('?v=20261109compiler1','?v=20261109compiler2');
      if(updated!==text||pending.has(path))pending.set(path,updated);
    }
  }
}
await gather(root);
for(const[path,text]of pending)await writeFile(path,text,'utf8');
console.log('Added demonstration downloads to Test Compiler; refreshed cache tags.');
