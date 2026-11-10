// Decimal bytes: leave headroom below R2's included 10 GB-month allowance.
export const CAP_BYTES = 9_000_000_000;
export const MAX_FILE_BYTES = 20_000_000;
export const RETENTION_MS = 24 * 60 * 60 * 1000;
export const DEVICES = ['Emulator', 'Garmin', 'iPad', 'iPhone', 'OpenWIG', 'PPC2003', 'WFPlayer', 'WhereYouGo', 'XMarksTheSpot'];
export class HttpError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}
export function assert(condition, status, message) {
  if (!condition) throw new HttpError(status, message);
}
export async function boundedBytes(request, maximum = MAX_FILE_BYTES) {
  const length = request.headers.get('content-length');
  assert(!length || (Number.isSafeInteger(Number(length)) && Number(length) <= maximum), 413, 'The file is too large. Maximum size is 20 MB.');
  const reader = request.body?.getReader();
  assert(reader, 400, 'Choose a cartridge file.');
  let size = 0;
  const chunks = [];
  try {
    while (true) {
      const {value, done} = await reader.read();
      if (done) break;
      size += value.byteLength;
      assert(size <= maximum, 413, 'The file is too large. Maximum size is 20 MB.');
      chunks.push(value);
    }
  } catch (error) { await reader.cancel().catch(() => {}); throw error; }
  const result = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) { result.set(chunk, offset); offset += chunk.byteLength; }
  return result;
}
export function sourceFile(bytes, filename) {
  assert(/\.gwz$/i.test(filename), 400, 'Choose a GWZ source package. Compiled GWC files cannot be recompiled.');
  assert(bytes.length >= 4 && bytes[0] === 0x50 && bytes[1] === 0x4b, 400, 'The source package is not a ZIP archive.');
}
export function gwcFile(bytes) {
  const signature = [2, 10, 67, 65, 82, 84, 0];
  assert(bytes.length > 9 && signature.every((n, i) => bytes[i] === n), 400, 'The compiler did not return a valid GWC header.');
}
export function selectedDevices(compile, download) {
  const devices = [...new Set(compile)];
  const downloads = [...new Set(download)];
  assert(devices.length > 0 && devices.every(d => DEVICES.includes(d)), 400, 'Select a supported device to compile for.');
  assert(downloads.every(d => devices.includes(d)), 400, 'Select Compile for every device you want to download.');
  return {devices, downloads};
}
export function reservation(sourceSize, downloads) {
  return sourceSize + downloads.length * MAX_FILE_BYTES;
}
