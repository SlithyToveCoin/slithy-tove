const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const script = fs.readFileSync(path.join(__dirname, '../../site/explorer.js'), 'utf8');
function searchValue(query) {
  const search = { value: '', addEventListener() {} };
  vm.runInNewContext(script, {
    document: { querySelector: () => search, addEventListener() {} },
    window: { location: { search: query } },
    URLSearchParams, AbortController,
    SlithyFeed: { read: () => new Promise(() => {}), fresh: () => true },
    fetch: () => new Promise(() => {}),
    setTimeout: () => 0, clearTimeout() {}, setInterval() {}
  });
  return search.value;
}
const transaction = 'a1'.repeat(32);
assert.equal(searchValue('?tx=' + transaction), transaction);
assert.equal(searchValue('?tx=' + transaction.toUpperCase()), transaction);
for (const query of ['', '?tx=short', '?tx=' + 'g'.repeat(64), '?tx=%3Cscript%3E'])
  assert.equal(searchValue(query), '');
console.log('PASS: 6 explorer transaction-link checks; no network requests sent.');
