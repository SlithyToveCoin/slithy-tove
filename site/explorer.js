(function () {
  'use strict';
  const integer = n => Number.isSafeInteger(n) && n >= 0;
  const hash = value => typeof value === 'string' && /^[a-f0-9]{64}$/i.test(value);
  const amount = value => typeof value === 'string' && /^\d{1,15}(\.\d{1,8})?$/.test(value);
  const address = value => value === null || (typeof value === 'string' && value.length <= 128);
  function validate(data) {
    if (!data || !SlithyFeed.fresh(data) || data.chain !== 'test' ||
        !integer(data.height) || !integer(data.peerCount) || !hash(data.bestBlockHash) ||
        !Array.isArray(data.blocks) || data.blocks.length > 25) return false;
    // Match the snapshot's display limits before building DOM elements from it.
    return data.blocks.every(block => block && integer(block.height) && block.height <= data.height &&
      hash(block.hash) && integer(block.time) && integer(block.confirmations) &&
      integer(block.transactionCount) && Number.isFinite(block.difficulty) && block.difficulty >= 0 &&
      amount(block.minerSubsidyEstimate) && amount(block.treasurySubsidyEstimate) &&
      Array.isArray(block.transactions) && block.transactions.length <= 8 &&
      block.transactions.every(tx => tx && hash(tx.txid) && typeof tx.coinbase === 'boolean' &&
        integer(tx.inputCount) && integer(tx.outputCount) &&
        Array.isArray(tx.inputs) && tx.inputs.length <= 16 && tx.inputCount >= tx.inputs.length &&
        Array.isArray(tx.outputs) && tx.outputs.length <= 16 && tx.outputCount >= tx.outputs.length &&
        tx.inputs.every(input => input && (input.coinbase === true ||
          (hash(input.txid) && integer(input.vout) && address(input.address) &&
           (input.value === null || amount(input.value))))) &&
        tx.outputs.every(output => output && integer(output.n) && address(output.address) &&
          typeof output.type === 'string' && output.type.length <= 80 && amount(output.value) &&
          typeof output.treasury === 'boolean')));
  }
  if (typeof module !== 'undefined' && module.exports) module.exports = { validate };
  if (typeof document === 'undefined') return;
  let blocks = [], busy = false, snapshot = null;
  const search = document.querySelector('[data-transaction-search]');
  const linkedTransaction = new URLSearchParams(window.location.search).get('tx');
  if (linkedTransaction && /^[a-fA-F0-9]{64}$/.test(linkedTransaction)) search.value = linkedTransaction.toLowerCase();
  function set(selector, text) { const el = document.querySelector(selector); if (el) el.textContent = text; }
  function add(tag, text, parent, cls) { const el = document.createElement(tag); if (text !== undefined) el.textContent = text; if (cls) el.className = cls; parent.append(el); return el; }
  function time(value) { const d = new Date(typeof value === 'number' ? value * 1000 : value); return Number.isNaN(d.getTime()) ? 'Unknown' : d.toLocaleString(); }
  function renderTransactions() {
    const container = document.querySelector('[data-explorer-transactions]');
    const opened = new Set(Array.from(container.querySelectorAll('details[open]')).map(el => el.dataset.txid));
    container.replaceChildren();
    const query = search.value.trim();
    let matches = 0;
    for (const block of blocks) for (const tx of block.transactions || []) {
      if (query && !tx.txid.includes(query) && ![...tx.inputs, ...tx.outputs].some(e => (e.address || '').includes(query))) continue;
      matches++;
      const details = add('details', undefined, container, 'explorer-transaction');
      details.dataset.txid = tx.txid;
      details.open = !!query || opened.has(tx.txid);
      const summary = add('summary', undefined, details);
      add('span', 'Block ' + block.height + (tx.coinbase ? ' / Mining reward' : ' / Transaction'), summary);
      add('code', tx.txid, summary, 'tx-address');
      add('p', block.confirmations + (block.confirmations === 1 ? ' confirmation' : ' confirmations') + ' at last update. Block time: ' + time(block.time), details);
      add('h3', 'Inputs', details);
      for (const input of tx.inputs) {
        const row = add('div', undefined, details, 'tx-entry');
        if (input.coinbase) { add('p', 'New coins created by mining. No sending address.', row); continue; }
        add('code', input.address || 'Previous output address unavailable', row, 'tx-address');
        if (input.value != null) add('span', input.value + ' SLTHY', row);
        add('p', 'Spends output ' + input.vout + ' of transaction:', row);
        add('code', input.txid, row, 'tx-address');
      }
      add('h3', 'Outputs', details);
      for (const output of tx.outputs) {
        const row = add('div', undefined, details, 'tx-entry');
        add('p', 'Output ' + output.n + (output.treasury ? ' / Literacy treasury' : ''), row);
        add('code', output.address || 'No standard address (' + output.type + ')', row, 'tx-address');
        add('span', output.value + ' SLTHY', row);
      }
      if (tx.inputCount > tx.inputs.length || tx.outputCount > tx.outputs.length) add('p', 'Showing the first 16 inputs and outputs. Total: ' + tx.inputCount + ' inputs, ' + tx.outputCount + ' outputs.', details);
    }
    if (!matches) add('p', query ? 'No match in the details shown here. Some transactions and addresses are outside this list.' : 'No transaction details are available yet.', container);
    set('[data-transaction-count]', matches + (matches === 1 ? ' transaction shown.' : ' transactions shown.') + ' Search checks the transactions below.');
  }
  function renderBlocks() {
    const body = document.querySelector('[data-explorer-blocks]');
    body.replaceChildren();
    for (const block of blocks) {
      const row = add('tr', undefined, body);
      for (const text of [block.height, time(block.time), block.hash, block.transactionCount, block.minerSubsidyEstimate + ' SLTHY', block.treasurySubsidyEstimate + ' SLTHY', block.difficulty]) add('td', text, row);
    }
    renderTransactions();
  }
  function markUnavailable() {
    set('[data-explorer-notice]', 'The explorer could not refresh. Details below may be out of date.');
    set('[data-explorer-height]', 'Unavailable'); set('[data-explorer-peers]', 'Unknown'); set('[data-explorer-mining]', 'Unknown');
  }
  function expire() {
    if (snapshot && !SlithyFeed.fresh(snapshot)) { snapshot = null; markUnavailable(); }
  }
  async function refresh() {
    if (busy) return;
    busy = true;
    try {
      const data = await SlithyFeed.read('/data/explorer.json', 4194304);
      if (!validate(data)) throw Error('Invalid or stale feed');
      blocks = data.blocks;
      set('[data-explorer-height]', data.height);
      set('[data-explorer-peers]', data.peerCount);
      set('[data-explorer-mining]', data.mining?.active === true ? 'On' : data.mining?.active === false ? 'Off' : 'Unknown');
      set('[data-explorer-updated]', time(data.updatedAt));
      set('[data-explorer-best]', data.bestBlockHash);
      set('[data-explorer-notice]', 'Recent confirmed transactions. Pending transactions are not included.');
      renderBlocks();
      snapshot = data;
    } catch (_) {
      snapshot = null;
      markUnavailable();
    } finally {busy=false;}
  }
  search.addEventListener('input', renderTransactions);
  refresh(); setInterval(refresh, 60000);
  setInterval(expire, 5000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) { expire(); refresh(); } });
})();
