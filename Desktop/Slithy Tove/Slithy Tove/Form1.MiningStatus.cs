using System.Diagnostics;

namespace Slithy_Tove;

public partial class Form1
{
    private bool _statusRefreshInProgress;
    private bool _miningStatusRefreshing;
    private bool _miningStatusUnknown;
    private long _miningStateVersion;
    private DateTimeOffset? _lastMiningStatusUtc;

    private bool ShouldPauseMiningPoll() =>
        IsDisposed || Disposing || _closingForExit || _installingUpdate ||
        _miningChangeInProgress || _nodeStartupInProgress || _nodeStartupGate.CurrentCount == 0 ||
        _walletOpening || _walletBusy || _walletDialogOpen;

    private bool MiningPollIsCurrent(long version) =>
        version == _miningStateVersion && !ShouldPauseMiningPoll();

    private void ShowMiningStatusUnknown(string title = "Checking miner")
    {
        _miningStatusUnknown = true;
        miningTitleLabel.Text = title;
        miningDescriptionLabel.Text = "The local node did not answer. Mining state is unknown.";
        miningStatusLineLabel.Text = _lastMiningStatusUtc is { } lastReply
            ? $"Last reply: {lastReply.LocalDateTime:h:mm:ss tt}. Retrying. You can request a stop."
            : "Slithy will check again. You can still request a stop.";
        miningToggleButton.Text = "Stop mining";
        miningToggleButton.Enabled = CanMine();
        _bookMiningAnimation.Mining = false;
    }

    private bool TryShowUnconfirmedMiningChange(bool stopRequested, bool startRequestSent, Exception error)
    {
        if ((!stopRequested && !startRequestSent) || error is NodeRpcException) return false;
        // A daemon rejection is explicit. A lost or malformed reply cannot tell us
        // whether the command ran. Check status instead of sending Start again.
        ShowMiningStatusUnknown(stopRequested ? "Stop not confirmed" : "Start not confirmed");
        SetConnectionState("Checking connection", StatusKind.Neutral);
        statusMessageLabel.Text = "The local node did not answer. Mining state could not be confirmed.";
        AppendLog($"The {(stopRequested ? "stop" : "start")} request could not be confirmed. Mining state is unknown.");
        return true;
    }

    private async Task RefreshMiningStatusAsync()
    {
        if (_miningStatusRefreshing || ShouldPauseMiningPoll()) return;
        _miningStatusRefreshing = true;
        long version = _miningStateVersion;
        try
        {
            MiningInfo mining;
            try
            {
                mining = await _rpcClient.GetMiningInfoAsync(BuildLocalNodeUri());
            }
            catch (Exception ex)
            {
                if (!MiningPollIsCurrent(version)) return;
                Debug.WriteLine(ex);
                AppendLog($"Mining status request (getcpumininginfo) failed: {ex.Message}");
                ShowMiningStatusUnknown();
                return;
            }

            // A reply from before a Start, Stop or wallet change no longer describes this screen.
            if (!MiningPollIsCurrent(version)) return;
            _lastMiningStatusUtc = DateTimeOffset.UtcNow;
            _miningStartedByApp = mining.Active;
            if (!mining.Active)
            {
                ShowMiningStopped();
                return;
            }

            TrackMiningBlocks(mining.Blocks);
            ShowMiningStarted(Math.Max(1, mining.Threads), mining.Speed);
            await RefreshWalletForMiningProgressAsync();
        }
        finally { _miningStatusRefreshing = false; }
    }

    private async Task RefreshWalletForMiningProgressAsync()
    {
        if (_walletSnapshot is null || _walletRefreshing || ShouldPauseMiningPoll() ||
            string.IsNullOrWhiteSpace(_openWalletName) ||
            DateTimeOffset.UtcNow - _lastMiningWalletRefreshUtc < TimeSpan.FromSeconds(4)) return;

        _lastMiningWalletRefreshUtc = DateTimeOffset.UtcNow;
        _walletRefreshing = true;
        long version = _miningStateVersion;
        string wallet = _openWalletName;
        bool IsCurrentWallet() => MiningPollIsCurrent(version) &&
            _walletSnapshot is not null && wallet.Equals(_openWalletName, StringComparison.Ordinal);
        try
        {
            // Core updates an open wallet as blocks arrive. Routine polling must not start rescans.
            // Read even at the same height: pending payments and reorganizations can change balances.
            WalletSnapshot snapshot = await _walletRpcClient.GetSnapshotAsync(WalletRpcUri);
            if (!IsCurrentWallet()) return;
            _walletSnapshot = snapshot;
            ApplyWalletSnapshotToDisplay(snapshot, saveCache: true);
        }
        catch (Exception ex)
        {
            if (!IsCurrentWallet()) return;
            AppendLog($"Mining balance refresh failed: {ex.Message}");
            // The miner answered. A wallet or cache error must not turn that answer into a node failure.
            availableBalanceLabel.Text = "Last known balance. Checking for an update...";
        }
        finally { _walletRefreshing = false; }
    }
}
