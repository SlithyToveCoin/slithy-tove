/* Fresh snapshots show that the node answered recently, not a live socket test. */
(function () {
  'use strict';
  const names = ['borogove', 'mome', 'rath'];
  const titles = { borogove: 'Borogove', mome: 'Mome', rath: 'Rath' };
  function classify(data, now) {
    const stamp = data && typeof data.updatedAt === 'string' ? Date.parse(data.updatedAt) : NaN;
    if (!Number.isFinite(stamp) || stamp > now + 30000 || now - stamp > 120000)
      return { state: 'unknown', label: 'Status unavailable', detail: 'No recent report. This does not confirm an outage.' };
    if (data.chain !== 'test' || typeof data.bestBlockHash !== 'string' || !/^[a-f0-9]{64}$/i.test(data.bestBlockHash) ||
        !Number.isSafeInteger(data.height) || data.height < 0 ||
        !Number.isSafeInteger(data.peerCount) || data.peerCount < 0 ||
        typeof data.initialBlockDownload !== 'boolean')
      return { state: 'unknown', label: 'Status unavailable', detail: 'The latest report could not be verified.' };
    return { state: data.initialBlockDownload ? 'syncing' : 'online',
      label: data.initialBlockDownload ? 'Online, syncing' : 'Online',
      detail: 'Block ' + data.height.toLocaleString() + ' / ' + data.peerCount + ' peer connections', stamp };
  }
  function summarize(snapshots, now) {
    const fresh = names.map(name => ({ name, data: snapshots[name] }))
      .filter(({ name, data }) => data && data.node === name && classify(data, now).state !== 'unknown');
    const sum = fresh.reduce((total, item) => total + item.data.peerCount, 0);
    const peers = fresh.length === names.length && Number.isSafeInteger(sum) ? sum : null;
    const synced = fresh.filter(({ data }) => !data.initialBlockDownload);
    // A disagreement at the same height is not a network-wide reading.
    const conflicting = synced.some((item, i) => synced.slice(i + 1).some(other =>
      item.data.height === other.data.height && item.data.bestBlockHash !== other.data.bestBlockHash));
    const candidates = synced.filter(({ data }) => data.networkHashrate &&
      Number.isFinite(data.networkHashrate.hashesPerSecond) && data.networkHashrate.hashesPerSecond >= 0 &&
      data.networkHashrate.windowBlocks === 120)
      .sort((a, b) => b.data.height - a.data.height || Date.parse(b.data.updatedAt) - Date.parse(a.data.updatedAt));
    // Each node estimates the same network. Pick one report, never add them.
    const source = conflicting ? null : candidates[0];
    return { peers, freshCount: fresh.length, hashrate: source ? source.data.networkHashrate.hashesPerSecond : null,
      source: source ? source.name : null, updatedAt: source ? source.data.updatedAt : null, conflicting };
  }
  function formatHashrate(value) {
    if (!Number.isFinite(value) || value < 0) return 'Unavailable';
    const units = ['H/s', 'kH/s', 'MH/s', 'GH/s', 'TH/s', 'PH/s', 'EH/s'];
    let unit = 0;
    while (value >= 1000 && unit < units.length - 1) { value /= 1000; unit++; }
    return value.toLocaleString(undefined, { maximumFractionDigits: 2 }) + ' ' + units[unit];
  }
  if (typeof module !== 'undefined' && module.exports) module.exports = { classify, summarize, formatHashrate };
  if (typeof document === 'undefined') return;
  const cards = Array.from(document.querySelectorAll('[data-node-status]'));
  const summaries = Array.from(document.querySelectorAll('[data-network-summary]'));
  if (!cards.length && !summaries.length) return;
  let snapshots = {}, busy = false, loaded = false;
  function set(parent, selector, text) {
    const element = parent.querySelector(selector);
    if (element && element.textContent !== text) element.textContent = text;
  }
  function time(stamp) {
    return new Date(stamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  function paint() {
    if (!loaded) return;
    const now = Date.now();
    for (const card of cards) {
      const name = card.dataset.nodeStatus;
      if (!names.includes(name)) continue;
      const data = snapshots[name];
      const status = classify(data && data.node === name ? data : null, now);
      card.dataset.state = status.state;
      set(card, '.node-status-label', status.label);
      set(card, '.node-status-detail', status.detail);
      set(card, '.node-status-time', status.stamp ? 'Reported ' + time(status.stamp) : 'Waiting for a fresh report');
    }
    const stats = summarize(snapshots, now);
    for (const summary of summaries) {
      set(summary, '[data-network-hashrate]', formatHashrate(stats.hashrate));
      set(summary, '[data-network-hashrate-note]', stats.source
        ? 'Last 120 blocks. Reported by ' + titles[stats.source] + ' at ' + time(stats.updatedAt) + '.'
        : stats.conflicting ? 'Waiting for the nodes to agree.' : 'Waiting for a fresh mining estimate.');
      set(summary, '[data-network-peers]', stats.peers === null ? 'Unavailable' : stats.peers.toLocaleString());
      set(summary, '[data-network-peers-note]', stats.peers === null
        ? stats.freshCount + ' of 3 nodes have a fresh report.' : 'Total across the three official nodes.');
    }
  }
  async function refresh() {
    if (busy) return;
    busy = true;
    try {
      const results = await Promise.all(names.map(async name => {
        try { return [name, await SlithyFeed.read('/data/nodes/' + name + '.json')]; }
        catch (_) { return [name, null]; }
      }));
      snapshots = Object.fromEntries(results);
      loaded = true;
    } finally { busy = false; paint(); }
  }
  refresh();
  setInterval(refresh, 60000);
  // Expire old readings even when the next request has not finished.
  setInterval(paint, 5000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) { paint(); refresh(); } });
})();
