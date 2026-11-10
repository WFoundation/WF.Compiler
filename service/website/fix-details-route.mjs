import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
const path=join(process.argv[2],'upload/new/index.html');
const text=await readFile(path,'utf8');
if(!text.includes('window.__WG_PATH__="/upload/"'))throw Error('Expected details shell route not found.');
await writeFile(path,text.replace('window.__WG_PATH__="/upload/"','window.__WG_PATH__="/upload/new/"'),'utf8');
