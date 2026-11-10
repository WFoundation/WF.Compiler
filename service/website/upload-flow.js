import {title, link, esc} from './ui.js?v=20261109upload1';
import {isApi} from './config.js?v=20261109upload1';
import {startUploadValidation, uploadValidationStatus, publishValidatedCartridge} from './api.js?v=20261109upload1';

const UPLOAD = 'wherigo.upload.v1';
const ERROR = 'wherigo.upload.error.v1';
const DRAFT = 'wherigo.upload.details.v1';
const uuid = value => typeof value === 'string' && /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(value);
const read = key => { try { return JSON.parse(sessionStorage.getItem(key) || 'null'); } catch { return null; } };
const write = (key, value) => { try { sessionStorage.setItem(key, JSON.stringify(value)); return true; } catch { return false; } };
const openSource = () => new URLSearchParams(location.search).has('opensource');
const route = (path, id) => {
  const params = new URLSearchParams();
  if (id) params.set('upload', id);
  if (openSource()) params.set('opensource', '1');
  return path + (params.size ? '?' + params : '');
};
const go = path => location.assign(window.__WG_FIX__ ? window.__WG_FIX__(path) : path);
const savedUpload = () => { const value = read(UPLOAD); return value && uuid(value.uploadId) && value.expiresAt > Date.now() ? value : null; };
const requestedUpload = () => {
  const query = new URLSearchParams(location.search).get('upload');
  return uuid(query) ? query : savedUpload()?.uploadId;
};
const messages = values => (Array.isArray(values) ? values : []).slice(0, 12).map(value => String(value).slice(0, 3000));

export function renderUploadStart() {
  const pending = savedUpload();
  return title('Upload a Cartridge', 'First, check your cartridge file.') +
    `<div class="os-page upload-check-page"><p class="eyebrow">Step 1 of 2 · Cartridge file</p>
    <p>Choose the GWZ source package exported by your Wherigo builder. We’ll check its Lua code and make sure its referenced media files are included before you add the listing details.</p>
    <form class="os-upload" data-upload-check novalidate>
      <div class="form-fields"><div class="full"><label for="check-cartridge">Wherigo cartridge file</label>
      <input id="check-cartridge" name="cartridge" type="file" accept=".gwz" aria-describedby="check-file-help" required>
      <p id="check-file-help" class="os-help">GWZ files only, up to 20 MB. A compiled GWC cannot be used as the source. Unpublished uploads expire after 24 hours.</p></div></div>
      <div class="toolbar"><button class="button" type="submit">Upload and check</button>${link('/profile/', 'Cancel', 'text-link')}</div>
      <p data-upload-check-status role="status" aria-live="polite" hidden></p>
      <button type="button" class="button" data-resume-upload${pending?.status === 'checking' ? '' : ' hidden'}>Continue checking previous upload</button>
    </form></div>`;
}

export function renderUploadNotice() {
  const id = requestedUpload();
  return `<p class="eyebrow">Step 2 of 2 · Cartridge details</p>
    <div class="upload-validation-notice" data-upload-notice role="status" aria-live="polite"><p>${id ? 'Confirming your cartridge’s validation result…' : 'Upload and check your cartridge before publishing this listing.'}</p>
    ${link(route('/upload/'), id ? 'Choose a different cartridge' : 'Upload and check a GWZ', 'text-link')}</div>`;
}

function diagnostics(errors) {
  return `<div class="upload-diagnostics"><h2>What needs fixing</h2>${errors.length ? `<ul>${errors.map(message => `<li><pre>${esc(message)}</pre></li>`).join('')}</ul>` : '<p>No detailed error report is available. Upload the GWZ again to get a fresh check.</p>'}</div>`;
}

export function renderUploadError() {
  const error = read(ERROR);
  const id = new URLSearchParams(location.search).get('upload');
  const usable = error && error.expiresAt > Date.now() && (!id || error.uploadId === id);
  return title('Cartridge Check Failed', 'Your cartridge has not been published.') +
    `<div class="os-page upload-error-page"><p>We couldn’t validate ${usable && error.filename ? `<strong>${esc(error.filename)}</strong>` : 'this cartridge'}. Correct the reported issues in your builder, export a new GWZ, and try again.</p>
    <div data-upload-errors>${diagnostics(usable ? messages(error.messages) : [])}</div>
    <div class="toolbar">${link(route('/upload/'), 'Choose a corrected GWZ', 'button')}${link('/cartridge-testing/', 'Cartridge testing help', 'text-link')}</div>
    <p class="os-help">A successful check confirms that the file can be processed. Test gameplay in a Wherigo player before publishing.</p></div>`;
}

function fail(filename, errors, uploadId, expiresAt = Date.now() + 86400000) {
  write(ERROR, {filename, messages: messages(errors), uploadId, expiresAt});
  if (uploadId) write(UPLOAD, {uploadId, filename, expiresAt, status: 'invalid'});
  go(route('/upload/error/', uploadId));
}

async function waitForValidation(form, upload) {
  const status = form.querySelector('[data-upload-check-status]');
  status.textContent = 'Checking the Lua code and referenced media files…';
  for (let attempt = 0; attempt < 120; attempt++) {
    const result = await uploadValidationStatus(upload.uploadId);
    if (result.status === 'valid') {
      write(UPLOAD, {...upload, ...result});
      go(route('/upload/new/', upload.uploadId));
      return;
    }
    if (result.status === 'invalid') {
      fail(upload.filename, result.messages, upload.uploadId, result.expiresAt);
      return;
    }
    await new Promise(resolve => setTimeout(resolve, 1500));
  }
  status.textContent = 'The check is still running. Use “Continue checking previous upload” to check again.';
}

function detailValues(form) {
  const data = new FormData(form);
  return {
    title: String(data.get('title') || '').trim(), description: String(data.get('description') || '').trim(),
    country: String(data.get('country') || '').trim(), region: String(data.get('region') || '').trim(),
    time: String(data.get('time') || ''), coordinates: String(data.get('coordinates') || '').trim(),
    starting: String(data.get('starting') || '').trim(), attributes: data.getAll('attribute'),
    openSource: data.has('opensource'), builders: data.getAll('builder'),
    license: String(data.get('license') || ''), permission: data.has('permission')
  };
}

function saveDetails(form, id) { return write(DRAFT, {uploadId: id || null, values: detailValues(form)}); }

function restoreDetails(form, id) {
  const draft = read(DRAFT);
  if (!draft || draft.uploadId !== (id || null)) return;
  for (const [name, value] of Object.entries(draft.values || {})) {
    const field = {attributes: 'attribute', openSource: 'opensource', builders: 'builder'}[name] || name;
    for (const input of form.querySelectorAll(`[name="${field}"]`)) {
      if (input.type === 'checkbox') input.checked = Array.isArray(value) ? value.includes(input.value) : Boolean(value);
      else input.value = value;
    }
  }
}

export async function installUploadFlow(page) {
  if (page === 'upload/error') {
    const id = new URLSearchParams(location.search).get('upload');
    if (uuid(id) && isApi('compiler')) {
      try {
        const result = await uploadValidationStatus(id);
        if (result.status === 'invalid') document.querySelector('[data-upload-errors]').innerHTML = diagnostics(messages(result.messages));
      } catch { /* Preserve the last known diagnostic rather than losing it on a network failure. */ }
    }
    return;
  }
  if (page !== 'upload/new') return;
  const form = document.querySelector('[data-publish-cartridge]');
  const id = requestedUpload();
  restoreDetails(form, id);
  form.addEventListener('input', () => saveDetails(form, id));
  form.addEventListener('change', () => saveDetails(form, id));
  const notice = document.querySelector('[data-upload-notice]');
  if (!id || !isApi('compiler')) {
    notice.innerHTML = `<p>${id ? 'The cartridge checker is currently unavailable. We must confirm your upload before it can be published.' : 'Upload and check your cartridge before publishing this listing.'}</p>${link(route('/upload/'), 'Upload and check a GWZ', 'text-link')}`;
    return;
  }
  try {
    const result = await uploadValidationStatus(id);
    if (result.status === 'invalid') { fail(result.filename, result.messages, id, result.expiresAt); return; }
    if (result.status !== 'valid' || !(result.expiresAt > Date.now())) {
      notice.innerHTML = `<p>This cartridge has not finished checking. Complete its check before publishing.</p>${link(route('/upload/'), 'Return to cartridge check', 'text-link')}`;
      return;
    }
    form.dataset.validatedUpload = id;
    form.dataset.validationExpires = String(result.expiresAt);
    form.querySelector('[data-publish-button]').disabled = false;
    notice.innerHTML = `<p><strong>Cartridge check passed.</strong> ${esc(result.filename || 'Your GWZ')} is ready for listing details.</p>${link(route('/upload/'), 'Choose a different cartridge', 'text-link')}`;
  } catch (error) {
    notice.innerHTML = `<p>${esc(error.message)}</p>${link(route('/upload/'), 'Return to cartridge check', 'text-link')}`;
  }
}

document.addEventListener('submit', async event => {
  const form = event.target.closest?.('[data-upload-check], [data-publish-cartridge]');
  if (!form) return;
  event.preventDefault();
  if (form.dataset.busy) return;
  if (form.matches('[data-upload-check]')) {
    const status = form.querySelector('[data-upload-check-status]');
    status.hidden = false;
    const file = form.querySelector('input[type=file]').files[0];
    if (!file) { status.textContent = 'Choose your GWZ source package first.'; return; }
    if (!/\.gwz$/i.test(file.name)) { fail(file.name, ['Choose the GWZ source package exported by your builder. A GWC or other file type cannot be used as the source.']); return; }
    if (file.size > 20000000) { fail(file.name, ['The GWZ is larger than the 20 MB upload limit. Reduce its media sizes and export it again.']); return; }
    if (!isApi('compiler')) { status.textContent = 'The cartridge checker is currently unavailable. Your cartridge has not been checked. Please try again later.'; return; }
    const button = form.querySelector('button[type=submit]');
    form.dataset.busy = 'true'; button.disabled = true;
    status.textContent = 'Uploading your cartridge for checking…';
    try {
      const result = await startUploadValidation(file);
      if (!uuid(result.uploadId)) throw new Error('The checker did not return an upload reference. Please try again.');
      const upload = {uploadId: result.uploadId, filename: file.name, expiresAt: result.expiresAt, status: 'checking'};
      write(UPLOAD, upload);
      await waitForValidation(form, upload);
    } catch (error) {
      if (error.status === 400 || error.status === 413 || error.status === 422) fail(file.name, [error.message]);
      else status.textContent = error.message;
    } finally {
      delete form.dataset.busy; button.disabled = false;
      form.querySelector('[data-resume-upload]').hidden = savedUpload()?.status !== 'checking';
    }
    return;
  }
  const status = form.querySelector('[data-publish-status]');
  status.hidden = false;
  const id = form.dataset.validatedUpload;
  if (!uuid(id) || !(Number(form.dataset.validationExpires) > Date.now())) {
    status.textContent = 'Upload and check your cartridge before publishing. If its check has expired, upload it again.';
    form.querySelector('[data-publish-button]').disabled = true;
    return;
  }
  if (!form.reportValidity()) return;
  const details = detailValues(form);
  if (details.openSource && (!details.permission || !details.builders.length || details.license === 'Choose a license')) {
    status.textContent = 'For open source sharing, choose a builder and license, and confirm that you have permission to release the source.';
    return;
  }
  const saved = saveDetails(form, id);
  if (!isApi('upload')) { status.textContent = 'Publishing is currently unavailable.' + (saved ? ' Your details are saved in this tab. Please try again later.' : ' Please keep this page open and try again later.'); return; }
  const button = form.querySelector('[data-publish-button]');
  form.dataset.busy = 'true'; button.disabled = true;
  status.textContent = 'Publishing your cartridge…';
  try {
    const validation = await uploadValidationStatus(id);
    if (validation.status !== 'valid') throw new Error('This upload must pass its cartridge check before publishing.');
    const result = await publishValidatedCartridge(id, details);
    if (!uuid(result.id) || result.status !== 'published') throw new Error('The server did not confirm publication. Your listing has not been marked as published.');
    write(DRAFT, null); write(UPLOAD, null);
    go('/cartridge/index.html?CGUID=' + encodeURIComponent(result.id));
  } catch (error) { status.textContent = error.message; }
  finally { delete form.dataset.busy; button.disabled = false; }
});

document.addEventListener('click', async event => {
  const button = event.target.closest?.('[data-resume-upload]');
  if (!button) return;
  const form = button.closest('form');
  if (form.dataset.busy) return;
  const upload = savedUpload();
  const status = form.querySelector('[data-upload-check-status]');
  status.hidden = false;
  if (!upload || !isApi('compiler')) { status.textContent = 'This upload is no longer available to check. Choose your GWZ and try again later.'; return; }
  form.dataset.busy = 'true'; button.disabled = true;
  try { await waitForValidation(form, upload); }
  catch (error) { status.textContent = error.message; }
  finally { delete form.dataset.busy; button.disabled = false; }
});
