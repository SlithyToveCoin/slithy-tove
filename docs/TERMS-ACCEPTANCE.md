# First-run terms agreement

Published September 10, 2026 in Windows 0.1.41 and Linux 0.1.48-testnet. The Windows installer and app binaries have valid, timestamped Azure signatures for CS Idea Labs LLC. Both update feeds have verified Slithy release signatures.

The website terms are at https://slithy.io/terms.html. The apps carry a complete offline copy in `licenses/BETA-TERMS.txt`. Windows embeds it in the executable. The Linux release builder places it beside the menu as `slithy-terms.txt`; installation and update checks require that file.

## What users see

Windows asks before the main form starts a node or opens a wallet. The checkbox starts unchecked. Agree and continue becomes available after checking it. Decline and exit closes the app without changing wallets. Open wallet files opens the user's Slithy data folder, including previous network folders. This is access to existing files, not a live-wallet backup exporter. Stop any running node before copying wallet files.

Linux shows the full terms and requires typing AGREE. FILES prints wallet and settings locations. Any other answer declines. A command running without a terminal refuses actions that need agreement; it does not treat piped input as consent. Reading status and stopping mining remain available. `slithy terms` displays the bundled terms and `slithy wallet-files` prints file locations without agreement.

This prompt applies to the official Windows app and Linux menu. It does not change consensus rules or impose agreement on independent node software or direct native RPC use.

## Saved record

Windows stores `terms-acceptance.json` under the current user's roaming Slithy Tove data directory, outside network-reset folders. Linux stores it under `$XDG_CONFIG_HOME/slithy` or `$HOME/.config/slithy`, with file mode 0600.

Each record contains the terms version, a SHA256 hash of the bundled text and the UTC acceptance time. It is stored locally and is not uploaded. Missing, damaged or outdated records trigger the prompt again. Ordinary software updates do not prompt when the bundled terms are unchanged. Failure to save agreement leaves the action blocked.

Keep the bundled text stable for ordinary releases. For a terms revision, update the website and offline text together, change the version in both apps, and test fresh acceptance. Review material legal changes with counsel. A local acceptance record is not a guarantee that every provision is enforceable.

## Verification

Run `dotnet run --project tests/terms/TermsTests.csproj` on Windows. It uses a temporary profile and tests the actual dialog, including decline, unchecked default, explicit acceptance, restart, changed terms and damaged records. No real wallet is opened.

Run `python3 tests/terms/linux-terms-test.py` on Linux. It uses pseudo-terminal input and fake node tools in a temporary directory. It tests noninteractive refusal, decline, acceptance persistence, file permissions, changed or missing terms, damaged records, save failure and stop commands.

The release was published with packages first and matching update feeds afterward. HTTPS downloads were fetched again and checked against their signed manifests. The downloaded Windows installer passed Authenticode and timestamp verification. Previous versioned downloads and the legacy Linux feed remain available. This update does not reset the chain or replace wallets.

The Windows agreement screen confirms successful UI startup to the updater before the user accepts. Without that confirmation, the updater's startup deadline could expire while someone reads the terms. This confirmation does not record consent or start the local node.

For later releases, publish the complete archive before updating its feed. Do not publish an installer or updater that requires files its target archive does not contain.
