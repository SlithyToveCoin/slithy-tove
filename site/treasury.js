async function loadTreasuryStatus() {
  const amount = document.querySelector("[data-treasury-amount]");
  const height = document.querySelector("[data-treasury-height]");
  const unlocked = document.querySelector("[data-treasury-unlocked]");
  const updated = document.querySelector("[data-treasury-updated]");
  const address = document.querySelector("[data-treasury-address]");
  if (!amount) return;

  try {
    const response = await fetch("/data/treasury-beta-20260909.json", { cache: "no-store" });
    if (!response.ok) throw new Error("Treasury status unavailable");
    const status = await response.json();

    const displayedAmount = status.currentBalance ?? status.accruedRewards;
    amount.textContent = `${Number(displayedAmount).toLocaleString(undefined, {
      minimumFractionDigits: 0,
      maximumFractionDigits: 3
    })} SLTHY`;
    height.textContent = Number(status.chainHeight).toLocaleString();
    unlocked.textContent = status.unlockedBalance === null
      ? "Awaiting wallet scan"
      : `${Number(status.unlockedBalance).toLocaleString(undefined, {
          minimumFractionDigits: 0,
          maximumFractionDigits: 3
        })} SLTHY`;
    updated.textContent = new Date(status.updatedAt).toLocaleString();
    address.textContent = status.treasuryAddress;
    address.title = status.treasuryAddress;
  } catch {
    amount.textContent = "Status temporarily unavailable";
  }
}

loadTreasuryStatus();
