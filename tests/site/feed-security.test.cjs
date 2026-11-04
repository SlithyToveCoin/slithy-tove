const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const root = path.join(__dirname, '../..');
const script = name => fs.readFileSync(path.join(root, 'site', name + '.js'), 'utf8');
const feed = require('../../site/feed-data.js');

function load(name, extras = {}) {
  const context = { module: { exports: {} }, SlithyFeed: feed, ...extras };
  vm.runInNewContext(script(name), context);
  return context.module.exports;
}
const treasury = load('treasury');
const explorer = load('explorer');
const sample = () => ({
  updatedAt: new Date().toISOString(), chain: 'test', network: 'slithy-testnet',
  chainHeight: 100, height: 100, peerCount: 2, bestBlockHash: 'a'.repeat(64),
  treasuryAddress: 'tslithy1quwm9s9qq9k03vdlus547p2x69uqvh50v3wffcu',
  currentBalance: '100.00000000', unlockedBalance: '1.00000000', blocks: []
});
test('treasury accepts current beta data and a missing spendable reading', () => {
  assert.equal(treasury.validate(sample()), true);
  assert.equal(treasury.validate({ ...sample(), unlockedBalance: null }), true);
});
test('treasury rejects stale, wrong-network, malformed and missing balances', () => {
  for (const change of [
    { updatedAt: new Date(Date.now() - 121000).toISOString() },
    { updatedAt: new Date(Date.now() + 31000).toISOString() },
    { updatedAt: 'yesterday' }, { chain: 'main' }, { network: 'other' },
    { chainHeight: -1 }, { bestBlockHash: 'bad' }, { treasuryAddress: '<script>' },
    { treasuryAddress: 'tslithy1qja33mzq8g457mp6tp4pmya2vr4auag48t9xp5p' },
    { currentBalance: null, accruedRewards: '100.00000000' },
    { currentBalance: '-1' }, { currentBalance: 'Infinity' }, { currentBalance: 100 },
    { unlockedBalance: undefined }
  ]) assert.equal(treasury.validate({ ...sample(), ...change }), false);
});
test('explorer validates nested data before drawing a partial report', () => {
  const block = { height: 100, hash: 'b'.repeat(64), time: 1700000000,
    confirmations: 1, transactionCount: 1, difficulty: 0.001,
    minerSubsidyEstimate: '9.00000000', treasurySubsidyEstimate: '1.00000000',
    transactions: [{ txid: 'c'.repeat(64), coinbase: true, inputCount: 1, outputCount: 1,
      inputs: [{ coinbase: true }], outputs: [{ n: 0, address: null,
        type: 'nulldata', value: '0.00000000', treasury: false }] }] };
  assert.equal(explorer.validate({ ...sample(), blocks: [block] }), true);
  for (const change of [{ blocks: Array(26).fill(block) }, { peerCount: -1 },
    { blocks: [{ ...block, transactions: [{ txid: 'x' }] }] },
    { blocks: [{ ...block, height: 101 }] }])
    assert.equal(explorer.validate({ ...sample(), ...change }), false);
});
function readerFor(parts, declared = null) {
  let canceled = false;
  return {
    response: { ok: true, headers: { get: () => declared }, body: { getReader: () => ({
      read: async () => parts.length ? { done: false, value: parts.shift() } : { done: true },
      cancel: async () => { canceled = true; }
    }) } },
    canceled: () => canceled
  };
}
async function readFixture(fixture, max = 100) {
  let options;
  const module = load('feed-data', {
    AbortController, TextDecoder, setTimeout, clearTimeout,
    fetch: async (_url, settings) => { options = settings; return fixture.response; }
  });
  const value = await module.read('/data/example.json', max);
  return { value, options };
}
test('feed fetch rejects redirects and omits cookies, including after completion', async () => {
  const fixture = readerFor([Buffer.from('{"value":1}')]);
  const { value, options } = await readFixture(fixture);
  assert.equal(value.value, 1);
  assert.equal(options.redirect, 'error');
  assert.equal(options.credentials, 'omit');
  assert.equal(options.signal.aborted, true);
  assert.equal(fixture.canceled(), true);
});
test('feed size limit applies while reading, without Content-Length', async () => {
  const fixture = readerFor([Buffer.alloc(5), Buffer.alloc(6)]);
  await assert.rejects(readFixture(fixture, 10), /Report too large/);
  assert.equal(fixture.canceled(), true);
});
test('feed rejects declared oversize, malformed UTF-8 and invalid JSON', async () => {
  await assert.rejects(readFixture(readerFor([], '101')), /Report too large/);
  await assert.rejects(readFixture(readerFor([Uint8Array.from([0xff])])));
  await assert.rejects(readFixture(readerFor([Buffer.from('not-json')])));
  await assert.rejects(feed.read('https://elsewhere.example/report.json'), /Unexpected report path/);
  await assert.rejects(feed.read('/data/../private.json'), /Unexpected report path/);
  await assert.rejects(feed.read('/data/./report.json'), /Unexpected report path/);
});
test('treasury clears displayed balances when a report expires in an open tab', async () => {
  const elements = new Map();
  const intervals = [];
  let current = sample();
  load('treasury', {
    document: { querySelector: name => {
      if (!elements.has(name)) elements.set(name, { textContent: '' });
      return elements.get(name);
    }, addEventListener() {} },
    SlithyFeed: { read: async () => current, fresh: feed.fresh },
    setInterval: (fn, delay) => intervals.push({ fn, delay })
  });
  await new Promise(resolve => setImmediate(resolve));
  assert.match(elements.get('[data-treasury-amount]').textContent, /100 SLTHY/);
  current.updatedAt = new Date(Date.now() - 121000).toISOString();
  intervals.find(x => x.delay === 5000).fn();
  assert.equal(elements.get('[data-treasury-amount]').textContent, 'Status temporarily unavailable');
  assert.equal(elements.get('[data-treasury-unlocked]').textContent, 'Spendable balance unavailable');
});
test('explorer marks expired counters unavailable without deleting old transaction details', async () => {
  const elements = new Map(), intervals = [], current = sample();
  const element = () => ({ textContent: '', value: '', children: [],
    append(child) { this.children.push(child); },
    replaceChildren() { this.children = []; },
    querySelectorAll: () => [], addEventListener() {} });
  load('explorer', {
    document: { querySelector: name => {
      if (!elements.has(name)) elements.set(name, element());
      return elements.get(name);
    }, createElement: element, addEventListener() {} },
    window: { location: { search: '' } }, URLSearchParams,
    SlithyFeed: { read: async () => current, fresh: feed.fresh },
    setInterval: (fn, delay) => intervals.push({ fn, delay })
  });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(elements.get('[data-explorer-height]').textContent, 100);
  current.updatedAt = new Date(Date.now() - 121000).toISOString();
  intervals.find(x => x.delay === 5000).fn();
  assert.equal(elements.get('[data-explorer-height]').textContent, 'Unavailable');
  assert.match(elements.get('[data-explorer-notice]').textContent, /may be out of date/);
});
test('feed timeout covers a stalled response body', async () => {
  let canceled = false;
  const module = load('feed-data', {
    AbortController, TextDecoder, clearTimeout, setTimeout: fn => setTimeout(fn, 5),
    fetch: async (_url, { signal }) => ({ ok: true, headers: { get: () => null }, body: {
      getReader: () => ({
        read: () => new Promise((resolve, reject) => {
          signal.addEventListener('abort', () => reject(Error('body aborted')), { once: true });
        }), cancel: async () => { canceled = true; }
      })
    } })
  });
  await assert.rejects(module.read('/data/stalled.json'), /body aborted/);
  assert.equal(canceled, true);
});
test('feed helper loads before each page that uses it', () => {
  for (const page of ['index', 'home-full', 'treasury', 'explorer']) {
    const html = fs.readFileSync(path.join(root, 'site', page + '.html'), 'utf8');
    const helper = html.indexOf('src="/feed-data.js');
    assert.ok(helper >= 0);
    for (const name of ['treasury', 'node-status', 'explorer']) {
      const consumer = html.indexOf('src="/' + name + '.js');
      if (consumer >= 0) assert.ok(helper < consumer);
    }
  }
});
