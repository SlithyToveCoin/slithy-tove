const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const { classify, summarize, formatHashrate } = require('../../site/node-status.js');
const now = Date.parse('2026-11-12T21:00:00Z');
const names = ['borogove', 'mome', 'rath'];
const sample = (node, change = {}) => ({ node, updatedAt: new Date(now).toISOString(),
  chain: 'test', height: 7000, bestBlockHash: 'a'.repeat(64), initialBlockDownload: false,
  peerCount: 4, networkHashrate: { hashesPerSecond: 1250, windowBlocks: 120 }, ...change });
const samples = () => Object.fromEntries(names.map(name => [name, sample(name)]));

test('network summary adds peer connections but does not add hashrate estimates', () => {
  const result = summarize(samples(), now);
  assert.equal(result.peers, 12);
  assert.equal(result.hashrate, 1250);
  assert.equal(result.source, 'borogove');
  assert.equal(result.freshCount, 3);
});

test('network estimate comes from the highest synchronized report, then the newest', () => {
  const data = samples();
  data.mome.height++;
  data.mome.networkHashrate.hashesPerSecond = 2100;
  assert.equal(summarize(data, now).hashrate, 2100);
  data.mome.initialBlockDownload = true;
  data.rath.updatedAt = new Date(now + 1000).toISOString();
  assert.equal(summarize(data, now).source, 'rath');
  assert.equal(summarize(data, now).peers, 12);
});

test('hashrate becomes unavailable when synchronized nodes disagree at the same height', () => {
  const data = samples();
  data.rath.bestBlockHash = 'b'.repeat(64);
  assert.equal(summarize(data, now).hashrate, null);
  assert.equal(summarize(data, now).conflicting, true);
  assert.equal(summarize(data, now).peers, 12);
});

test('missing or expired reports cannot become a partial peer total', () => {
  for (const change of [null, { updatedAt: new Date(now - 120001).toISOString() },
    { updatedAt: new Date(now + 30001).toISOString() }, { updatedAt: 'yesterday' },
    { chain: 'main' }, { node: 'mome' }, { height: -1 }, { bestBlockHash: '<script>' },
    { bestBlockHash: ['a'.repeat(64)] }, { peerCount: -1 }, { peerCount: 1.5 },
    { peerCount: '4' }, { initialBlockDownload: null }]) {
    const data = samples();
    data.borogove = change ? sample('borogove', change) : null;
    const result = summarize(data, now);
    assert.equal(result.peers, null);
    assert.equal(result.freshCount, 2);
    assert.equal(result.hashrate, 1250);
  }
});

test('zero hashrate and zero connections are valid readings', () => {
  const data = samples();
  for (const value of Object.values(data)) {
    value.peerCount = 0;
    value.networkHashrate.hashesPerSecond = 0;
  }
  assert.equal(summarize(data, now).peers, 0);
  assert.equal(summarize(data, now).hashrate, 0);
  assert.equal(formatHashrate(0), '0 H/s');
});

test('malformed hashrate never replaces a missing estimate with zero', () => {
  for (const hashrate of [undefined, null, {}, { hashesPerSecond: null, windowBlocks: 120 },
    { hashesPerSecond: '20', windowBlocks: 120 }, { hashesPerSecond: -1, windowBlocks: 120 },
    { hashesPerSecond: NaN, windowBlocks: 120 }, { hashesPerSecond: Infinity, windowBlocks: 120 },
    { hashesPerSecond: true, windowBlocks: 120 }, { hashesPerSecond: 2, windowBlocks: 20 }]) {
    const data = samples();
    for (const value of Object.values(data)) value.networkHashrate = hashrate;
    assert.equal(summarize(data, now).hashrate, null);
    assert.equal(summarize(data, now).peers, 12);
  }
});

test('syncing reports do not supply a network estimate', () => {
  const data = samples();
  for (const value of Object.values(data)) value.initialBlockDownload = true;
  assert.equal(summarize(data, now).hashrate, null);
  assert.equal(summarize(data, now).peers, 12);
});

test('peer totals cannot overflow and stale data expires at the advertised boundary', () => {
  const data = samples();
  data.rath.peerCount = Number.MAX_SAFE_INTEGER;
  assert.equal(summarize(data, now).peers, null);
  assert.equal(classify(sample('borogove'), now + 120000).state, 'online');
  assert.equal(classify(sample('borogove'), now + 120001).state, 'unknown');
});

test('hashrate units are readable and invalid values are unavailable', () => {
  assert.equal(formatHashrate(1250), '1.25 kH/s');
  assert.equal(formatHashrate(2e6), '2 MH/s');
  assert.equal(formatHashrate(3e9), '3 GH/s');
  for (const value of [null, undefined, NaN, Infinity, -1, '5'])
    assert.equal(formatHashrate(value), 'Unavailable');
});

test('summary expires in an open tab and unchanged polls do not rewrite its text', async () => {
  const elements = new Map();
  let writes = 0;
  const summary = { querySelector(selector) {
    if (!elements.has(selector)) {
      let content = '';
      elements.set(selector, { get textContent() { return content; },
        set textContent(value) { content = value; writes++; } });
    }
    return elements.get(selector);
  } };
  const intervals = [];
  let clock = now;
  class TestDate extends Date { static now() { return clock; } }
  const context = { Date: TestDate, document: {
    querySelectorAll: selector => selector === '[data-network-summary]' ? [summary] : [],
    addEventListener() {}
  }, SlithyFeed: { read: async uri => sample(uri.match(/\/([^/]+)\.json$/)[1]) },
  setInterval: (fn, delay) => { intervals.push({ fn, delay }); } };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../site/node-status.js'), 'utf8'), context);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(elements.get('[data-network-peers]').textContent, '12');
  assert.equal(elements.get('[data-network-hashrate]').textContent, '1.25 kH/s');
  const initialWrites = writes;
  intervals.find(item => item.delay === 5000).fn();
  assert.equal(writes, initialWrites);
  clock += 120001;
  intervals.find(item => item.delay === 5000).fn();
  assert.equal(elements.get('[data-network-peers]').textContent, 'Unavailable');
  assert.equal(elements.get('[data-network-hashrate]').textContent, 'Unavailable');
});

test('a failed node fetch removes the total but leaves a fresh estimate available', async () => {
  const elements = new Map();
  const summary = { querySelector(selector) {
    if (!elements.has(selector)) elements.set(selector, { textContent: '' });
    return elements.get(selector);
  } };
  const paths = [];
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../site/node-status.js'), 'utf8'), {
    document: { querySelectorAll: selector => selector === '[data-network-summary]' ? [summary] : [], addEventListener() {} },
    SlithyFeed: { read: async uri => {
      paths.push(uri);
      if (uri.includes('mome')) throw Error('offline');
      return sample(uri.match(/\/([^/]+)\.json$/)[1], { updatedAt: new Date().toISOString() });
    } }, setInterval() {}
  });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(elements.get('[data-network-peers]').textContent, 'Unavailable');
  assert.equal(elements.get('[data-network-peers-note]').textContent, '2 of 3 nodes have a fresh report.');
  assert.equal(elements.get('[data-network-hashrate]').textContent, '1.25 kH/s');
  assert.deepEqual(paths.sort(), names.map(name => '/data/nodes/' + name + '.json').sort());
});

test('home and explorer use short labels and load the shared feed helper first', () => {
  for (const page of ['index', 'home-full', 'explorer']) {
    const html = fs.readFileSync(path.join(__dirname, '../../site', page + '.html'), 'utf8');
    assert.match(html, /<h3>Estimated hashrate<\/h3>/);
    assert.match(html, /<h3>Peer connections<\/h3>/);
    assert.equal((html.match(/data-network-summary/g) || []).length, 1);
    assert.equal((html.match(/data-network-hashrate role="status"/g) || []).length, 1);
    assert.equal((html.match(/data-network-peers role="status"/g) || []).length, 1);
    assert.ok(html.indexOf('src="/feed-data.js') < html.indexOf('src="/node-status.js'));
    assert.match(html, /Connections are not individual people/);
  }
});
