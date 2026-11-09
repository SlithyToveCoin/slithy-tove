# Desktop runtime update

Windows 0.1.49 and Linux desktop 0.1.50-beta.1 bundle .NET 10. You do not
need to install .NET separately. The wallet screens and mining controls work
as before.

The Windows installer removes obsolete runtime files after copying the new
version. A file must match its recorded name and hash before it is removed.
Wallet files and settings are not part of that cleanup.

The Windows web installer also includes the new runtime and a Licenses link.
Its download address stays the same.

## Updating

Back up your wallet, then review the update from Settings. Mining stops for
the update and stays stopped when the app reopens. Your wallet files, saved
thread count and current beta balances stay in place. This update does not
reset the chain or change the mining rules.

The website now offers the desktop app for both Windows and Linux. Existing
Linux terminal installations are not replaced or removed. The terminal and
desktop apps use separate data folders. Stop the old node before moving a
wallet to the desktop app, and retain your backup.

## Linux testing

The desktop package was tested on Ubuntu 24.04 with Xfce. Other distributions,
Wayland desktops and different graphics hardware still need testing. Use Quit
inside the app to stop its node. Sending SIGTERM directly to the GUI can leave
the node running, as it could in the previous release.

This remains a beta test network. Test coins have no market value and will not
carry over to the live network.
