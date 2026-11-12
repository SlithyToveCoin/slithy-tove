# App diagnostic log checks

```text
dotnet run --project tests/diagnostic-log/DiagnosticLogTests.csproj --configuration Release
```

This test uses temporary files. It does not open wallets, contact servers, or start an app. Run it on Windows and Linux. Linux adds permission checks; symbolic-link checks run where the account can create links.

The persistent app log records UTC time, the app assembly version, and fixed event labels. Error details are reduced to a fixed category such as `timeout`, `connection`, or `wallet-locked`. Wallet names, addresses, transaction IDs, paths, passwords, keys, recovery words and raw exception messages are not copied into it. Unrecognized messages are dropped. This is deliberately less detailed than the technical log visible during a session.

The logger retains up to four 64 KiB files. Repeated adjacent events are limited to one entry per 30 seconds. One background writer handles the disk work, with room for 64 fixed entries in its queue. If the drive stalls and the queue fills, new diagnostics are dropped. Closing the app does not wait for the writer, so its final entries may be lost. These files are for troubleshooting, not transaction recovery.

Each session creates a new file instead of reopening an existing one for writing. Linux uses directory mode 0700 and file mode 0600. On Windows, the directory inherits permissions from the account's app-data folder. Linked paths are rejected. A logging failure disables further writes for that logger instance and does not interrupt the app.

Locations:

- Windows: `%APPDATA%/Slithy Tove/networks/beta-20260909/logs/app-diagnostics/`
- Linux: the GUI's data directory, under `diagnostics/`. With default XDG settings, that is `~/.local/share/slithy-tove-gui/beta-20260909/diagnostics/`.

These limits apply to the app diagnostic files. Native node logs are separate, and a manually exported technical log can contain more detail. Review an export before sharing it. The app does not upload these diagnostic files automatically.
