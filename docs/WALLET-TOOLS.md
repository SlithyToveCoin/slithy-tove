# Wallet tools

These tools are included in the Windows 0.1.47 and Linux GUI 0.1.47-beta.1 builds. They use your local node. Test coins have no monetary value and do not carry over to the live network.

## Transaction history and addresses

Open **Activity** to browse your wallet history. Use the search box to find a transaction ID or address. The filters narrow the results by activity and status. **Previous** and **Next** move through the results, and **Export CSV** saves the filtered history rather than the displayed page.

Each entry shows where coins were sent or received. Choose **View details** to copy the complete address or transaction ID, inspect confirmations and see the transaction's outputs. An address can appear in several transactions. A transaction can also create several outputs, including change returned to your own wallet.

An incoming payment does not have a verified sender address. Inputs refer to earlier outputs, and several people can contribute inputs to a transaction. The app shows the receiving address and transaction details without treating an input address as someone's identity.

CSV files contain wallet activity, not private keys. Addresses and amounts can still reveal private information. Check a file before sharing it. A transaction fee belongs to the whole transaction and can repeat on its activity rows; do not add those repeated values together.

The website explorer covers recent blocks. Older transactions may be absent there even when your local wallet has them. Opening the explorer shares the transaction ID with slithy.io.

## Choosing inputs and combining small rewards

The normal **Send** option selects spendable outputs for you. Under **Advanced**, you can choose specific outputs instead. This is often called coin control. Immature mining rewards cannot be spent yet and are not available for selection.

Choose **Consolidate** to combine selected spendable outputs into one new output in your own wallet. The app accepts between 2 and 200 selected outputs per consolidation. That is a batch size for this tool, not a limit on how many outputs an address can receive. A wallet with more outputs can use several batches.

Review the destination and exact fee before confirming. Consolidation spends a transaction fee, and putting outputs together links them publicly on the chain. It does not create more coins. Do not consolidate automatically just because a wallet has many rewards.

If a broadcast result is uncertain, use the app's retry option for the already approved transaction. Do not create a second payment just to see if the first one worked.

## Receiving addresses

Open **Receive** to create a new address or copy a previous one. Every address in that list belongs to the same wallet. An old address can still receive coins after you create a new one.

Creating an address does not redirect mining that is already running. Check the mining destination separately before starting a miner.

## Recovery words

When you create a wallet, Slithy asks you to confirm selected words from your written copy. Later, **Manage wallet > Check recovery words** compares those words with the open wallet's receiving and change descriptors. It works with a locked wallet and does not send the words to a public service.

That check verifies the wallet's receiving and change descriptors match those words. A wallet imported from another program can contain additional keys or descriptors that those words do not recover. Retain a wallet-file backup for those keys and your labels. The check does not replace a restore test. Store your words somewhere private, separate from your wallet password. Support will not ask you to send either one.

A restore scans the chain from the beginning. By default, it checks receiving and change addresses through index 10,000. The scan may take several minutes. Advanced restore options let you raise the last index to 100,000 if another program used addresses beyond the default range.

These GUIs stop generating new receiving or change addresses beyond the default recovery range. Other wallet software may use a different range. If you used another program, retain a wallet-file backup and verify the restored balance before relying on recovery words alone. Words also do not restore labels or other local notes saved in a wallet file.

If a deeper restore has already passed the change-address limit, a payment can return change to a verified address at index 0 in your wallet instead. The confirmation warns about that address reuse. This lets you spend recovered coins without creating change that a default restore would miss.

Try recovery with test coins first. Check the balance and a few known transactions after the local node finishes syncing. Avoid sending from two copies of the same wallet at the same time.
