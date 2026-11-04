# Security fixes for the desktop and terminal apps

This release covers Windows 0.1.48, Linux desktop 0.1.49-beta.1 and
Linux terminal 0.1.51-testnet. All use the existing public beta network.
Test coins have no market value and will reset before the live launch.

## Changes

Wallet RPC requests no longer follow redirects. Error handling removes private
descriptor and password arguments before displaying an error. The Linux
terminal also stops printing raw recovery-import errors.

Wallet restore and update copying reject linked paths that could lead outside
the chosen folder. Windows holds a verified update file open until installation
finishes. Linux settings check linked parent folders and dangling links before
reading or writing a record.

The native dependency recipe uses libevent 2.1.13. The public report publishers
now select their output fields explicitly, and treasury snapshots check for a
chain reorganization before publishing. A late-halving mining-template fix
removes the treasury-output requirement when its calculated amount rounds to
zero. The subsidy schedule and block-validation rules have not changed.

## Updating

Review the update inside your app, or download the current package from
[slithy.io](https://slithy.io/downloads.html). The Windows web installer remains
the same file and downloads the current signed release.

Back up your wallet before updating. The update does not delete your wallet
files or reset this beta chain. Mining stays stopped after the app restarts.

If a wallet backup is stored behind a filesystem link or cloud placeholder,
copy it to a regular local folder before restoring it.

## What this does not establish

The fixes address specific defects found during source review. They do not
establish that every possible vulnerability has been found. A trusted signature
identifies a publisher and protects file integrity; it is not a security audit.

The live-network launch mechanism still needs a separate decision and test.
This beta update does not start that network or change its consensus parameters.
