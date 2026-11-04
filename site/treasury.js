(function () {
  'use strict';
  // This page describes the September beta. A live-network page needs its own identity.
  const betaTreasury = 'tslithy1quwm9s9qq9k03vdlus547p2x69uqvh50v3wffcu';
  function validAmount(value) {
    return typeof value === 'string' && /^\d{1,15}(\.\d{1,8})?$/.test(value) &&
      Number.isFinite(Number(value));
  }
  function validate(status, now = Date.now()) {
    return !!status && SlithyFeed.fresh(status, now) && status.chain === 'test' &&
      status.network === 'slithy-testnet' && Number.isSafeInteger(status.chainHeight) &&
      status.chainHeight >= 0 && /^[a-f0-9]{64}$/i.test(status.bestBlockHash || '') &&
      status.treasuryAddress === betaTreasury &&
      validAmount(status.currentBalance) &&
      (status.unlockedBalance === null || validAmount(status.unlockedBalance));
  }
  if (typeof module !== 'undefined' && module.exports) module.exports = { validate };
  if (typeof document === 'undefined') return;
  const amount = document.querySelector('[data-treasury-amount]');
  if (!amount) return;
  let status = null, busy = false;
  function set(name, text) {
    const element = document.querySelector('[data-treasury-' + name + ']');
    if (element) element.textContent = text;
  }
  function money(value) {
    return Number(value).toLocaleString(undefined, { maximumFractionDigits: 3 }) + ' SLTHY';
  }
  function paint() {
    // Do not substitute accrued rewards for a missing balance. Spending changes the balance.
    if (!validate(status)) {
      set('amount', 'Status temporarily unavailable');
      set('height', 'Unavailable');
      set('unlocked', 'Spendable balance unavailable');
      set('updated', 'Waiting for a fresh report');
      return;
    }
    set('amount', money(status.currentBalance));
    set('height', status.chainHeight.toLocaleString());
    set('unlocked', status.unlockedBalance === null ? 'Spendable balance unavailable' : money(status.unlockedBalance));
    set('updated', new Date(status.updatedAt).toLocaleString());
    set('address', status.treasuryAddress);
  }
  async function refresh() {
    if (busy) return;
    busy = true;
    try { status = await SlithyFeed.read('/data/treasury-beta-20260909.json'); }
    catch (_) { status = null; }
    finally { busy = false; paint(); }
  }
  refresh();
  setInterval(refresh, 60000);
  // A tab left open must stop presenting an old balance as a current reading.
  setInterval(paint, 5000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) { paint(); refresh(); } });
})();
