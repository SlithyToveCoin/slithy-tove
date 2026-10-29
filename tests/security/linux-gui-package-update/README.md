# Linux GUI package update check

This check uses the real release archive and its native node. It creates a disposable regtest wallet, adds spendable and immature rewards, cancels a download, exercises rollback, then hands startup to the new GUI. The final checks compare the balances and address after replacement.

The harness links the production updater and node-host source. Its HTTP handler serves a manifest signed with a temporary key held in memory. It does not change the release key or contact the update server. A test process adapter adds `--regtest` to the unchanged replacement GUI executable and completes the window handoff. The default production launch path has separate tests in `../linux-gui-update/`.

You need Linux x64 with a graphical session and the GUI's system libraries. RPC port 54436 must be free. The test never attaches to a node that already owns that port. Public peer connections are disabled. HTTP requests outside localhost use a deliberately unavailable proxy during the test.

Build on a machine with .NET 8:

```sh
dotnet publish tests/security/linux-gui-package-update/LinuxGuiPackageUpdate.Tests.csproj \
  -c Release -r linux-x64 --self-contained true -o /tmp/slithy-update-harness
```

Run with the release archive, an extracted previous GUI folder, and a report directory:

```sh
/tmp/slithy-update-harness/LinuxGuiPackageUpdate.Tests \
  /path/to/Slithy-Tove-Linux-GUI-0.1.44-beta.1-x64.tar.gz \
  /path/to/slithy-tove-linux-gui-0.1.43-beta.1 \
  /tmp/slithy-update-results
```

The report contains checks and package hashes, not wallet secrets. The disposable profile remains under `/tmp/slithy-package-update-*` for inspection. It contains test keys, so do not include that folder in published results. Your regular settings and wallet folders are not used.

The preceding GUI release has no built-in updater. Its native binary and a saved launcher act as the old installation for this test. The candidate is a real GUI process; the test does not click through its wallet password dialog. Wallet persistence is checked through the isolated node's RPC after the window has taken ownership.
