# Website compiler and R2 storage

For the approved free demonstration without hosting a compiler on this PC, see
[`demo/README.md`](demo/README.md). Its manual GitHub Actions project workflow
builds the approved sample and can publish temporary downloads through this
gateway while live visitor compilation remains disabled. The Windows-host
requirements below apply to the full arbitrary-upload compiler service.

## Current state

This checkout is connected to `forest-ghost/WF.Compiler` as origin and
`WFoundation/WF.Compiler` as upstream. No changes have been pushed.

Cloudflare account `9fc7410af12261185b658ea2294256d9` now has:

| Bucket | Class | Access | Retention |
| --- | --- | --- | --- |
| `wherigo-sources` | Standard | Private | Published sources retained |
| `wherigo-builds` | Standard | Private | All objects expire after one day |

The builds lifecycle rule `expire-test-files-after-24-hours` was verified enabled
in the dashboard. No source-expiration rule was added to the published bucket.
Do not put published originals in the builds bucket.

The service and website changes are prepared locally, **not deployed**. The
9 GB cap becomes active only when the service is deployed and every writer uses
it. Direct uploads via dashboard, API keys, or another service bypass accounting.
Keep both buckets exclusively managed by this service once enabled. Any future
additional R2 buckets in this account also consume the same free allowance.

## Architecture

The existing website keeps its catalog API settings. `compilerBaseUrl` can point
its Test Compiler requests at this separate Cloudflare Worker. The Build menu
already links to Test Compiler; the Build landing-page resource now does too.

The Worker accepts a GWZ and device choices, reserves storage, stores the upload,
and returns a random, unguessable job ID. That ID is a bearer capability for
checking results and downloading builds: do not publish it in analytics or logs.
Original test source packages are never available on a public download route.

A trusted Node runner pulls jobs over HTTPS. It passes each source into a
separate Windows container with no network, a non-admin user, 512 MB memory,
one CPU, and a 60-second timeout. Credentials stay in the host runner, outside
the container. Only that job's input/output directories are mounted. The runner
uploads genuine generated GWC files and structured device results.

Cloudflare Workers cannot run this native Windows/.NET compiler. A compatible
Windows container host is required. Docker is not installed on the current PC;
the container build and sandbox execution have **not** been verified here.
Do not expose the legacy compiler directly to the internet or run uploaded Lua
on the API host. The PC/server running the runner must remain online.

## Storage limits and cleanup

One SQLite-backed Durable Object serializes writes across both buckets. It
reserves the input size plus up to 20 MB for each requested downloadable output
before it writes any R2 objects. Its hard reservation limit is **9,000,000,000
bytes**, leaving headroom under the included storage allowance. Reservations
include uploads and builds in progress. They remain conservative until cleanup;
unused output capacity is not reused until the job expires.

Jobs and their download URLs expire 24 hours after acceptance. A Durable Object
alarm deletes their temporary source and builds, then releases their reservation
only after R2 confirms deletion. Failed/uncertain writes and failed deletion
remain counted. An uncertain published-source write remains reserved for manual
reconciliation. Neither automatic cleanup nor the R2 lifecycle touches published
source files. Cleanup is idempotent.

R2's lifecycle is backup cleanup, not an exact timer: physical deletion may occur
after the one-day expiry. The service refuses expired downloads immediately,
and never releases space based only on the nominal expiry timestamp.

The upload limit is 20 MB, expanded ZIP limit 128 MB, and entry limit 1,000.
Compiled output is capped at 20 MB per device. The gateway permits 20,000 public
compiler requests per UTC day, including polling/downloads. This limits abuse;
it does **not** guarantee a zero Cloudflare bill. Worker, Durable Object and R2
operations have separate allowances, and direct account activity is uncounted.

## Verify locally

No JavaScript dependencies are needed for these tests:

```powershell
cd service
node --test test/storage.test.js
```

Native tests build the x86 compiler as a self contained .NET 10 app and test
only self-authored trusted source. They generate actual GWC files for all nine
listed profiles and check the GWC header and embedded Lua 5.1 bytecode signature.
They also check Lua errors on the first/last lines. This is a smoke test, not
proof of full compatibility with real players, media, builders or save files.

```powershell
./service/test/native-smoke.ps1
```

The real local Worker runtime has also been tested with Wrangler's local R2 and
Durable Object bindings. To repeat that test after the native smoke test:

```powershell
# From service/, first terminal (test-only credentials; localhost only):
node node_modules/wrangler/bin/wrangler.js dev --local --ip 127.0.0.1 --port 8788 --var SERVICE_ENABLED:true --var RUNNER_TOKEN:local-test-runner --var WEBSITE_TOKEN:local-test-website
# Second terminal, also in service/:
node test/local-worker-smoke.mjs
```

This checks upload, assignment, source integrity, genuine GWC delivery and space
accounting through the actual Worker runtime. It uses a precompiled trusted GWC
fixture, not the unverified Docker runner. Never reuse its test tokens in production.
Wrangler's deployment dry-run also passes with both R2 bindings and the SQLite
coordinator. The pinned development tool and lockfile are included.

The missing `Eluant/BindingSupport.lua` resource was restored from its historical
upstream source; provenance/license is in `../THIRD_PARTY_NOTICES.md`. Lua error
line indexing was fixed. Compiler-time Lua access to OS commands, environment,
filesystem modules, debug and native library loading is restricted.

## Deployment still required

1. Select/provision the Windows container host. Check host/image compatibility,
   build `service/runner/Dockerfile` from the repository root, and verify isolation
   and timeouts before accepting untrusted uploads. No host has been selected yet.
2. Use official Wrangler tooling and authorize the intended Cloudflare account.
   `wrangler.jsonc` declares the existing buckets and the global coordinator.
   No paid event-notification queue is needed; the runner pulls jobs.
3. Store different strong secrets as `RUNNER_TOKEN` and `WEBSITE_TOKEN` via
   Wrangler's secret prompt or the Cloudflare secret UI. Never commit secrets,
   put them in browser code, or paste them into chat. The website token is for
   the authenticated website backend only; it is not needed for the public test
   form. This checkout intentionally contains no credentials.
4. Set `ALLOWED_ORIGIN` to the actual website origin. Deploy with
   `SERVICE_ENABLED=false` first. Run local/remote Worker integration checks
   against real bindings before enabling. Start the host runner with
   `COMPILER_SERVICE_URL` and `RUNNER_TOKEN` in its environment.
5. Once the runner is verified, set `SERVICE_ENABLED=true`, set the website's
   `compilerBaseUrl` to the returned HTTPS service URL, and switch only its
   `features.compiler` to `api`. Leave unrelated catalog/download flags alone.
6. Verify an upload, device results, a genuine download, unavailable-runner errors,
   expiry, cleanup and quota on the deployed service. Then publish website edits
   through its existing deployment process. Do not push without user permission.

## Endpoints

Browser API matches the existing website contract:

- `POST /api/v1/compile`: multipart `cartridge`, repeated `compile`, repeated
  `download`. Responds 202 `{jobId, expiresAt}`.
- `GET /api/v1/compile/{jobId}`: `{status, results, expiresAt}`. Queued is exposed
  as `running` for compatibility. A result includes device, ok, messages and
  optional downloadUrl.
- `GET /api/v1/compile/{jobId}/download/{device}`: private, non-cacheable GWC.
- `GET /api/v1/compile/devices`: profile identifiers.
- `GET /api/v1/health`: reports `compilerReady` only when enabled with a recent
  authenticated runner heartbeat. Uploads are refused before storing a file when
  the runner is unavailable.

Runner calls require `Authorization: Bearer RUNNER_TOKEN`; assigned-job calls
also require `X-Job-Lease` from the claim response:

- `POST /internal/runner/claim`: gets a queued job, or 204. Also heartbeats.
- `GET /internal/runner/{jobId}/source`: private source for the assigned job.
- `PUT /internal/runner/{jobId}/build/{device}`: binary GWC, one write per target.
- `POST /internal/runner/{jobId}/complete`: JSON `{results:[...]}`.

The future authenticated publication backend can retain validated GWZ sources
with `POST /internal/sources` (binary body, `X-Filename`, `X-Validated:true`,
WEBSITE_TOKEN). It must validate ownership and actual compiler validation before
calling this trusted endpoint. The source ID must be stored in the website's
database. `GET /internal/storage` reports reservedBytes and capBytes to that
backend. Author publication and on-demand builds of published sources are not
yet wired into the website's sample account/catalog implementation.
