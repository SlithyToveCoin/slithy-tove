# Linux desktop beta

This guide covers Linux desktop test package 0.1.48-beta.1. It connects to the public test network. Test SLTHY has no market value. The live-network target is December 1, 2026. Wallets and the chain will reset before that launch.

The terminal app remains available. Installing this GUI does not replace it, and the terminal updater does not update the GUI.

## Run the test package

Use a Linux x64 desktop with an X11 session or XWayland. ARM computers and headless servers are not supported by this package. Extract the archive into a folder you own, open **slithy-install**, then choose **Install for my account**. It checks the package files and puts the program in `~/.local/opt/slithy-tove/<version>/`. Open **Slithy Tove** from your application menu afterward. You can then remove the extracted download folder.

To try the app without installing it, open **slithy-desktop** from the extracted folder. If your file manager does not launch executables, you can run it from a terminal:

```bash
chmod +x slithy-desktop Binaries/slithyd
./slithy-desktop
```

Do not run the app with sudo. The self-contained package includes .NET, though the desktop still needs its normal X11 and font libraries. Ubuntu 24.04 x64 is the reference platform. Other distributions and desktop environments need testing.

The bundled daemon needs glibc 2.34 or later, a C++ runtime providing
`GLIBCXX_3.4.32`, Libevent 2.1 and SQLite 3. The desktop also needs X11,
fontconfig and OpenGL libraries, plus the normal .NET native dependencies
such as ICU and OpenSSL. On Ubuntu 24.04, the relevant packages include
`libstdc++6`, `libevent-core-2.1-7t64`, `libevent-extra-2.1-7t64`,
`libevent-pthreads-2.1-7t64`, `libsqlite3-0`, `libx11-6`, `libice6`, `libsm6`,
`libfontconfig1`, `libgl1`, `libicu74` and `libssl3t64`. Most are already
installed on a full desktop. A headless server is not a desktop test
environment. Older distributions may need a build made for their libraries.

The older `install-desktop.sh` script remains available for a portable installation. It creates a menu shortcut pointing to the extracted folder, so leave that folder in place if you use the script. The graphical installer copies the program to its managed installation folder instead.

To remove a graphical installation, close Slithy, then right-click its application-menu entry and choose **Uninstall Slithy Tove**. You can also open **slithy-uninstall** in its installed version folder. Removal leaves your wallets, settings and blockchain data in place. Older program versions and any extra files you placed beside the program are left alone.

The main window and dialogs use the Slithy logo. The application menu and taskbar use the same application ID. Some desktops may ask you to unpin an older shortcut and pin the new Slithy Tove launcher after updating. If an old private preview shortcut still opens an earlier build, use the Slithy Tove entry installed by the current package instead.

The window starts at 980 by 720 pixels, with smaller buttons and aligned balance and mining panels. You can resize it down to 760 pixels wide. Settings scroll when they need more room.

`./slithy-desktop --preview` opens the interface with sample values. Preview mode does not start a node, open wallets or mine. Use it to inspect the layout, not to test transactions.

## Your existing wallets

When Slithy opens, choose your wallet from the dropdown and enter its password. The app remembers your last wallet name. You do not need to type the name again. The same screen has options to create a wallet or restore one. If no wallets are found, choose one of those options first.

The GUI uses a separate local node and wallet directory. It does not attach to a server or the existing Linux terminal installation.

- Settings: `~/.config/slithy-tove-gui/beta-20260909/settings.json`
- Node data: `~/.local/share/slithy-tove-gui/beta-20260909/`
- Wallets: the `wallets` folder inside that node directory
- Node log: `node.log` inside that node directory

These paths follow `XDG_CONFIG_HOME` and `XDG_DATA_HOME` when they are set to absolute paths. Settings and wallet folders are private to your Linux account. Wallet passwords are not stored in the GUI settings.

To use a terminal wallet in the GUI, make a wallet backup through the terminal app, then select **Manage wallet > Restore wallet file** in the GUI and choose a new wallet name. Recovery words can also restore a deterministic wallet. Never copy a live `wallet.dat` file while its node is writing to it. Do not move or delete the original wallet when trying the GUI.

For testing, use a separate wallet first. Avoid sending from two copies of the same wallet at the same time.

Opening or restoring a wallet can show a fresh receiving address. Previously used addresses still belong to that wallet. Check that the restored wallet finds your transactions and balance.

Under **Receive**, you can create another address or copy a previous one. This does not change the destination of mining that is already running. **Manage wallet > Check recovery words** checks your written words against the open wallet without creating another wallet or sending the words to a public node.

Recovery-word restores scan the chain from the beginning and check receiving and change addresses through index 10,000 by default. This may take several minutes. If another wallet program used a later address, expand the restore dialog's advanced options and increase the last address index, up to 100,000. A wallet-file backup is preferable when you are unsure how that wallet generated its addresses. See [Wallet tools](WALLET-TOOLS.md) for transaction history, coin control and recovery details.

## What to test

Create a wallet and record its recovery words privately. Confirm that the node catches up, then start mining. Check the total and spendable balances as rewards arrive.

The thread-count link under the mining button on Home opens Settings and focuses the CPU mining slider. Opening it does not change your thread count or stop mining.

Choose your thread count under **Settings > CPU mining**. The slider runs from one thread to the number of logical processors available to the app. Slithy remembers your choice. A higher count gives mining more of your CPU and can increase heat and power use. If mining is already running, stop and start it to use the new count. Moving the slider does not interrupt a running miner.

The Home page no longer has the Quiet, Balanced and Full power dropdown. Slithy starts with one CPU thread when no thread count has been saved. Choose a different count in Settings and the app remembers it. Updates leave an existing saved count in place.

In **Settings**, toggle automatic wallet locking and restart the app to confirm the choice is remembered. With locking enabled, let the timer expire. The GUI should label the wallet as locked while continuing to display its balance. Mining should continue until you stop it. Sending requests your password.

Send a small amount to another test wallet. Check the displayed fee before confirming. Test a backup and recovery-word restore, including the restored address and balance. Try closing to the notification area and using **Quit**. Before hiding for the first time, the app asks you to confirm that the tray icon is visible. If it is missing, cancel and turn off the notification-area option in Settings. Closing will then stop this app's node and exit.

Report the Linux distribution, desktop environment and CPU model with any bug. Do not include passwords, recovery words, private keys or RPC cookies in a report.

## Node behavior

The GUI starts its own bundled `slithyd` on the beta chain. It uses cookie-authenticated RPC on `127.0.0.1:54426`; it does not expose RPC or accept inbound P2P connections. All three official seeds remain available for failover. Automatic mode tries the quickest responding seed first. A node can still maintain connections to several peers.

Quiet beta periods use the same old-tip allowance as the Windows app. This does not disable block validation, header checks or chain-work checks. The shared mining checks still require a usable peer connection and a synchronized local chain.

If the GUI crashes and leaves its native node running, the next launch checks the saved process record, executable and local authentication. When they match this app and its data folder, it offers to stop that node safely and reopen it. Agreeing stops any mining on the old node. The app waits for the node to save its data before starting a replacement.

A missing or mismatched recovery record, a different native build or an unknown process is refused. Slithy does not force-stop that process. An older build that did not write a recovery record needs a normal shutdown before you open this version. Ask for help if you cannot identify the running node.

The native tools must match the reviewed Slithy beta. The GUI checks the beta genesis hash at startup. For development, `SLITHY_NODE_BIN` can point to an absolute filename for a reviewed Linux `slithyd` binary.

## Updates

Updates have been available inside the GUI since version 0.1.44. By default, Slithy checks when it opens and about once an hour while it runs. Under **Settings > Updates**, clear **Check for updates when Slithy opens and while it is running** to turn off those checks. **Check now** still lets you check yourself.

A newer release adds an **Update available** badge beside the version and a notice above the tabs. Choose **Review update** to read the release notes and start the download. **Later** dismisses the notice; the badge stays available. The same release will not show that notice again during the current session.

On desktops with `notify-send` and a working notification service, Slithy also sends a desktop notification. The in-app notice works without that service.

You can download an update while mining. **Stop mining** remains available during the download, and **Cancel download** cancels the update. After checking the files, Slithy stops its local node and opens the new version. Mining does not restart automatically. Finish or cancel an open payment before updating. Back up your wallet before an update, particularly during beta testing.

The GUI checks its own signed release feed:

`https://slithy.io/updates/linux-gui-x64/beta-20260909/stable.json`

A release must be published to that feed before it appears in the app. The GUI verifies the release signature and the archive's SHA256 hash before installing. It also checks the platform and beta-network identity. The terminal app uses a separate feed and updater.

Updates install into `~/.local/opt/slithy-tove/<version>/`. The application-menu launcher then points to that version. Downloads use `~/.cache/slithy-tove-gui/updates/`, or the matching folder under an absolute `XDG_CACHE_HOME`. Wallet and node data stay in the directories listed above. An application archive must never replace your wallet data directory.

### Upgrading from 0.1.43-beta.1 or earlier

Older GUI packages cannot install an update themselves. They need one manual upgrade to gain the new updater. Obtain the newer test package, close the GUI, and extract it into a separate folder. Open **slithy-install**, then open **Slithy Tove** from the application menu after installation. Your wallet files and settings stay in place. After that, published GUI updates can be installed from Settings.

For published packages, use [slithy.io/downloads.html](https://slithy.io/downloads.html) and compare the supplied checksum and signed release information. A checksum detects a different or damaged download; the release signature verifies it against the project's release key.

## Build and developer checks

The Avalonia interface links the existing Windows wallet RPC client and recovery-word code. The Windows Forms interface itself is not used on Linux.

From the repository root with the .NET SDK installed:

```bash
bash tools/build-core.sh linux
bash tools/build-linux-gui.sh "$PWD/build/native/linux/bin/slithyd" "$PWD/build/linux-gui"
```

The GUI build script includes the native daemon as `Binaries/slithyd`, along with its notices and application-menu installer. Use a new output folder for each build. Do not substitute a Bitcoin binary or an old Slithy network build.

From the packaged folder:

```bash
./slithy-desktop --self-test
./slithy-desktop --integration-test
./slithy-desktop --update-test
./slithy-desktop --ui-test --screenshots /tmp/slithy-gui-screenshots
./slithy-desktop --installer-ui-test --screenshots /tmp/slithy-installer-screenshots
```

The self-test checks settings and startup safeguards. The integration test starts a fresh disconnected regtest node, creates temporary wallets and exercises payments and recovery. It does not transact on the public beta. Test directories are separate from the GUI's normal beta wallets.

The updater test uses local fixtures to check signatures, archives and installation failures. The UI test renders the wallet picker and each tab at several window sizes. It checks the update notice and mining-button states without connecting to a node.

Use the source archive matching the package version when building or reporting a bug. The application and native daemon have separate dependency notices in `licenses/`. Preserve those notices when distributing your own build.
