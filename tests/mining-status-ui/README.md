# Windows mining status checks

Run on Windows with the .NET 10 SDK:

```powershell
dotnet run --project tests/mining-status-ui/MiningStatusUiTests.csproj --configuration Release
```

The tests create disposable Windows forms and replace their HTTP clients with fake replies. Settings go in a temporary directory. Normal startup and polling timers are disabled, and the treasury URL is blank. The tests do not open a real wallet, run a native node, contact a server, or touch the installed app.

A delayed balance read must not change a confirmed mining status into a connection error. The checks cover failed balance reads, failed mining reads, overlapping timer ticks, and replies arriving after a start or stop. They also check that wallet dialogs, startup, and shutdown block background work, and that Stop remains usable when a mining status request fails.

A lost Start or Stop reply is resolved by checking the miner, without automatically repeating the command. Ordinary wallet refreshes also reject old balance and transaction replies after closing and reopening a wallet, even when its name has not changed. Routine balance checks and stopping mining must not trigger a chain rescan.

Holding a reply uses a task completion source, not a slow real server. Each wait has a short test deadline so a regression fails instead of leaving the test window running.

## Native balance tracking

```powershell
pwsh -NoProfile -File tests/mining-status-ui/native-balance-tracking.ps1
```

This separate test starts a disposable regtest node with no peer connections. It checks mined rewards, maturity, pending payments at the same height, and balance changes after invalidating and reconsidering a block. It does not request a wallet rescan. The temporary node uses cookie authentication and is stopped and removed when the test finishes.
