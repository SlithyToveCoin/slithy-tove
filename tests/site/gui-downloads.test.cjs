const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const site = path.join(__dirname, '../../site');
const page = name => fs.readFileSync(path.join(site, name), 'utf8');

test('downloads presents Windows and Linux desktop apps as its two choices', () => {
  const html = page('downloads.html');
  const grid = html.match(/<section class="download-grid"[\s\S]*?<\/section>/)?.[0];
  assert.ok(grid, 'download choices have a shared section');
  assert.equal((grid.match(/<article\b/g) || []).length, 2);
  assert.match(grid, /href="\/downloads\/windows\/Slithy-Tove-Web-Setup\.exe"/);
  assert.match(grid, /href="\/downloads\/linux\/Slithy-Tove-Linux-GUI-[^"]+\.tar\.gz"/);
  assert.match(grid, /Install for my account/);
  assert.doesNotMatch(grid, /terminal app below|Linux menu tool|install script/i);
});

test('visitor pages do not offer terminal packages or shell installers', () => {
  for (const name of fs.readdirSync(site).filter(name => name.endsWith('.html'))) {
    const html = page(name);
    // Old updater endpoints still serve existing users. New visitors get the GUI.
    assert.doesNotMatch(html, /href\s*=\s*["'][^"']*(?:\/install\/linux\/|\/updates\/linux-x64\/|slithy-install\.sh)/i, name);
    assert.doesNotMatch(html, /sudo\s+(?:bash|systemctl)|Download Linux install script|Full Linux terminal package/i, name);
  }
});

test('Linux guidance retains safe installation and older-wallet migration steps', () => {
  const html = page('guide.html');
  assert.match(html, /id="linux-desktop"/);
  assert.match(html, /Install for my account/);
  assert.match(html, /stop its mining and node before opening the desktop app/i);
  assert.match(html, /separate data folder/i);
  assert.match(html, /Do not copy an active wallet database or delete the original wallet/);
  assert.match(html, /Settings &gt; Updates/);
  assert.doesNotMatch(html, /What does the Linux terminal tool do/);
});
