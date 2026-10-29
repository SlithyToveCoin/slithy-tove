# Desktop update notices and the Linux GUI

This release covers Windows 0.1.44, Linux desktop 0.1.44-beta.2 and Linux
terminal 0.1.50-testnet. These remain test-network builds. The live-network
target is December 1, 2026, subject to testing.

## Windows

An update badge stays beside the version when a newer release is available.
The notification-area menu also offers Review update. Startup and hourly checks
do not interrupt an open wallet or start an installation. Choose Review update
when you are ready.

## Linux desktop

The window and dialogs have a Slithy title bar, with a smaller default layout.
Opening a wallet now gives you a dropdown and remembers your last selection.
Restoring the window from the tray does not run startup again or repeat the
wallet prompt. A desktop test caught that issue before the update feed changed.

Settings includes update checks. A new release shows a badge and an in-app
notice. Desktops with a notification service can also show a notification.
Downloading does not stop mining. Before installing, the app verifies the signed
release and package hash, stops its node and opens the new version. Start mining
again yourself after it opens.

GUI versions through 0.1.43-beta.1 need one manual upgrade. Close the old app,
extract the new archive into a separate folder and run `bash install-desktop.sh`.
Your wallet data stays in place. Later GUI releases can use the updater.

## Linux terminal

The menu checks for updates in the background. Mining watch shows an update
notice and accepts U to open Updates. It does not stop mining to check.

## Source and testing

The native consensus source has not changed for this release. GUI and terminal
packages retain their separately tested native builds. Both use the same public
beta network and recovery-word format.

Tests cover signed-feed validation, damaged archives, failed starts and launcher
rollback. The packaged GUI upgrade passed 26 checks, including wallet balances,
address persistence and mining remaining stopped after the new app opens.
Wallet checks use isolated regtest nodes, not payments on the public
network. Window layouts are tested at several sizes. Ubuntu 24.04 x64 under WSL2
is the local test environment; other distributions and desktop environments
still need testing. Report your distribution and desktop with any display bug.

The Linux GUI assembly was rebuilt from the staged public source and matched the
release assembly byte for byte. Its SHA256 is
`b128549d79e59bc80daf3e18ad09f25e7ed758abf70dbffcd616862462758281`.
The build used SDK 9.0.320 targeting .NET 8, Release, linux-x64, self-contained, `DebugType=None` and
`DebugSymbols=false`. To reproduce that assembly's version metadata, also pass
`-p:SourceRevisionId=d5672f4a2e8ad376bca2b231b2ef7a65cc340d8d`.
That value records the development checkout's metadata, not the publication
commit. Changing it changes the assembly hash without changing its behavior.

The 79 Windows build inputs match this source after line-ending normalization.
The Windows build uses `IncludeSourceRevisionInInformationalVersion=false`, so
its version metadata reads 0.1.44. Authenticode signatures and timestamps are
added after compilation; unsigned rebuilds will not have the same file hashes.

Keep your recovery words and wallet backups private. Send bug reports through
[slithy.io](https://slithy.io/bug-report.html).
