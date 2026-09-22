(function () {
  'use strict';
  let blocks = [], busy = false;
  const search = document.querySelector('[data-transaction-search]');
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
      add('p', block.confirmations + ' confirmations in this snapshot. ' + time(block.time), details);
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
    if (!matches) add('p', query ? 'No match in the loaded transactions. This is not a search of the entire chain.' : 'No transaction details in this snapshot.', container);
    set('[data-transaction-count]', matches + ' transactions shown. Search covers the loaded details below.');
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
  async function refresh() {
    if (busy) return;
    busy = true;
    const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 10000);
    try {
      const response = await fetch('/data/explorer.json', {cache:'no-store',signal:controller.signal});
      if (!response.ok) throw Error('Feed unavailable');
      const data = await response.json(), age = Date.now() - Date.parse(data.updatedAt);
      if (!Number.isFinite(age) || age > 120000 || age < -30000 || data.chain !== 'test') throw Error('Stale feed');
      blocks = Array.isArray(data.blocks) ? data.blocks : [];
      set('[data-explorer-height]', data.height);
      set('[data-explorer-peers]', data.peerCount);
      set('[data-explorer-mining]', data.mining?.active === true ? 'On' : data.mining?.active === false ? 'Off' : 'Unknown');
      set('[data-explorer-updated]', time(data.updatedAt));
      set('[data-explorer-best]', data.bestBlockHash);
      set('[data-explorer-notice]', 'Recent confirmed transactions. Pending transactions are not included.');
      renderBlocks();
    } catch (_) {
      set('[data-explorer-notice]', 'Fresh explorer data is unavailable. Any details below are from the last loaded snapshot.');
      set('[data-explorer-height]', 'Unavailable'); set('[data-explorer-peers]', 'Unknown'); set('[data-explorer-mining]', 'Unknown');
    } finally {clearTimeout(timer);busy=false;}
  }
  search.addEventListener('input', renderTransactions);
  refresh(); setInterval(refresh, 60000);
})();
