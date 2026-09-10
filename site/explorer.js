(function () {
  const formatNumber = new Intl.NumberFormat("en-US", { maximumFractionDigits: 8 });
  const formatAmount = new Intl.NumberFormat("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 8
  });

  function setText(selector, value) {
    const element = document.querySelector(selector);
    if (element) {
      element.textContent = value;
    }
  }

  function formatTime(value) {
    if (!value) {
      return "Not available";
    }
    const parsed = typeof value === "number" ? new Date(value * 1000) : new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return "Not available";
    }
    return parsed.toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit"
    });
  }

  function renderBlocks(blocks) {
    const body = document.querySelector("[data-explorer-blocks]");
    if (!body) {
      return;
    }

    if (!Array.isArray(blocks) || blocks.length === 0) {
      body.innerHTML = '<tr><td colspan="7">No recent blocks were reported.</td></tr>';
      return;
    }

    body.innerHTML = blocks.map((block) => {
      const difficulty = Number(block.difficulty || 0);
      const miner = Number(block.minerSubsidyEstimate || 0);
      const treasury = Number(block.treasurySubsidyEstimate || 0);
      return `
        <tr>
          <td>${block.height ?? ""}</td>
          <td>${formatTime(block.time)}</td>
          <td><code>${block.hash || ""}</code></td>
          <td>${block.txCount ?? 0}</td>
          <td>${formatAmount.format(miner)} SLTHY</td>
          <td>${formatAmount.format(treasury)} SLTHY</td>
          <td>${formatNumber.format(difficulty)}</td>
        </tr>`;
    }).join("");
  }

  async function loadExplorer() {
    try {
      const response = await fetch("/data/explorer.json", { cache: "no-store" });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      const data = await response.json();

      setText("[data-explorer-height]", formatNumber.format(Number(data.height || 0)));
      setText("[data-explorer-peers]", formatNumber.format(Number(data.peerCount || 0)));
      setText("[data-explorer-mining]", data.miningActive ? "On" : "Off");
      setText("[data-explorer-updated]", formatTime(data.updatedAt));
      setText("[data-explorer-best]", data.bestBlockHash || "Not available");
      renderBlocks(data.blocks);
    } catch (error) {
      setText("[data-explorer-height]", "Offline");
      setText("[data-explorer-peers]", "Unknown");
      setText("[data-explorer-mining]", "Unknown");
      setText("[data-explorer-updated]", "Check again soon");
      setText("[data-explorer-best]", "The explorer feed could not be loaded.");
      renderBlocks([]);
    }
  }

  loadExplorer();
  window.setInterval(loadExplorer, 60000);
}());
