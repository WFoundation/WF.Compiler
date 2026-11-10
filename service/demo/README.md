# Controlled GitHub Actions demonstration

The goal is a few genuine compiler builds and website downloads that work while
the developer's computer is off. This is a manual compiler-project demonstration,
not a public service dispatching arbitrary visitor jobs to GitHub Actions.

## What the workflow does

`Build approved compiler demonstration` runs only through `workflow_dispatch`.
It uses a standard `windows-2022` runner and refuses private repositories. It has
read-only repository permissions, a ten-minute timeout, no schedule, no visitor
input/source URLs and no paid larger-runner label. The official checkout and
artifact actions are pinned to verified revisions.

It builds WF.Compiler, tests nine profile headers/bytecode and syntax diagnostics,
then compiles this directory's approved play-anywhere `cartridge.lua` for Garmin,
iPhone and WhereYouGo. A one-day artifact contains the source package, GWC files,
compiler reports and SHA-256 manifest. Source and outputs are public demo material.
Compilation is verified locally. Real-player behavior has not yet been verified.

The upstream x86 Lua DLL requires Microsoft's Visual C++ 2012 runtime. The first
GitHub run exposed its absence on the hosted image. The workflow now explicitly
installs it from Microsoft's official download, verifying its pinned SHA-256 and
Microsoft Authenticode signature. This setup runs on the disposable GitHub host;
it does not install anything on the developer's PC. Checkout uses the pinned v6
action to avoid the retired Node 20 runtime.

When `publish_to_website` is checked, a separate post-compilation step sends the
verified files to the configured Cloudflare service. The service stores them in
the existing private `wherigo-builds` bucket, using the same 9 GB reservation
ledger and 24-hour expiry as ordinary jobs. The website gets its three links from
`GET /api/v1/demo`. Old demo links expire; rerun the manual workflow for a fresh
demonstration. The permanent source bucket is not used for these disposable files.

The publication credential is passed only to the publisher step, after cartridge
execution. It cannot claim visitor jobs, read source files, or write to published
source storage. No credentials are committed, exposed to the browser, or included
in the artifacts. Live visitor uploads remain disabled with SERVICE_ENABLED=false.

## Local verification

From the repository root:

```powershell
./service/demo/build-demo.ps1
node --test service/test/storage.test.js
```

From `service/`, `node --test test/*.test.js` also checks that the workflow is
manual-only, refuses private repositories, uses the standard Windows label,
pins its official actions, limits artifact retention and scopes publication
credentials to the post-compilation step.

The demo publisher was also verified end to end against Wrangler's real local
Worker/R2/Durable Object runtime, delivering all three locally compiled GWC
files while live uploads remained disabled. The test is
`service/test/local-demo-smoke.mjs`; start Wrangler with SERVICE_ENABLED=false
and the local-only DEMO_PUBLISH_TOKEN `local-test-demo` to repeat it. Never reuse
that test value in production.

The generated manifest/files are under ignored `service/demo/output/`. This Lua
is trusted project source; do not repurpose this direct execution script for
unreviewed uploaded cartridges.

## Activation checklist

Prepared locally, not yet activated on GitHub or Cloudflare:

1. Review and push the compiler changes to the public fork with the user's
   authorization. Existing project instructions prohibit silently pushing.
2. Manually run the workflow with publication **unchecked** and inspect its
   results and one-day artifact. It needs no R2 or other secret for this step.
3. Deploy the prepared Cloudflare Worker to the existing account/buckets. Keep
   SERVICE_ENABLED=false. Set ALLOWED_ORIGIN to the website's actual origin.
4. Configure a strong DEMO_PUBLISH_TOKEN secret in that Worker and the same secret
   in the fork's GitHub Actions settings. Set the GitHub repository variable
   COMPILER_SERVICE_URL to the deployed HTTPS Worker URL. Use secure secret entry;
   never paste the value into chat. These are not configured yet.
5. Run the workflow with publication checked. Set the website's compilerBaseUrl
   to the Worker URL, but leave features.compiler='sample' until a real visitor
   compiler host is available. The separate demonstration button uses the URL
   regardless of that flag. Publish website changes through its normal process.
6. Verify a public download from Test Compiler and open the GWC in a real player.

The gateway does not execute native code: after GitHub finishes its project test
run, the demonstration downloads need neither this PC nor an always-on compiler.
R2, Workers and Durable Objects retain their own separate usage allowances. The
9 GB cap is an application storage cap, not an account-wide zero-dollar guarantee.
