using Slithy_Tove;

namespace Slithy.Linux;

internal sealed partial class MainWindow
{
    private async Task CheckWalletRecoveryWords()
    {
        if (_walletName.Length == 0) return;
        await Dialogs.CheckRecoveryWords(this, words => _wallet.CheckRecoveryWordsAsync(_endpoint!, words));
    }

    private async Task Consolidate()
    {
        if (_walletName.Length == 0) return;
        if (_paymentBlocked || _pending != null)
        {
            await Dialogs.Message(this, "Payment needs review", "Finish the previous payment before combining outputs. Use Send to review an approved transaction whose result is still unknown.");
            return;
        }
        var outputs = await _wallet.GetSpendableOutputsAsync(_endpoint!);
        var selected = await Dialogs.ChooseCoins(this, outputs, consolidation: true);
        if (selected == null || selected.Count < 2) return;
        string? password = await Dialogs.Password(this); if (password == null) return;
        await _wallet.UnlockWalletAsync(_endpoint!, password, TimeSpan.FromMinutes(2));
        PreparedTransfer prepared;
        try { prepared = await _wallet.PrepareConsolidationAsync(_endpoint!, selected); }
        finally { await _wallet.LockWalletOnlyAsync(_endpoint!); _locked = true; }
        ValidatePreparedPayment(prepared);
        if (!prepared.IsConsolidation) throw new InvalidDataException("The wallet did not prepare a consolidation transaction.");
        string message = $"Combine {prepared.InputCount} selected outputs into this wallet.\n\nReceiving address:\n{prepared.RecipientAddress}\n\nAmount after fee: {prepared.AmountAtomic / 100_000_000m:N8} SLTHY\nFee: {prepared.FeeAtomic / 100_000_000m:N8} SLTHY\n\nThis moves existing coins. It does not create a mining reward.";
        if (!await Dialogs.Confirm(this, "Confirm consolidation", message, "Combine outputs")) return;
        RememberApprovedPayment(prepared);
        await RelayPendingPayment();
    }

    internal static void ValidatePreparedPayment(PreparedTransfer prepared)
    {
        if (prepared.TransactionHash.Length != 64 || !prepared.TransactionHash.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(prepared.RecipientAddress) || prepared.AmountAtomic == 0 || prepared.InputCount < 1)
            throw new InvalidDataException("The prepared payment is missing details. Nothing was submitted.");
    }

    internal static string PaymentConfirmation(PreparedTransfer prepared)
    {
        ValidatePreparedPayment(prepared);
        decimal amount = prepared.AmountAtomic / 100_000_000m;
        decimal fee = prepared.FeeAtomic / 100_000_000m;
        string message = $"Send {amount:N8} SLTHY\nTo: {prepared.RecipientAddress}\nFee: {fee:N8} SLTHY\nTotal: {amount + fee:N8} SLTHY\nInputs: {prepared.InputCount}";
        if (prepared.ReusesChangeAddress)
            message += "\n\nChange returns to an existing wallet address because this wallet reached its recovery window. Reusing an address reduces privacy.";
        return message;
    }

    private void RememberApprovedPayment(PreparedTransfer prepared)
    {
        // Store the approved signed transaction before attempting the network call.
        // After a timeout, retry this transaction instead of creating a second payment.
        _paymentBlocked = true;
        decimal amount = prepared.AmountAtomic / 100_000_000m;
        SavePending(new PendingPayment(prepared, prepared.RecipientAddress, amount));
        _pending = prepared; _pendingAmount = amount; _pendingAddress = prepared.RecipientAddress;
        _paymentBlocked = false;
    }

    private async Task RelayPendingPayment()
    {
        var sent = await _wallet.RelayTransferAsync(_endpoint!, _pending ?? throw new InvalidOperationException("No approved payment is ready."));
        DeletePending(); _historyUpdated = default;
        await Dialogs.Message(this, "Payment submitted", "Transaction ID:\n" + sent.TransactionHash);
        await Refresh();
    }
}
