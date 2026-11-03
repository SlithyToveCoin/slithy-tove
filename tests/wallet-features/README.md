# Wallet feature checks

Run the fast checks from the repository root:

```powershell
dotnet run --project tests/wallet-features/WalletFeatures.Tests.csproj -c Release
```

These checks use fake RPC responses. They cover full-history searches, changing history during export, CSV formula escaping, signed-payment previews, explicit input selection and recovery failure handling.

The native fixture needs a locally built daemon:

```powershell
dotnet run --project tests/wallet-features/WalletFeatures.Tests.csproj -c Release -- --integration --node build/native/windows/bin/slithyd.exe
```

It creates a separate temporary regtest directory and random loopback RPC port. Peer discovery and listening are disabled. The fixture never opens your normal wallet or sends a public transaction. Sparse recovery scans can take several minutes.

The fixture funds receiving indices 1,001 and 10,000, spends through change index 1,001, then checks recovery from the same words. A deeper scan checks receiving index 12,001. Another wallet receives 2,200 outputs at one address and makes an automatic payment with more than 675 inputs. Explicit-input payments and a 200-input consolidation are broadcast on the isolated chain.

Use `--skip-recovery` to rerun the payment checks without repeating the slow recovery scans.

## Cross-platform recovery

Append `--export-interop <private-directory>` to the Windows native fixture to write a disconnected chain and disposable test words. Do not put that directory in source control or a release package. It does not contain a real wallet, but test words still belong in a private directory.

Publish this test project for Linux, then run its executable with:

```text
--interop --fixture <private-directory> --node <reviewed-linux-daemon>
```

The Linux stage copies the chain into a private temporary directory. It restores the Windows-generated words, checks funded receiving and change paths, tests the maximum explicit scan range, and spends recovered coins. It also verifies that a deep-restored wallet can show its balance without generating another address. Test credentials are not printed. Linux temporary directories use mode 0700 and credential files use mode 0600.

Delete the exported disposable words after testing. The native fixture leaves its isolated wallet directory in the system temporary folder for diagnosis.

## Backend behavior

- History uses 500-entry RPC windows and scans beyond the first page when you search. It deduplicates wallet entries by transaction ID, output index, category and address. A changed wallet transaction count or chain tip causes the read to fail with a refresh message rather than mixing pages from different chain states.
- CSV export includes every matching wallet entry, up to 64 MiB. The fee column is a transaction fee and can repeat for several entries from one transaction. It is not a column to sum without grouping by transaction ID.
- Transaction inputs are previous transaction IDs and output indices. They do not identify a sender. Output addresses and wallet-entry addresses are shown separately.
- Coin control uses exactly the selected confirmed, safe, spendable inputs. Funding is not allowed to add other inputs. The signed result is checked before confirmation, and retry broadcasts the same bytes.
- Consolidation accepts 2 through 200 inputs per app batch. It sends them to a new wallet address and subtracts the fee from that output. This batch size is not an address or consensus limit.
- Word recovery scans both receiving and change indices 0 through 10,000 by default. An explicit deeper scan can extend through 100,000. Address generation beyond the default window is blocked. If the change branch reaches that window, a normal payment can reuse verified receiving index zero for change, with a privacy note in its preview.
- Reading a balance does not need a new address. A restored wallet can use an existing receiving address or verified descriptor index zero even if its address book was not restored.
- Word checks compare the two app descriptors. A wallet file imported from another tool can contain additional descriptors or keys that those words do not cover. Back up that wallet file too.
