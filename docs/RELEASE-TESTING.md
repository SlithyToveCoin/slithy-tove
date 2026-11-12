# Testing published desktop packages

Source checks build the code. Release smoke tests install the files people download from slithy.io. Both checks belong in a release review.

The `Published desktop checks` GitHub workflow uses fresh hosted Windows and Ubuntu machines. It downloads a specific release and checks its SHA256 before running it. The jobs have read-only repository access.

## Windows

The test uses the signed public installer with its real application ID. It checks publisher signatures, installation registration and shortcuts, then removes the installation and checks that disposable wallet data survives. It also opens the first-run window and declines the terms rather than accepting them for somebody else.

A separate transaction fixture calls the installed application's wallet code. It mines regtest blocks in small batches, sends a coin and reopens an encrypted backup to check its balance. Each phase is recorded so a failure can be traced to the operation that failed.

Run this test on a disposable machine. It refuses to run when it finds an existing Slithy installation, profile or running process. Do not use a computer that holds your wallets.

The hosted runners use Windows Server. They cover the installer and Windows APIs, but cannot establish how SmartScreen, UAC or desktop scaling behaves on every Windows 11 computer. The unchanged web installer has a separate interactive download flow; the offline installer checks do not cover every screen in that flow.

## Linux

The Linux checks use the published archive in a private HOME and XDG profile. They validate the installed desktop entry and icon, launch the GUI and exercise window controls. Wallet tests run on an isolated regtest chain. They never mine on the public network.

Xfce runs on X11. The Weston session tests the app through XWayland. This release does not use a native Wayland backend. Virtual displays and software rendering make these tests repeatable, but physical GPUs and multi-monitor setups still need testing on desktop machines.

Xfce checks dragging, resizing and minimizing. Nested Weston may report dragging and resizing as unsupported by the automation. Its shell has no taskbar or notification-area host for restore and tray checks. Those results are not counted as passes; check them on a regular Wayland desktop.

An additional Xfce job uses the official Ubuntu 26.04 container. It checks the newer system libraries, not a complete Xubuntu installation or its kernel and display drivers.

The package also contains runtime, wallet and updater fixture tests. These cover simulated future updates and rollback. A fixture result should not be described as an update downloaded from the public feed.

## Results

The workflow uploads test reports and screenshots as artifacts. It does not upload the temporary home directory, wallet databases or node authentication files. A failed step fails the job even when evidence upload succeeds.

For a new release, review and update the pinned URLs and hashes in the test scripts. Do not change a pin to make a failing download pass without checking the release first. Passing these tests does not publish software or change an update feed.

## Mining status checks

Run `dotnet run --project tests/mining-status-ui/MiningStatusUiTests.csproj -c Release` on Windows. It uses temporary settings and simulated RPC replies. No user wallet or node is opened.

The checks cover delayed replies during Start, Stop and wallet changes. A failed balance read must leave a confirmed mining result intact. A failed mining-status request must leave Stop available and recover when replies resume. Routine balance polling and stopping workers must not rescan the blockchain.

Run `pwsh -NoProfile -File tests/mining-status-ui/native-balance-tracking.ps1` after building the Windows native tools. It creates a disconnected regtest node and disposable wallets. It checks reward maturity, an incoming payment before and after confirmation, and a tip rollback followed by reconsideration. No check requests a rescan. The script shuts down its own node and removes its temporary directory when finished.

The Linux GUI's `--ui-test` includes the same balance-versus-miner failure checks, plus failed network and disk reads. It also tests Stop during delayed replies and verifies that a lost Start reply does not lead to a second Start. Headless UI tests do not replace a long mining session on a physical Windows or Linux computer. Desktop testing should also cover sustained CPU load, sleep and wake.

Run `dotnet run --project tests/diagnostic-log/DiagnosticLogTests.csproj -c Release` to check local diagnostic files. The tests cover rotation, private permissions and rejection of linked paths. Raw messages containing disposable secret markers must never appear in the saved files. Run on Linux as well as Windows to exercise the platform-specific file checks.
