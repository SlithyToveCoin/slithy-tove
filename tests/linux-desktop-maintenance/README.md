# Linux desktop maintenance checks

This test uses a temporary Linux profile and a disconnected regtest node. It does not open installed wallets or contact public peers. Run it under a normal Linux account.

Build a self-contained test runner:

```sh
dotnet publish tests/linux-desktop-maintenance/LinuxDesktopMaintenance.Tests.csproj \
  -c Release -r linux-x64 --self-contained true -o /tmp/slithy-maintenance-checks
```

Pass the absolute path to a reviewed Slithy native daemon:

```sh
/tmp/slithy-maintenance-checks/slithy-maintenance-tests /absolute/path/to/slithyd
```

Leave RPC port 54436 free. The test starts a disposable GUI-host process, terminates that process to simulate a crash, and checks recovery of its own node. Refusal cases include a reused PID, changed executable hash, wrong data directory, public RPC cookie and authentication changed during confirmation. The test does not stop an unknown node.

Installer checks cover package hashes, unsafe paths and repeat installation. Wallet and extra-file canaries must survive uninstall. A separate graphical smoke test exercises the actual package in an isolated Xfce session:

```sh
python3 tests/linux-gui/xfce-session.py --output /tmp/slithy-install-results --timeout 360 -- \
  python3 tests/linux-gui/graphical_install_smoke.py /path/to/extracted/package /tmp/slithy-install-results
```

The desktop check needs Xvfb, Xfce, xdotool and desktop-file-validate. It launches the renamed installer and uninstaller, checks their window icon and validates the resulting desktop entry. No wallet node is started.
