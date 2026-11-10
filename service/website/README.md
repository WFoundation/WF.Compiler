# Website upload integration

The existing static website checkout is separate from this compiler repository.
The scripts here are one-time, guarded edits used to prepare that checkout;
they are not runtime migrations and should not be rerun on an updated website.
`upload-flow.js` is the prepared source for the website's corresponding module.

## Current flow

- `/upload/`: choose a GWZ, then upload and check. File extensions/size are checked
  locally. The backend validates Lua and referenced media; a ZIP signature alone
  never means the cartridge passed.
- `/upload/new/index.html`: existing listing-details form. The file chooser is
  removed and the bottom action is Publish. The page rechecks the upload reference
  with the backend before enabling that action. A direct URL or local browser
  state cannot establish server-side validation.
- `/upload/error/`: shows specific compiler diagnostics, including Lua line
  context or missing-media messages, with a corrected-file retry action. Messages
  are escaped as text. An unavailable service is reported on the first page and
  does not claim the cartridge itself is invalid.

Both open-source entry links and the Build menu start at `/upload/`. Draft details
are retained in sessionStorage when available; no selected file is stored there.
Unpublished upload references expire after 24 hours.

## Backend contract

The website's `compilerBaseUrl` and `features.compiler='api'` enable these calls:

- `POST /api/v1/uploads/validate`: multipart `cartridge`; returns 202 with
  `{uploadId, expiresAt}`. The prepared Worker uses the real compiler runner to
  check the source by compiling an Emulator build without retaining a download.
- `GET /api/v1/uploads/{uploadId}`: `{status:'checking'|'valid'|'invalid', filename,
  expiresAt, messages}`. The native adapter supplies actual compiler diagnostics.

Publication uses the separate website/account backend, enabled by
`features.upload='api'`:

- `POST /api/v1/cartridges`: JSON `{uploadId, title, description, country, region,
  time, coordinates, starting, attributes, openSource, builders, license, permission}`.
  On successful publication the backend returns `{id, status:'published'}`.
- That backend must authenticate the author, bind ownership of the upload, verify
  metadata/source-sharing permission, and call the Worker's authenticated
  `POST /internal/uploads/{uploadId}/publish` to retain the validated original.
  This endpoint refuses unchecked, failed, expired, ordinary test and demo jobs.
  It returns an idempotent sourceId and reserves permanent source bytes before
  the R2 copy. The backend then creates the cartridge listing transactionally.

The account/catalog publication backend is not implemented by this static website
or Worker. Neither a button label nor a client-side pass flag publishes a record.
Without the live checker or account backend, the page reports unavailability;
it never fabricates validation or publication success.

## Battleship demonstration

The author supplied a private Battleship GWZ. It was compiled locally into genuine
iPhone, iPad and WhereYouGo GWC files, and those compiled files were copied into
the website's `downloads/battleship/` directory. Its adapted cartridge ID is
`654c6417-e3e4-4ede-8514-e4c12e5050d4`; the original catalog entry has a different ID
and remains unchanged. The original GWZ, embedded Lua and Urwigo builder project
were not copied into either public repository.

These fixed website downloads need neither R2 nor a live compiler. Their existence
does not enable arbitrary website uploads. GWC structure, embedded Lua bytecode,
object bounds and download hashes were checked; gameplay still needs a phone test.
