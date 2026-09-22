/* Fresh snapshots show that the node answered recently, not a live socket test. */
(function () {
  'use strict';
  function classify(data, now) {
    const stamp = Date.parse(data && data.updatedAt);
    if (!Number.isFinite(stamp) || stamp > now + 30000 || now - stamp > 120000)
      return { state: 'unknown', label: 'Status unavailable', detail: 'No recent report. This does not confirm an outage.' };
    if (data.chain !== 'test' || !/^[a-f0-9]{64}$/i.test(data.bestBlockHash || '') ||
        !Number.isSafeInteger(data.height) || data.height < 0 ||
        !Number.isSafeInteger(data.peerCount) || data.peerCount < 0 ||
        typeof data.initialBlockDownload !== 'boolean')
      return { state: 'unknown', label: 'Status unavailable', detail: 'The latest report could not be verified.' };
    return { state: data.initialBlockDownload ? 'syncing' : 'online',
      label: data.initialBlockDownload ? 'Online, syncing' : 'Online',
      detail: 'Block ' + data.height.toLocaleString() + ' / ' + data.peerCount + ' peer connections', stamp };
  }
  if (typeof module !== 'undefined' && module.exports) module.exports = { classify };
  if (typeof document === 'undefined') return;
  document.querySelectorAll('[data-node-status]').forEach(card => {
    const name = card.dataset.nodeStatus;
    if (!['borogove', 'mome', 'rath'].includes(name)) return;
    let snapshot = null;
    let busy = false;
    function paint() {
      const status = classify(snapshot, Date.now());
      card.dataset.state = status.state;
      card.querySelector('.node-status-label').textContent = status.label;
      card.querySelector('.node-status-detail').textContent = status.detail;
      card.querySelector('.node-status-time').textContent = status.stamp
        ? 'Reported ' + new Date(status.stamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        : 'Waiting for a fresh report';
    }
    async function refresh() {
      if (busy) return;
      busy = true;
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 8000);
      try {
        const response = await fetch('/data/nodes/' + name + '.json', {
          cache: 'no-store', signal: controller.signal, redirect: 'error'
        });
        if (!response.ok) throw new Error('No node report');
        const text = await response.text();
        if (text.length > 131072) throw new Error('Report too large');
        snapshot = JSON.parse(text);
      } catch (_) { snapshot = null; }
      finally { clearTimeout(timeout); busy = false; paint(); }
    }
    refresh();
    setInterval(refresh, 60000);
    // Expire old readings even when the next request has not finished.
    setInterval(paint, 5000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) { paint(); refresh(); } });
  });
})();
