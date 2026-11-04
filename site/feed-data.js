/* Shared limits for the public, same-origin status reports. */
(function () {
  'use strict';
  function fresh(data, now = Date.now()) {
    const age = now - Date.parse(data && data.updatedAt);
    return Number.isFinite(age) && age >= -30000 && age <= 120000;
  }
  async function read(path, maxBytes = 131072) {
    if (!/^\/data\/[a-zA-Z0-9/_.-]+\.json$/.test(path) || path.split('/').some(part => part === '.' || part === '..'))
      throw Error('Unexpected report path');
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 10000);
    let reader;
    try {
      const response = await fetch(path, {
        cache: 'no-store', credentials: 'omit', redirect: 'error', signal: controller.signal
      });
      if (!response.ok || !response.body) throw Error('Report unavailable');
      if (Number(response.headers.get('content-length')) > maxBytes) throw Error('Report too large');
      reader = response.body.getReader();
      const decoder = new TextDecoder('utf-8', { fatal: true });
      let size = 0, text = '';
      // Count bytes as they arrive. A missing Content-Length must not bypass the limit.
      for (;;) {
        const part = await reader.read();
        if (part.done) break;
        size += part.value.byteLength;
        if (size > maxBytes) throw Error('Report too large');
        text += decoder.decode(part.value, { stream: true });
      }
      return JSON.parse(text + decoder.decode());
    } finally {
      if (reader) { try { await reader.cancel(); } catch (_) {} }
      controller.abort();
      clearTimeout(timer);
    }
  }
  const api = { read, fresh };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else globalThis.SlithyFeed = api;
})();
