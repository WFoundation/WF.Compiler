import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {parse} from 'yaml';

test('demo remains a manual public-repository project test using a standard runner',async()=>{
  const workflow=parse(await readFile(new URL('../../.github/workflows/compiler-demo.yml',import.meta.url),'utf8'));
  assert.deepEqual(Object.keys(workflow.on),['workflow_dispatch']);
  assert.deepEqual(workflow.permissions,{contents:'read'});
  const job=workflow.jobs.compile;
  assert.equal(job['runs-on'],'windows-2022');
  assert.match(job.if,/github\.event\.repository\.private == false/);
  assert.ok(job['timeout-minutes']<=10);
  assert.equal(workflow.on.workflow_dispatch.inputs.publish_to_website.default,false);
  for(const step of job.steps.filter(s=>s.uses))assert.match(step.uses,/^actions\/[a-z-]+@[a-f0-9]{40}$/);
  const artifact=job.steps.find(s=>s.uses?.startsWith('actions/upload-artifact@'));
  assert.equal(artifact.with['retention-days'],1);
  const publisher=job.steps.find(s=>s.env?.DEMO_PUBLISH_TOKEN);
  assert.equal(job.steps.filter(s=>s.env?.DEMO_PUBLISH_TOKEN).length,1);
  assert.match(publisher.if,/inputs\.publish_to_website/);
  assert.ok(job.steps.indexOf(publisher)>job.steps.findIndex(s=>s.run?.includes('build-demo.ps1')));
});
