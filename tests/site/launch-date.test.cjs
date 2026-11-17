const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const read = name => fs.readFileSync(path.join(root, name), 'utf8');
const oldDate = /December\s+0?1,?\s+2026|2026-12-01|(?:12\/0?1\/2026)/i;

test('every main website page states the confirmed launch date', () => {
  const pages = fs.readdirSync(path.join(root, 'site')).filter(name => name.endsWith('.html'));
  assert.equal(pages.length, 15);
  for (const name of pages) {
    const html = read('site/' + name);
    assert.ok(html.includes('Live network launches January 1, 2027.'), name);
    assert.doesNotMatch(html, oldDate, name);
    assert.doesNotMatch(html, /Live network target|Testing may delay that date|Testing may require that date to change/i, name);
  }
});

test('press kit descriptions and fact sheet agree', () => {
  for (const name of ['index.html', 'README.txt', 'slithy-tove-facts.txt']) {
    const text = read('site/press-kit/' + name);
    assert.ok(text.includes('January 1, 2027'), name);
    assert.doesNotMatch(text, oldDate, name);
    assert.doesNotMatch(text, /subject to testing|Live network target/i, name);
  }
});

test('website and bundled terms carry the same dated revision', () => {
  const web = read('site/terms.html');
  const offline = read('licenses/BETA-TERMS.txt');
  const sentence = 'The live network launches on January 1, 2027. The exact start time will be announced before launch.';
  assert.ok(web.includes(sentence));
  assert.ok(offline.includes(sentence));
  assert.ok(web.includes('Version 1.1 &middot; Effective November 17, 2026'));
  assert.ok(offline.includes('Terms version: 1.1'));
  assert.ok(offline.includes('Version 1.1  |  Effective November 17, 2026'));
});

test('launch copy preserves the test-network reset warning', () => {
  for (const file of ['site/index.html', 'site/downloads.html', 'site/whitepaper.html', 'site/terms.html', 'site/press-kit/index.html']) {
    const text = read(file);
    assert.match(text, /reset/i, file);
    assert.match(text, /test (?:SLTHY|coins)|test network/i, file);
  }
});
